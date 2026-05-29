using ChainPOS.ViewModels.Sales;

namespace ChainPOS.Services.Sales;

public interface IOrderService
{
    Task<OrderIndexViewModel> GetOrdersAsync(
        string areaName,
        Guid? storeId,
        string? search,
        string? status,
        string? paymentStatus,
        DateOnly? date,
        CancellationToken cancellationToken = default);

    Task<OrderDetailsViewModel?> GetOrderDetailsAsync(
        string areaName,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> CancelOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
