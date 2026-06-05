using ChainPOS.Contracts.Payments;
using ChainPOS.ViewModels.SystemPayments;

namespace ChainPOS.Services.Payments;

public interface ISystemPaymentSePayService
{
    Task<SystemPaymentCheckoutViewModel?> BuildCheckoutPageAsync(
        Guid paymentId,
        Guid? tenantId,
        bool allowAnyTenant,
        CancellationToken cancellationToken = default);

    Task<SystemPaymentStatusResponse?> GetStatusAsync(
        Guid paymentId,
        Guid? tenantId,
        bool allowAnyTenant,
        CancellationToken cancellationToken = default);

    Task<SystemPaymentWebhookProcessResult> HandleWebhookAsync(
        SepayWebhookPayload payload,
        string rawPayload,
        string? authorizationHeader,
        string? apiKeyHeader,
        CancellationToken cancellationToken = default);
}

public sealed record SystemPaymentWebhookProcessResult(
    bool Success,
    bool Authorized,
    bool Processed,
    string Message,
    Guid? PaymentId)
{
    public static SystemPaymentWebhookProcessResult CreateUnauthorized()
        => new(false, false, false, "Webhook authorization failed.", null);

    public static SystemPaymentWebhookProcessResult CreateProcessed(Guid paymentId)
        => new(true, true, true, "Webhook processed successfully.", paymentId);

    public static SystemPaymentWebhookProcessResult CreateIgnored(string message)
        => new(true, true, false, message, null);
}
