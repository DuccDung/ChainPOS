namespace ChainPOS.Models;

public partial class SystemPaymentWebhook
{
    public Guid Id { get; set; }

    public Guid? SystemPaymentId { get; set; }

    public string Gateway { get; set; } = null!;

    public string? EventType { get; set; }

    public string? ReferenceCode { get; set; }

    public string? ContentTransfer { get; set; }

    public decimal? Amount { get; set; }

    public string RawPayload { get; set; } = null!;

    public bool IsProcessed { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual SystemPayment? SystemPayment { get; set; }
}
