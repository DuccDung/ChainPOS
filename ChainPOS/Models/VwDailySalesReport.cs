using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class VwDailySalesReport
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public DateOnly? ReportDate { get; set; }

    public long? OrderCount { get; set; }

    public decimal? SubTotal { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? TotalAmount { get; set; }
}
