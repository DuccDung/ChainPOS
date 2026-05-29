using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Sales;

public sealed class PosCartItemInputModel
{
    [Required]
    public Guid? ProductId { get; set; }

    [Range(typeof(decimal), "0.001", "9999999999999999")]
    public decimal Quantity { get; set; }
}
