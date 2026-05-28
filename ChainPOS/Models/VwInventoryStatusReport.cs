using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class VwInventoryStatusReport
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public decimal Quantity { get; set; }

    public decimal MinQuantity { get; set; }

    public bool? IsLowStock { get; set; }

    public DateTime UpdatedAt { get; set; }
}
