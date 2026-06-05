using System.ComponentModel.DataAnnotations;
using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Sales;

public sealed class PosCheckoutInputModel
{
    public string AreaName { get; set; } = string.Empty;

    [Required]
    public Guid? StoreId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal DiscountAmount { get; set; }

    [Required]
    [StringLength(30)]
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;

    [StringLength(20)]
    public string CheckoutMode { get; set; } = "pay";

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal CustomerPaidAmount { get; set; }

    [StringLength(100)]
    public string? TransactionCode { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public List<PosCartItemInputModel> Items { get; set; } = new();
}
