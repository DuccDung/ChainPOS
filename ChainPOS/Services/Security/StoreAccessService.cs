using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Security;

public sealed class StoreAccessService : IStoreAccessService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StoreAccessService(StoreFlowDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> CanAccessStoreAsync(Guid storeId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.TenantId.HasValue)
        {
            return false;
        }

        if (_currentUser.IsInRole(AppRoles.Admin))
        {
            return false;
        }

        if (_currentUser.IsInRole(AppRoles.Owner))
        {
            return await _db.Stores.AnyAsync(
                x => x.Id == storeId
                    && x.TenantId == _currentUser.TenantId.Value
                    && !x.IsDeleted
                    && x.Status == StoreStatuses.Active,
                cancellationToken);
        }

        if (_currentUser.IsInRole(AppRoles.Staff) && !string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return await _db.UserStores.AnyAsync(
                x => x.StoreId == storeId
                    && x.TenantId == _currentUser.TenantId.Value
                    && x.UserId == _currentUser.UserId
                    && x.IsActive
                    && !x.Store.IsDeleted
                    && x.Store.Status == StoreStatuses.Active,
                cancellationToken);
        }

        return false;
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleStoreIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.TenantId.HasValue)
        {
            return Array.Empty<Guid>();
        }

        if (_currentUser.IsInRole(AppRoles.Admin))
        {
            return Array.Empty<Guid>();
        }

        if (_currentUser.IsInRole(AppRoles.Owner))
        {
            return await _db.Stores
                .Where(x => x.TenantId == _currentUser.TenantId.Value
                    && !x.IsDeleted
                    && x.Status == StoreStatuses.Active)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        if (_currentUser.IsInRole(AppRoles.Staff) && !string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return await _db.UserStores
                .Where(x => x.TenantId == _currentUser.TenantId.Value
                    && x.UserId == _currentUser.UserId
                    && x.IsActive
                    && !x.Store.IsDeleted
                    && x.Store.Status == StoreStatuses.Active)
                .Select(x => x.StoreId)
                .ToListAsync(cancellationToken);
        }

        return Array.Empty<Guid>();
    }
}
