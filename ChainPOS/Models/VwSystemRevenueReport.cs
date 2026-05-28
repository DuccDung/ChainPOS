using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class VwSystemRevenueReport
{
    public Guid TenantId { get; set; }

    public DateOnly? PaidDate { get; set; }

    public long? PaymentCount { get; set; }

    public decimal? TotalAmount { get; set; }
}
