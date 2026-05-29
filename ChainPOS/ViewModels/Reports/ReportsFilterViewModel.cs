using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Reports;

public sealed class ReportsFilterViewModel
{
    public Guid? TenantId { get; set; }

    public Guid? StoreId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? FromDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? ToDate { get; set; }
}
