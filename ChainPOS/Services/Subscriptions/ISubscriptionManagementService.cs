using ChainPOS.ViewModels.Admin.SubscriptionPlans;
using ChainPOS.ViewModels.Admin.Subscriptions;
using ChainPOS.ViewModels.Admin.SystemPayments;
using ChainPOS.ViewModels.Owner.Subscriptions;

namespace ChainPOS.Services.Subscriptions;

public interface ISubscriptionManagementService
{
    Task<SubscriptionPlanIndexViewModel> GetPlansAsync(string? search, string? status, CancellationToken cancellationToken = default);

    Task<SubscriptionPlanFormViewModel?> GetPlanFormAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? PlanId)> CreatePlanAsync(
        SubscriptionPlanFormViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> UpdatePlanAsync(
        Guid id,
        SubscriptionPlanFormViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetPlanActiveAsync(
        Guid id,
        bool isActive,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> DeletePlanAsync(
        Guid id,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<TenantSubscriptionCreateViewModel> GetTenantSubscriptionCreateAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, Guid? SubscriptionId, Guid? PaymentId)> CreateTenantSubscriptionAsync(
        TenantSubscriptionCreateViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<SystemPaymentIndexViewModel> GetSystemPaymentsAsync(
        Guid? tenantId,
        string? status,
        string? search,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> MarkSystemPaymentPaidAsync(
        Guid id,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> MarkSystemPaymentFailedAsync(
        Guid id,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<OwnerSubscriptionIndexViewModel> GetOwnerSubscriptionAsync(CancellationToken cancellationToken = default);
}
