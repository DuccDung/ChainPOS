namespace ChainPOS.Services.Realtime;

public sealed record InventoryChangedEvent(
    Guid TenantId,
    Guid StoreId,
    Guid ProductId,
    string StoreName,
    string StoreCode,
    string ProductName,
    string? Sku,
    decimal Quantity,
    decimal MinQuantity,
    string ChangeType,
    decimal Delta,
    DateTime OccurredAt);

public sealed record OrderCreatedEvent(
    Guid TenantId,
    Guid StoreId,
    Guid OrderId,
    string OrderCode,
    string StoreName,
    string StoreCode,
    string? StaffName,
    int ItemCount,
    decimal TotalAmount,
    string PaymentStatus,
    string OrderStatus,
    DateTime CreatedAt);

public sealed record OrderCancelledEvent(
    Guid TenantId,
    Guid StoreId,
    Guid OrderId,
    string OrderCode,
    string PaymentStatus,
    string OrderStatus,
    DateTime CancelledAt);

public sealed record ShiftChangedEvent(
    Guid TenantId,
    Guid StoreId,
    Guid ShiftId,
    string StoreName,
    string StoreCode,
    string OpenedBy,
    string Status,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal? ExpectedCash,
    decimal? DifferenceAmount,
    DateTime OccurredAt);

public sealed record SubscriptionChangedEvent(
    Guid TenantId,
    Guid SubscriptionId,
    string TenantName,
    string PlanName,
    string Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateTime OccurredAt);

public sealed record SystemPaymentChangedEvent(
    Guid TenantId,
    Guid PaymentId,
    string TenantName,
    string PlanName,
    decimal Amount,
    string Method,
    string Status,
    DateTime? PaidAt,
    DateTime OccurredAt);
