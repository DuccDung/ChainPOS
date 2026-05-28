using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class VwStaffSalesReport
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public string? StaffUserId { get; set; }

    public DateOnly? ReportDate { get; set; }

    public long? OrderCount { get; set; }

    public decimal? TotalSales { get; set; }
}
