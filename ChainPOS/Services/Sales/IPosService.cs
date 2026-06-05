using ChainPOS.ViewModels.Sales;

namespace ChainPOS.Services.Sales;

public interface IPosService
{
    Task<PosIndexViewModel> GetRegisterAsync(
        string areaName,
        Guid? storeId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? OrderId)> CheckoutAsync(
        PosCheckoutInputModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? OrderId)> CompletePendingOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> CancelPendingOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
