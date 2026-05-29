using ChainPOS.ViewModels.Owner.StoreProducts;

namespace ChainPOS.Services.Owner;

public interface IOwnerStoreProductService
{
    Task<StoreProductIndexViewModel> GetStoreProductsAsync(
        Guid? storeId,
        string? search,
        string? availability,
        CancellationToken cancellationToken = default);

    Task<StoreProductAssignViewModel> GetAssignFormAsync(
        StoreProductAssignViewModel? model = null,
        CancellationToken cancellationToken = default);

    Task<StoreProductEditViewModel?> GetEditFormAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? StoreProductId)> AssignProductAsync(
        StoreProductAssignViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> UpdateStoreProductAsync(
        Guid id,
        StoreProductEditViewModel model,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetAvailabilityAsync(
        Guid id,
        bool isAvailable,
        CancellationToken cancellationToken = default);

    Task<decimal?> GetEffectiveSellingPriceAsync(
        Guid storeId,
        Guid productId,
        CancellationToken cancellationToken = default);
}
