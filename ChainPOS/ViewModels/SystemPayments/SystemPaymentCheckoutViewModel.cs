using System.Globalization;

namespace ChainPOS.ViewModels.SystemPayments;

public sealed class SystemPaymentCheckoutViewModel
{
    public string LayoutPath { get; set; } = "~/Views/Shared/_OwnerLayout.cshtml";

    public string BackUrl { get; set; } = "/Owner/Subscription";

    public string BackLabel { get; set; } = "Back";

    public Guid PaymentId { get; init; }

    public string PageTitle { get; init; } = "System payment";

    public string TenantName { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public string PaymentStatus { get; init; } = string.Empty;

    public string StatusTitle { get; init; } = string.Empty;

    public string StatusDescription { get; init; } = string.Empty;

    public string TransactionCode { get; init; } = string.Empty;

    public string ReceiverBankName { get; init; } = string.Empty;

    public string ReceiverBankShortName { get; init; } = string.Empty;

    public string ReceiverAccountNumber { get; init; } = string.Empty;

    public string ReceiverAccountName { get; init; } = string.Empty;

    public string TransferContent { get; init; } = string.Empty;

    public string QrImageUrl { get; init; } = string.Empty;

    public bool HasQrCode { get; init; }

    public decimal Amount { get; init; }

    public decimal? PaidAmount { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public bool IsPaid { get; init; }

    public bool IsExpired { get; init; }

    public string PollStatusUrl { get; init; } = string.Empty;

    public IReadOnlyList<SystemPaymentInfoRowViewModel> PaymentInfoRows { get; init; } = Array.Empty<SystemPaymentInfoRowViewModel>();

    public string AmountText => FormatMoney(Amount);

    public string PaidAmountText => PaidAmount.HasValue ? FormatMoney(PaidAmount.Value) : "-";

    public string CreatedAtText => FormatDateTime(CreatedAt);

    public string? ExpiresAtText => ExpiresAt.HasValue ? FormatDateTime(ExpiresAt.Value) : null;

    public string? PaidAtText => PaidAt.HasValue ? FormatDateTime(PaidAt.Value) : null;

    public static string FormatMoney(decimal amount)
        => string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} d", Math.Max(0, amount));

    private static string FormatDateTime(DateTime value)
        => value.ToLocalTime().ToString("HH:mm dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
}

public sealed class SystemPaymentInfoRowViewModel
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string? CopyValue { get; init; }

    public bool CanCopy => !string.IsNullOrWhiteSpace(CopyValue);
}
