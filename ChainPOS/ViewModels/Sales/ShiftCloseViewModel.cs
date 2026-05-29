using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Sales;

public sealed class ShiftCloseViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public Guid Id { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal CashSales { get; set; }

    public decimal ExpectedCash { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Closing Cash")]
    public decimal ClosingCash { get; set; }

    public decimal DifferenceAmount => ClosingCash - ExpectedCash;
}
