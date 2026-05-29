namespace ChainPOS.ViewModels.Sales;

public sealed class ShiftListItemViewModel
{
    public Guid Id { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string OpenedByName { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal? ClosingCash { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? DifferenceAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public int OrderCount { get; set; }

    public decimal RevenueTotal { get; set; }

    public bool CanClose { get; set; }
}
