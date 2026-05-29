namespace ChainPOS.ViewModels.Reports;

public sealed class InventoryStatusReportItemViewModel
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public decimal Quantity { get; set; }

    public decimal MinQuantity { get; set; }

    public bool IsLowStock { get; set; }

    public DateTime UpdatedAt { get; set; }
}
