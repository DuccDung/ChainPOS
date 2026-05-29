using ChainPOS.ViewModels.Owner.Stores;

namespace ChainPOS.Services.Owner;

public interface IOwnerStoreService
{
    Task<StoreIndexViewModel> GetStoresAsync(string? search, string? status, CancellationToken cancellationToken = default);

    Task<StoreDetailsViewModel?> GetStoreDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StoreFormViewModel?> GetStoreFormAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? StoreId)> CreateStoreAsync(StoreFormViewModel model, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> UpdateStoreAsync(Guid id, StoreFormViewModel model, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetStoreStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> DeleteStoreAsync(Guid id, CancellationToken cancellationToken = default);
}
