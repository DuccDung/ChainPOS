using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.Services.Realtime;
using ChainPOS.ViewModels.Admin.SubscriptionPlans;
using ChainPOS.ViewModels.Admin.Subscriptions;
using ChainPOS.ViewModels.Admin.SystemPayments;
using ChainPOS.ViewModels.Owner.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Subscriptions;

public sealed class SubscriptionManagementService : ISubscriptionManagementService
{
    private readonly StoreFlowDbContext _db;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public SubscriptionManagementService(
        StoreFlowDbContext db,
        IAuditLogService auditLog,
        ICurrentUserService currentUser,
        IRealtimeNotifier realtimeNotifier)
    {
        _db = db;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<SubscriptionPlanIndexViewModel> GetPlansAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SubscriptionPlans
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var trimmedSearch = TrimToNull(search);
        if (trimmedSearch is not null)
        {
            query = query.Where(x => x.Name.Contains(trimmedSearch) || x.BillingCycle.Contains(trimmedSearch));
        }

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        var plans = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Price)
            .Select(x => new SubscriptionPlanListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                BillingCycle = x.BillingCycle,
                MaxStores = x.MaxStores,
                MaxStaff = x.MaxStaff,
                MaxProducts = x.MaxProducts,
                IsActive = x.IsActive,
                ActiveTenantCount = x.TenantSubscriptions.Count(s => s.Status == SubscriptionStatuses.Active || s.Status == SubscriptionStatuses.Trial),
                TotalTenantCount = x.TenantSubscriptions.Count,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new SubscriptionPlanIndexViewModel
        {
            Search = trimmedSearch,
            Status = status,
            TotalPlans = plans.Count,
            ActivePlans = plans.Count(x => x.IsActive),
            InactivePlans = plans.Count(x => !x.IsActive),
            TenantSubscriptions = plans.Sum(x => x.ActiveTenantCount),
            Plans = plans
        };
    }

    public async Task<SubscriptionPlanFormViewModel?> GetPlanFormAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new SubscriptionPlanFormViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                BillingCycle = x.BillingCycle,
                MaxStores = x.MaxStores,
                MaxStaff = x.MaxStaff,
                MaxProducts = x.MaxProducts,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error, Guid? PlanId)> CreatePlanAsync(
        SubscriptionPlanFormViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidatePlanAsync(model, null, cancellationToken);
        if (validationError is not null)
        {
            return (false, validationError, null);
        }

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Price = model.Price,
            BillingCycle = model.BillingCycle,
            MaxStores = model.MaxStores,
            MaxStaff = model.MaxStaff,
            MaxProducts = model.MaxProducts,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };

        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "CreateSubscriptionPlan",
            nameof(SubscriptionPlan),
            plan.Id.ToString(),
            newValue: PlanAuditValue(plan),
            cancellationToken: cancellationToken);

        return (true, null, plan.Id);
    }

    public async Task<(bool Succeeded, string? Error)> UpdatePlanAsync(
        Guid id,
        SubscriptionPlanFormViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (plan is null)
        {
            return (false, "Plan not found.");
        }

        var validationError = await ValidatePlanAsync(model, id, cancellationToken);
        if (validationError is not null)
        {
            return (false, validationError);
        }

        var oldValue = PlanAuditValue(plan);
        plan.Name = model.Name.Trim();
        plan.Price = model.Price;
        plan.BillingCycle = model.BillingCycle;
        plan.MaxStores = model.MaxStores;
        plan.MaxStaff = model.MaxStaff;
        plan.MaxProducts = model.MaxProducts;
        plan.IsActive = model.IsActive;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = currentUserId;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "UpdateSubscriptionPlan",
            nameof(SubscriptionPlan),
            plan.Id.ToString(),
            oldValue,
            PlanAuditValue(plan),
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SetPlanActiveAsync(
        Guid id,
        bool isActive,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (plan is null)
        {
            return (false, "Plan not found.");
        }

        var oldValue = $"IsActive={plan.IsActive}";
        plan.IsActive = isActive;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = currentUserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            isActive ? "ActivateSubscriptionPlan" : "DeactivateSubscriptionPlan",
            nameof(SubscriptionPlan),
            plan.Id.ToString(),
            oldValue,
            $"IsActive={plan.IsActive}",
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeletePlanAsync(
        Guid id,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _db.SubscriptionPlans
            .Include(x => x.TenantSubscriptions)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (plan is null)
        {
            return (false, "Plan not found.");
        }

        if (plan.TenantSubscriptions.Count > 0)
        {
            return (false, "Plan is already used by tenants. Deactivate it instead.");
        }

        plan.IsDeleted = true;
        plan.IsActive = false;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.UpdatedBy = currentUserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "DeleteSubscriptionPlan",
            nameof(SubscriptionPlan),
            plan.Id.ToString(),
            oldValue: PlanAuditValue(plan),
            newValue: "IsDeleted=True",
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<TenantSubscriptionCreateViewModel> GetTenantSubscriptionCreateAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return new TenantSubscriptionCreateViewModel
        {
            TenantId = tenantId,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(1).AddDays(-1),
            Tenants = await GetTenantOptionsAsync(cancellationToken),
            Plans = await GetPlanOptionsAsync(cancellationToken)
        };
    }

    public async Task<(bool Succeeded, string? Error, Guid? SubscriptionId)> CreateTenantSubscriptionAsync(
        TenantSubscriptionCreateViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!model.TenantId.HasValue || !model.PlanId.HasValue)
        {
            return (false, "Tenant and plan are required.", null);
        }

        if (!SubscriptionStatuses.All.Contains(model.Status))
        {
            return (false, "Invalid subscription status.", null);
        }

        if (model.EndDate.HasValue && model.EndDate.Value < model.StartDate)
        {
            return (false, "End date must be greater than or equal to start date.", null);
        }

        if (!PaymentMethodsAllowed(model.PaymentMethod))
        {
            return (false, "Invalid payment method.", null);
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == model.TenantId.Value && !x.IsDeleted, cancellationToken);
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == model.PlanId.Value && !x.IsDeleted && x.IsActive, cancellationToken);
        if (tenant is null)
        {
            return (false, "Tenant not found.", null);
        }

        if (plan is null)
        {
            return (false, "Active plan not found.", null);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var currentSubscriptions = await _db.TenantSubscriptions
            .Where(x => x.TenantId == tenant.Id
                && (x.Status == SubscriptionStatuses.Active || x.Status == SubscriptionStatuses.Trial || x.Status == SubscriptionStatuses.Suspended))
            .ToListAsync(cancellationToken);
        foreach (var subscription in currentSubscriptions)
        {
            subscription.Status = SubscriptionStatuses.Expired;
            subscription.EndDate = model.StartDate.AddDays(-1);
            subscription.UpdatedAt = DateTime.UtcNow;
            subscription.UpdatedBy = currentUserId;
        }

        var newSubscription = new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            PlanId = plan.Id,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            Status = model.Status,
            AutoRenew = model.AutoRenew,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };
        _db.TenantSubscriptions.Add(newSubscription);

        SystemPayment? pendingPayment = null;
        if (model.CreatePendingPayment && plan.Price > 0)
        {
            pendingPayment = new SystemPayment
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                SubscriptionId = newSubscription.Id,
                Amount = plan.Price,
                Method = model.PaymentMethod,
                Status = PaymentStatuses.Pending,
                InvoiceUrl = TrimToNull(model.InvoiceUrl),
                CreatedAt = DateTime.UtcNow
            };
            _db.SystemPayments.Add(pendingPayment);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "ChangeSubscription",
            nameof(TenantSubscription),
            newSubscription.Id.ToString(),
            oldValue: currentSubscriptions.Count == 0 ? "No active subscription" : $"{currentSubscriptions.Count} previous subscription(s) expired",
            newValue: $"Tenant={tenant.Name}; Plan={plan.Name}; Status={newSubscription.Status}; Start={newSubscription.StartDate}",
            tenantId: tenant.Id,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await _realtimeNotifier.SubscriptionChangedAsync(
            new SubscriptionChangedEvent(
                tenant.Id,
                newSubscription.Id,
                tenant.Name,
                plan.Name,
                newSubscription.Status,
                newSubscription.StartDate,
                newSubscription.EndDate,
                DateTime.UtcNow),
            cancellationToken);
        if (pendingPayment is not null)
        {
            await _realtimeNotifier.SystemPaymentChangedAsync(
                new SystemPaymentChangedEvent(
                    tenant.Id,
                    pendingPayment.Id,
                    tenant.Name,
                    plan.Name,
                    pendingPayment.Amount,
                    pendingPayment.Method,
                    pendingPayment.Status,
                    pendingPayment.PaidAt,
                    DateTime.UtcNow),
                cancellationToken);
        }

        return (true, null, newSubscription.Id);
    }

    public async Task<SystemPaymentIndexViewModel> GetSystemPaymentsAsync(
        Guid? tenantId,
        string? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SystemPayments
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Include(x => x.Subscription)
            .ThenInclude(x => x.Plan)
            .AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var trimmedSearch = TrimToNull(search);
        if (trimmedSearch is not null)
        {
            query = query.Where(x =>
                x.Tenant.Name.Contains(trimmedSearch)
                || x.Subscription.Plan.Name.Contains(trimmedSearch)
                || (x.InvoiceUrl != null && x.InvoiceUrl.Contains(trimmedSearch)));
        }

        var allForStats = await query.ToListAsync(cancellationToken);
        var payments = allForStats
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapSystemPayment)
            .ToList();

        return new SystemPaymentIndexViewModel
        {
            TenantId = tenantId,
            Status = status,
            Search = trimmedSearch,
            TotalPayments = payments.Count,
            PendingPayments = payments.Count(x => x.Status == PaymentStatuses.Pending),
            PaidPayments = payments.Count(x => x.Status == PaymentStatuses.Paid),
            FailedPayments = payments.Count(x => x.Status == PaymentStatuses.Failed),
            PaidAmount = payments.Where(x => x.Status == PaymentStatuses.Paid).Sum(x => x.Amount),
            Tenants = await GetTenantOptionsAsync(cancellationToken),
            Payments = payments
        };
    }

    public async Task<(bool Succeeded, string? Error)> MarkSystemPaymentPaidAsync(
        Guid id,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.SystemPayments
            .Include(x => x.Tenant)
            .Include(x => x.Subscription)
            .ThenInclude(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (payment is null)
        {
            return (false, "Payment not found.");
        }

        var oldValue = PaymentAuditValue(payment);
        payment.Status = PaymentStatuses.Paid;
        payment.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "MarkSystemPaymentPaid",
            nameof(SystemPayment),
            payment.Id.ToString(),
            oldValue,
            PaymentAuditValue(payment),
            payment.TenantId,
            cancellationToken: cancellationToken);
        await NotifySystemPaymentChangedAsync(payment, cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> MarkSystemPaymentFailedAsync(
        Guid id,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.SystemPayments
            .Include(x => x.Tenant)
            .Include(x => x.Subscription)
            .ThenInclude(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (payment is null)
        {
            return (false, "Payment not found.");
        }

        var oldValue = PaymentAuditValue(payment);
        payment.Status = PaymentStatuses.Failed;
        payment.PaidAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "MarkSystemPaymentFailed",
            nameof(SystemPayment),
            payment.Id.ToString(),
            oldValue,
            PaymentAuditValue(payment),
            payment.TenantId,
            cancellationToken: cancellationToken);
        await NotifySystemPaymentChangedAsync(payment, cancellationToken);

        return (true, null);
    }

    public async Task<OwnerSubscriptionIndexViewModel> GetOwnerSubscriptionAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var tenant = await _db.Tenants.AsNoTracking().FirstAsync(x => x.Id == tenantId, cancellationToken);

        var subscriptions = await _db.TenantSubscriptions
            .AsNoTracking()
            .Include(x => x.Plan)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var payments = await _db.SystemPayments
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Include(x => x.Subscription)
            .ThenInclude(x => x.Plan)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var current = subscriptions.FirstOrDefault(x =>
            (x.Status == SubscriptionStatuses.Active || x.Status == SubscriptionStatuses.Trial)
            && (!x.EndDate.HasValue || x.EndDate.Value >= today));

        return new OwnerSubscriptionIndexViewModel
        {
            TenantName = tenant.Name,
            CurrentSubscription = current is null ? null : MapOwnerSubscription(current),
            SubscriptionHistory = subscriptions.Select(MapOwnerSubscription).ToList(),
            PaymentHistory = payments.Select(MapSystemPayment).ToList()
        };
    }

    private async Task<string?> ValidatePlanAsync(SubscriptionPlanFormViewModel model, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return "Plan name is required.";
        }

        if (model.Price < 0)
        {
            return "Price must be greater than or equal to 0.";
        }

        if (!BillingCycles.All.Contains(model.BillingCycle))
        {
            return "Invalid billing cycle.";
        }

        if (model.MaxStores < 0 || model.MaxStaff < 0 || model.MaxProducts < 0)
        {
            return "Plan limits must be greater than or equal to 0.";
        }

        var normalizedName = model.Name.Trim();
        var exists = await _db.SubscriptionPlans.AnyAsync(
            x => x.Name == normalizedName && !x.IsDeleted && (!excludedId.HasValue || x.Id != excludedId.Value),
            cancellationToken);
        return exists ? "Plan name already exists." : null;
    }

    private async Task<IReadOnlyList<SubscriptionTenantOptionViewModel>> GetTenantOptionsAsync(CancellationToken cancellationToken)
    {
        return await _db.Tenants
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new SubscriptionTenantOptionViewModel
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SubscriptionPlanOptionViewModel>> GetPlanOptionsAsync(CancellationToken cancellationToken)
    {
        return await _db.SubscriptionPlans
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.Price)
            .Select(x => new SubscriptionPlanOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                BillingCycle = x.BillingCycle
            })
            .ToListAsync(cancellationToken);
    }

    private static SystemPaymentListItemViewModel MapSystemPayment(SystemPayment payment)
    {
        return new SystemPaymentListItemViewModel
        {
            Id = payment.Id,
            TenantId = payment.TenantId,
            TenantName = payment.Tenant.Name,
            SubscriptionId = payment.SubscriptionId,
            PlanName = payment.Subscription.Plan.Name,
            Amount = payment.Amount,
            Method = payment.Method,
            Status = payment.Status,
            PaidAt = payment.PaidAt,
            InvoiceUrl = payment.InvoiceUrl,
            CreatedAt = payment.CreatedAt
        };
    }

    private async Task NotifySystemPaymentChangedAsync(SystemPayment payment, CancellationToken cancellationToken)
    {
        await _realtimeNotifier.SystemPaymentChangedAsync(
            new SystemPaymentChangedEvent(
                payment.TenantId,
                payment.Id,
                payment.Tenant.Name,
                payment.Subscription.Plan.Name,
                payment.Amount,
                payment.Method,
                payment.Status,
                payment.PaidAt,
                DateTime.UtcNow),
            cancellationToken);
    }

    private static OwnerSubscriptionItemViewModel MapOwnerSubscription(TenantSubscription subscription)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new OwnerSubscriptionItemViewModel
        {
            Id = subscription.Id,
            PlanName = subscription.Plan.Name,
            Price = subscription.Plan.Price,
            BillingCycle = subscription.Plan.BillingCycle,
            MaxStores = subscription.Plan.MaxStores,
            MaxStaff = subscription.Plan.MaxStaff,
            MaxProducts = subscription.Plan.MaxProducts,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            Status = subscription.Status,
            AutoRenew = subscription.AutoRenew,
            IsExpired = subscription.EndDate.HasValue && subscription.EndDate.Value < today
        };
    }

    private static string PlanAuditValue(SubscriptionPlan plan)
        => $"Name={plan.Name}; Price={plan.Price:#,##0.##}; Cycle={plan.BillingCycle}; Active={plan.IsActive}; Stores={plan.MaxStores?.ToString() ?? "Unlimited"}; Staff={plan.MaxStaff?.ToString() ?? "Unlimited"}; Products={plan.MaxProducts?.ToString() ?? "Unlimited"}";

    private static string PaymentAuditValue(SystemPayment payment)
        => $"TenantId={payment.TenantId}; Amount={payment.Amount:#,##0.##}; Status={payment.Status}; PaidAt={payment.PaidAt?.ToString("O") ?? "-"}";

    private static bool PaymentMethodsAllowed(string method)
        => method is PaymentMethods.Cash or PaymentMethods.BankTransfer or PaymentMethods.Card or PaymentMethods.Momo or PaymentMethods.ZaloPay or PaymentMethods.Other;

    private Guid RequireTenantId()
    {
        if (!_currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException("Current user does not have a tenant.");
        }

        return _currentUser.TenantId.Value;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
