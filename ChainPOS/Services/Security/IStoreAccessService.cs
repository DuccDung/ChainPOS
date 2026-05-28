namespace ChainPOS.Services.Security;

public interface IStoreAccessService
{
    Task<bool> CanAccessStoreAsync(Guid storeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetAccessibleStoreIdsAsync(CancellationToken cancellationToken = default);
}
