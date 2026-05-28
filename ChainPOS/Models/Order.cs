using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class Order
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public string OrderCode { get; set; } = null!;

    public string? StaffUserId { get; set; }

    public Guid? ShiftId { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public string OrderStatus { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelledBy { get; set; }

    public virtual AspNetUser? CancelledByNavigation { get; set; }

    public virtual AspNetUser? CreatedByNavigation { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Shift? Shift { get; set; }

    public virtual AspNetUser? StaffUser { get; set; }

    public virtual Store Store { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
