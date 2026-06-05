using System.Data;
using System.Text;
using System.Text.Json;
using ChainPOS.Constants;
using ChainPOS.Contracts.Payments;
using ChainPOS.Models;
using ChainPOS.Options;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Realtime;
using ChainPOS.ViewModels.SystemPayments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChainPOS.Services.Payments;

public sealed class SystemPaymentSePayService : ISystemPaymentSePayService
{
    private readonly StoreFlowDbContext _db;
    private readonly SepayGatewayClient _sepayGatewayClient;
    private readonly SePayOptions _options;
    private readonly IAuditLogService _auditLog;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public SystemPaymentSePayService(
        StoreFlowDbContext db,
        SepayGatewayClient sepayGatewayClient,
        IOptions<SePayOptions> sePayOptions,
        IAuditLogService auditLog,
        IRealtimeNotifier realtimeNotifier)
    {
        _db = db;
        _sepayGatewayClient = sepayGatewayClient;
        _options = sePayOptions.Value;
        _auditLog = auditLog;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<SystemPaymentCheckoutViewModel?> BuildCheckoutPageAsync(
        Guid paymentId,
        Guid? tenantId,
        bool allowAnyTenant,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadPaymentAsync(paymentId, tracking: true, cancellationToken);
        if (payment is null || !CanAccess(payment, tenantId, allowAnyTenant) || !IsSePayPayment(payment))
        {
            return null;
        }

        if (string.Equals(payment.Status, PaymentStatuses.Pending, StringComparison.OrdinalIgnoreCase)
            && ShouldRefreshCheckout(payment))
        {
            await InitializeCheckoutAsync(payment, cancellationToken);
        }

        return BuildCheckoutViewModel(payment);
    }

    public async Task<SystemPaymentStatusResponse?> GetStatusAsync(
        Guid paymentId,
        Guid? tenantId,
        bool allowAnyTenant,
        CancellationToken cancellationToken = default)
    {
        var payment = await LoadPaymentAsync(paymentId, tracking: false, cancellationToken);
        if (payment is null || !CanAccess(payment, tenantId, allowAnyTenant) || !IsSePayPayment(payment))
        {
            return null;
        }

        var (title, description) = ResolveStatusCopy(payment);
        return new SystemPaymentStatusResponse(
            payment.Id,
            payment.Status,
            string.Equals(payment.Status, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase),
            IsExpired(payment),
            title,
            description,
            payment.PaidAt);
    }

    public async Task<SystemPaymentWebhookProcessResult> HandleWebhookAsync(
        SepayWebhookPayload payload,
        string rawPayload,
        string? authorizationHeader,
        string? apiKeyHeader,
        CancellationToken cancellationToken = default)
    {
        if (!IsWebhookAuthorized(authorizationHeader, apiKeyHeader))
        {
            return SystemPaymentWebhookProcessResult.CreateUnauthorized();
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var utcNow = DateTime.UtcNow;
            var webhook = new SystemPaymentWebhook
            {
                Id = Guid.NewGuid(),
                Gateway = string.IsNullOrWhiteSpace(payload.Gateway) ? "sepay" : payload.Gateway!,
                EventType = payload.TransferType,
                ReferenceCode = payload.ReferenceCode,
                ContentTransfer = payload.Content,
                Amount = payload.TransferAmount,
                RawPayload = rawPayload,
                IsProcessed = false,
                CreatedAt = utcNow
            };
            _db.SystemPaymentWebhooks.Add(webhook);

            if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase) ||
                payload.TransferAmount <= 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return SystemPaymentWebhookProcessResult.CreateIgnored("Webhook was stored, but it is not an incoming payment.");
            }

            if (!string.IsNullOrWhiteSpace(_options.ReceiverAccountNumber) &&
                !string.IsNullOrWhiteSpace(payload.AccountNumber) &&
                !string.Equals(_options.ReceiverAccountNumber, payload.AccountNumber, StringComparison.OrdinalIgnoreCase))
            {
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return SystemPaymentWebhookProcessResult.CreateIgnored("Webhook account number does not match the configured SePay receiver.");
            }

            var payment = await FindSystemPaymentForWebhookAsync(payload, cancellationToken);
            if (payment is null)
            {
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return SystemPaymentWebhookProcessResult.CreateIgnored("No matching ChainPOS system payment was found.");
            }

            webhook.SystemPaymentId = payment.Id;

            if (string.Equals(payment.Status, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            {
                webhook.IsProcessed = true;
                webhook.ProcessedAt = utcNow;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await NotifySystemPaymentChangedAsync(payment, cancellationToken);
                return SystemPaymentWebhookProcessResult.CreateProcessed(payment.Id);
            }

            var oldValue = PaymentAuditValue(payment);
            payment.Status = PaymentStatuses.Paid;
            payment.ProviderTransactionId = string.IsNullOrWhiteSpace(payload.ReferenceCode)
                ? payment.ProviderTransactionId
                : payload.ReferenceCode;
            payment.PaidAmount = payload.TransferAmount;
            payment.PaidAt = utcNow;
            payment.RawResponse = rawPayload;
            payment.UpdatedAt = utcNow;

            webhook.IsProcessed = true;
            webhook.ProcessedAt = utcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await _auditLog.LogForUserAsync(
                "SePaySystemPaymentPaid",
                null,
                nameof(SystemPayment),
                payment.Id.ToString(),
                oldValue,
                PaymentAuditValue(payment),
                payment.TenantId,
                cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await NotifySystemPaymentChangedAsync(payment, cancellationToken);

            return SystemPaymentWebhookProcessResult.CreateProcessed(payment.Id);
        });
    }

    private async Task<SystemPayment?> LoadPaymentAsync(
        Guid paymentId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = _db.SystemPayments
            .Include(x => x.Tenant)
            .Include(x => x.Subscription)
            .ThenInclude(x => x.Plan)
            .Where(x => x.Id == paymentId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task InitializeCheckoutAsync(SystemPayment payment, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var transactionCode = string.IsNullOrWhiteSpace(payment.TransactionCode)
            ? await GenerateUniqueTransactionCodeAsync(cancellationToken)
            : payment.TransactionCode.Trim();
        var sepayCheckout = await _sepayGatewayClient.PrepareCheckoutAsync(
            payment.Amount,
            transactionCode,
            cancellationToken);

        payment.TransactionCode = transactionCode;
        payment.BankCode = sepayCheckout.BankShortName;
        payment.BankAccountNo = sepayCheckout.AccountNumber;
        payment.BankAccountName = sepayCheckout.AccountName;
        payment.QrContent = sepayCheckout.QrImageUrl;
        payment.TransferContent = transactionCode;
        payment.RawResponse = JsonSerializer.Serialize(new
        {
            transactionCode,
            systemPaymentId = payment.Id,
            sepayCheckout.BankShortName,
            sepayCheckout.AccountNumber,
            payment.Amount,
            sepayCheckout.ResolvedByApi,
            sepayCheckout.ProviderRawResponse
        });
        payment.ExpiredAt = _options.PaymentExpireMinutes > 0
            ? utcNow.AddMinutes(_options.PaymentExpireMinutes)
            : null;
        payment.UpdatedAt = utcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private bool ShouldRefreshCheckout(SystemPayment payment)
    {
        if (string.IsNullOrWhiteSpace(payment.TransactionCode) ||
            string.IsNullOrWhiteSpace(payment.QrContent))
        {
            return true;
        }

        if (ConfiguredValueChanged(payment.BankAccountNo, _options.ReceiverAccountNumber) ||
            ConfiguredValueChanged(payment.BankCode, _options.ReceiverBankShortName) ||
            ConfiguredValueChanged(payment.BankAccountName, _options.ReceiverAccountName))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(_options.ReceiverAccountNumber) &&
               !payment.QrContent.Contains(_options.ReceiverAccountNumber.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private SystemPaymentCheckoutViewModel BuildCheckoutViewModel(SystemPayment payment)
    {
        var isPaid = string.Equals(payment.Status, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase);
        var isExpired = IsExpired(payment);
        var receiverBankShortName = ResolvePreferredText(payment.BankCode, _options.ReceiverBankShortName);
        var receiverAccountNumber = ResolvePreferredText(payment.BankAccountNo, _options.ReceiverAccountNumber);
        var receiverAccountName = ResolvePreferredText(payment.BankAccountName, _options.ReceiverAccountName);
        var receiverBankName = ResolvePreferredText(_options.ReceiverBankName, receiverBankShortName);
        var transferContent = ResolvePreferredText(payment.TransferContent, payment.TransactionCode);
        var amountText = SystemPaymentCheckoutViewModel.FormatMoney(payment.Amount);
        var (statusTitle, statusDescription) = ResolveStatusCopy(payment);

        return new SystemPaymentCheckoutViewModel
        {
            PaymentId = payment.Id,
            PageTitle = $"System payment {payment.TransactionCode ?? payment.Id.ToString("N")[..8]}",
            TenantName = payment.Tenant.Name,
            PlanName = payment.Subscription.Plan.Name,
            PaymentStatus = payment.Status,
            StatusTitle = statusTitle,
            StatusDescription = statusDescription,
            TransactionCode = ResolvePreferredText(payment.TransactionCode),
            ReceiverBankName = receiverBankName,
            ReceiverBankShortName = receiverBankShortName,
            ReceiverAccountNumber = receiverAccountNumber,
            ReceiverAccountName = receiverAccountName,
            TransferContent = transferContent,
            QrImageUrl = ResolvePreferredText(payment.QrContent),
            HasQrCode = !string.IsNullOrWhiteSpace(payment.QrContent),
            Amount = payment.Amount,
            PaidAmount = payment.PaidAmount,
            CreatedAt = payment.CreatedAt,
            ExpiresAt = payment.ExpiredAt,
            PaidAt = payment.PaidAt,
            IsPaid = isPaid,
            IsExpired = isExpired,
            PollStatusUrl = $"/api/system-payments/{payment.Id}/status",
            PaymentInfoRows =
            [
                new SystemPaymentInfoRowViewModel
                {
                    Label = "Bank",
                    Value = receiverBankName
                },
                new SystemPaymentInfoRowViewModel
                {
                    Label = "Account number",
                    Value = receiverAccountNumber,
                    CopyValue = receiverAccountNumber
                },
                new SystemPaymentInfoRowViewModel
                {
                    Label = "Account name",
                    Value = receiverAccountName
                },
                new SystemPaymentInfoRowViewModel
                {
                    Label = "Transfer content",
                    Value = transferContent,
                    CopyValue = transferContent
                },
                new SystemPaymentInfoRowViewModel
                {
                    Label = "Amount",
                    Value = amountText,
                    CopyValue = decimal.Round(payment.Amount, 0, MidpointRounding.AwayFromZero).ToString("0")
                }
            ]
        };
    }

    private async Task<SystemPayment?> FindSystemPaymentForWebhookAsync(
        SepayWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        var normalizedAmount = decimal.Round(payload.TransferAmount, 0, MidpointRounding.AwayFromZero);
        var candidateTokens = ExtractCandidateTokens(
            payload.Code,
            payload.Content,
            payload.Description,
            payload.ReferenceCode);
        var normalizedCode = NormalizeToken(payload.Code);
        var normalizedContent = NormalizeCombinedContent(payload.Code, payload.Content, payload.Description);

        if (candidateTokens.Length > 0)
        {
            var exactMatch = await BaseWebhookPaymentQuery()
                .Where(x => x.Amount == normalizedAmount &&
                    (candidateTokens.Contains(x.TransactionCode!) ||
                     (x.TransferContent != null && candidateTokens.Contains(x.TransferContent))))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (exactMatch is not null)
            {
                return exactMatch;
            }
        }

        var candidates = await BaseWebhookPaymentQuery()
            .Where(x => x.Amount == normalizedAmount)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var transactionCode = NormalizeToken(candidate.TransactionCode);
            var transferContent = NormalizeToken(candidate.TransferContent);

            if (!string.IsNullOrWhiteSpace(normalizedCode) &&
                (normalizedCode == transactionCode || normalizedCode == transferContent))
            {
                return candidate;
            }

            if (!string.IsNullOrWhiteSpace(normalizedContent) &&
                ((!string.IsNullOrWhiteSpace(transactionCode) && normalizedContent.Contains(transactionCode, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(transferContent) && normalizedContent.Contains(transferContent, StringComparison.OrdinalIgnoreCase))))
            {
                return candidate;
            }
        }

        return null;
    }

    private IQueryable<SystemPayment> BaseWebhookPaymentQuery()
    {
        return _db.SystemPayments
            .Include(x => x.Tenant)
            .Include(x => x.Subscription)
            .ThenInclude(x => x.Plan)
            .Where(x => x.Method == PaymentMethods.SePay &&
                (x.Status == PaymentStatuses.Pending || x.Status == PaymentStatuses.Paid));
    }

    private async Task<string> GenerateUniqueTransactionCodeAsync(CancellationToken cancellationToken)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.TransferCodePrefix)
            ? "CP"
            : NormalizeToken(_options.TransferCodePrefix) ?? "CP";

        while (true)
        {
            var guidToken = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var candidate = $"{prefix}{guidToken}";
            var exists = await _db.SystemPayments
                .AsNoTracking()
                .AnyAsync(x => x.TransactionCode == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }
        }
    }

    private async Task NotifySystemPaymentChangedAsync(SystemPayment payment, CancellationToken cancellationToken)
    {
        await _realtimeNotifier.SystemPaymentChangedAsync(
            new SystemPaymentChangedEvent(
                payment.TenantId,
                payment.Id,
                payment.Tenant.Name,
                payment.Subscription.Plan.Name,
                payment.Amount,
                payment.Method,
                payment.Status,
                payment.PaidAt,
                DateTime.UtcNow),
            cancellationToken);
    }

    private bool IsWebhookAuthorized(string? authorizationHeader, string? apiKeyHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookApiKey))
        {
            return true;
        }

        var expected = _options.WebhookApiKey.Trim();
        if (string.Equals(apiKeyHeader?.Trim(), expected, StringComparison.Ordinal))
        {
            return true;
        }

        var normalizedAuthorization = authorizationHeader?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAuthorization))
        {
            return false;
        }

        return string.Equals(normalizedAuthorization, $"Apikey {expected}", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedAuthorization, $"Bearer {expected}", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedAuthorization, expected, StringComparison.Ordinal);
    }

    private static bool CanAccess(SystemPayment payment, Guid? tenantId, bool allowAnyTenant)
    {
        return allowAnyTenant || (tenantId.HasValue && payment.TenantId == tenantId.Value);
    }

    private static bool ConfiguredValueChanged(string? currentValue, string? configuredValue)
    {
        return !string.IsNullOrWhiteSpace(configuredValue) &&
               !string.Equals(
                   currentValue?.Trim(),
                   configuredValue.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSePayPayment(SystemPayment payment)
        => string.Equals(payment.Method, PaymentMethods.SePay, StringComparison.OrdinalIgnoreCase);

    private static bool IsExpired(SystemPayment payment)
    {
        return !string.Equals(payment.Status, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase) &&
               payment.ExpiredAt.HasValue &&
               payment.ExpiredAt.Value < DateTime.UtcNow;
    }

    private static (string Title, string Description) ResolveStatusCopy(SystemPayment payment)
    {
        var transferContent = ResolvePreferredText(payment.TransferContent, payment.TransactionCode);
        if (string.Equals(payment.Status, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
        {
            return (
                "SePay payment completed",
                "ChainPOS has received the SePay webhook and marked this SaaS payment as paid.");
        }

        if (IsExpired(payment))
        {
            return (
                "Payment code expired",
                "This SePay QR code has passed its configured expiry time. Contact the platform admin if you need a new payment request.");
        }

        return (
            "Waiting for SePay confirmation",
            $"Transfer the exact amount with content {transferContent}. The payment will be marked paid automatically when SePay sends the webhook.");
    }

    private static string ResolvePreferredText(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeCombinedContent(params string?[] values)
    {
        return string.Concat(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeToken(value) ?? string.Empty));
    }

    private static string[] ExtractCandidateTokens(params string?[] values)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var builder = new StringBuilder();
            foreach (var character in value.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    continue;
                }

                FlushCandidateToken(builder, tokens);
            }

            FlushCandidateToken(builder, tokens);
        }

        return tokens.ToArray();
    }

    private static void FlushCandidateToken(StringBuilder builder, ISet<string> tokens)
    {
        if (builder.Length >= 6)
        {
            tokens.Add(builder.ToString());
        }

        builder.Clear();
    }

    private static string? NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Concat(value
            .Trim()
            .ToUpperInvariant()
            .Where(character => !char.IsWhiteSpace(character)));
    }

    private static string PaymentAuditValue(SystemPayment payment)
        => $"TenantId={payment.TenantId}; Amount={payment.Amount:#,##0.##}; Status={payment.Status}; TransactionCode={payment.TransactionCode ?? "-"}; PaidAt={payment.PaidAt?.ToString("O") ?? "-"}";

}
