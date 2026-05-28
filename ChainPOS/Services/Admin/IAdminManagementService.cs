using ChainPOS.ViewModels.Admin.Owners;
using ChainPOS.ViewModels.Admin.Tenants;

namespace ChainPOS.Services.Admin;

public interface IAdminManagementService
{
    Task<OwnerIndexViewModel> GetOwnersAsync(string? search, string? status, CancellationToken cancellationToken = default);

    Task<OwnerDetailsViewModel?> GetOwnerDetailsAsync(string id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> CreateOwnerAsync(
        OwnerCreateViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetOwnerStatusAsync(
        string id,
        string status,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<TenantIndexViewModel> GetTenantsAsync(string? search, string? status, CancellationToken cancellationToken = default);

    Task<TenantDetailsViewModel?> GetTenantDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetTenantStatusAsync(
        Guid id,
        string status,
        string? currentUserId,
        CancellationToken cancellationToken = default);
}
