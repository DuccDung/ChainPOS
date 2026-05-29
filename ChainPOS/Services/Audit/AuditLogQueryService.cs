using ChainPOS.Models;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Audit;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Audit;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditLogQueryService(StoreFlowDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AuditLogIndexViewModel> GetAuditLogsAsync(
        string areaName,
        AuditLogFilterViewModel? filter,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(areaName, "Admin", StringComparison.OrdinalIgnoreCase);
        var effectiveFilter = NormalizeFilter(isAdmin, filter);
        var query = BuildFilteredQuery(isAdmin, effectiveFilter);

        var totalEvents = await query.CountAsync(cancellationToken);
        var actionsForSeverity = await query.Select(x => x.Action).ToListAsync(cancellationToken);
        var distinctUsers = await query
            .Where(x => x.UserId != null)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalEvents / (double)effectiveFilter.PageSize));
        if (effectiveFilter.Page > totalPages)
        {
            effectiveFilter.Page = totalPages;
        }

        var skip = (effectiveFilter.Page - 1) * effectiveFilter.PageSize;
        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(effectiveFilter.PageSize)
            .ToListAsync(cancellationToken);

        return new AuditLogIndexViewModel
        {
            AreaName = areaName,
            IsAdmin = isAdmin,
            Filter = effectiveFilter,
            Tenants = await GetTenantOptionsAsync(isAdmin, cancellationToken),
            Stores = await GetStoreOptionsAsync(isAdmin, effectiveFilter, cancellationToken),
            Users = await GetUserOptionsAsync(isAdmin, effectiveFilter, cancellationToken),
            Actions = await GetActionOptionsAsync(isAdmin, effectiveFilter, cancellationToken),
            Logs = logs.Select(MapLog).ToList(),
            TotalEvents = totalEvents,
            DistinctUsers = distinctUsers,
            WarningEvents = actionsForSeverity.Count(IsWarningAction),
            CriticalEvents = actionsForSeverity.Count(IsCriticalAction),
            Page = effectiveFilter.Page,
            PageSize = effectiveFilter.PageSize,
            TotalPages = totalPages
        };
    }

    private AuditLogFilterViewModel NormalizeFilter(bool isAdmin, AuditLogFilterViewModel? filter)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromDate = filter?.FromDate ?? today.AddDays(-6);
        var toDate = filter?.ToDate ?? today;
        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var pageSize = filter is not null && filter.PageSize > 0 ? filter.PageSize : DefaultPageSize;
        pageSize = Math.Clamp(pageSize, 10, MaxPageSize);

        return new AuditLogFilterViewModel
        {
            TenantId = isAdmin ? filter?.TenantId : RequireTenantId(),
            StoreId = filter?.StoreId,
            UserId = string.IsNullOrWhiteSpace(filter?.UserId) ? null : filter.UserId.Trim(),
            Action = string.IsNullOrWhiteSpace(filter?.Action) ? null : filter.Action.Trim(),
            Search = string.IsNullOrWhiteSpace(filter?.Search) ? null : filter.Search.Trim(),
            FromDate = fromDate,
            ToDate = toDate,
            Page = Math.Max(1, filter?.Page ?? 1),
            PageSize = pageSize
        };
    }

    private IQueryable<AuditLog> BuildFilteredQuery(bool isAdmin, AuditLogFilterViewModel filter)
    {
        IQueryable<AuditLog> query = BuildAccessQuery(isAdmin, filter.TenantId)
            .Include(x => x.Tenant)
            .Include(x => x.Store)
            .Include(x => x.User);

        if (filter.StoreId.HasValue)
        {
            query = query.Where(x => x.StoreId == filter.StoreId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            query = query.Where(x => x.UserId == filter.UserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(x => x.Action == filter.Action);
        }

        if (filter.FromDate.HasValue)
        {
            var from = filter.FromDate.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (filter.ToDate.HasValue)
        {
            var toExclusive = filter.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            query = query.Where(x => x.CreatedAt < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search!;
            query = query.Where(x =>
                x.Action.Contains(search)
                || (x.EntityName != null && x.EntityName.Contains(search))
                || (x.EntityId != null && x.EntityId.Contains(search))
                || (x.OldValue != null && x.OldValue.Contains(search))
                || (x.NewValue != null && x.NewValue.Contains(search))
                || (x.IpAddress != null && x.IpAddress.Contains(search))
                || (x.Tenant != null && x.Tenant.Name.Contains(search))
                || (x.Store != null && (x.Store.Name.Contains(search) || x.Store.Code.Contains(search)))
                || (x.User != null && ((x.User.FullName != null && x.User.FullName.Contains(search))
                    || (x.User.Email != null && x.User.Email.Contains(search)))));
        }

        return query;
    }

    private IQueryable<AuditLog> BuildAccessQuery(bool isAdmin, Guid? tenantId)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (isAdmin)
        {
            if (tenantId.HasValue)
            {
                query = query.Where(x => x.TenantId == tenantId.Value);
            }

            return query;
        }

        var currentTenantId = RequireTenantId();
        return query.Where(x => x.TenantId == currentTenantId);
    }

    private async Task<IReadOnlyList<AuditTenantOptionViewModel>> GetTenantOptionsAsync(
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (!isAdmin)
        {
            var tenantId = RequireTenantId();
            return await _db.Tenants
                .AsNoTracking()
                .Where(x => x.Id == tenantId && !x.IsDeleted)
                .Select(x => new AuditTenantOptionViewModel
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync(cancellationToken);
        }

        return await _db.Tenants
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new AuditTenantOptionViewModel
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AuditStoreOptionViewModel>> GetStoreOptionsAsync(
        bool isAdmin,
        AuditLogFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var query = _db.Stores
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (isAdmin)
        {
            if (filter.TenantId.HasValue)
            {
                query = query.Where(x => x.TenantId == filter.TenantId.Value);
            }
        }
        else
        {
            query = query.Where(x => x.TenantId == filter.TenantId);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new AuditStoreOptionViewModel
            {
                Id = x.Id,
                TenantId = x.TenantId,
                Name = x.Name,
                Code = x.Code
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AuditUserOptionViewModel>> GetUserOptionsAsync(
        bool isAdmin,
        AuditLogFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var accessQuery = BuildAccessQuery(isAdmin, filter.TenantId)
            .Where(x => x.UserId != null)
            .Select(x => x.UserId!)
            .Distinct();

        return await _db.AspNetUsers
            .AsNoTracking()
            .Where(x => accessQuery.Contains(x.Id))
            .OrderBy(x => x.FullName ?? x.Email ?? x.UserName ?? x.Id)
            .Select(x => new AuditUserOptionViewModel
            {
                Id = x.Id,
                DisplayName = x.FullName ?? x.Email ?? x.UserName ?? x.Id,
                Email = x.Email
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetActionOptionsAsync(
        bool isAdmin,
        AuditLogFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        return await BuildAccessQuery(isAdmin, filter.TenantId)
            .Select(x => x.Action)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    private static AuditLogListItemViewModel MapLog(AuditLog log)
    {
        var severity = GetSeverity(log.Action);
        return new AuditLogListItemViewModel
        {
            Id = log.Id,
            TenantId = log.TenantId,
            TenantName = log.Tenant?.Name ?? "System",
            StoreId = log.StoreId,
            StoreName = log.Store?.Name,
            StoreCode = log.Store?.Code,
            UserId = log.UserId,
            UserName = log.User?.FullName ?? log.User?.Email ?? log.User?.UserName ?? "System",
            UserEmail = log.User?.Email,
            Action = log.Action,
            ActionGroup = GetActionGroup(log.Action),
            Module = GetModule(log.Action, log.EntityName),
            Severity = severity,
            SeverityLabel = severity switch
            {
                "critical" => "Critical",
                "warning" => "Warning",
                _ => "Info"
            },
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            Description = BuildDescription(log),
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            CreatedAt = log.CreatedAt
        };
    }

    private static string BuildDescription(AuditLog log)
    {
        if (!string.IsNullOrWhiteSpace(log.NewValue))
        {
            return log.NewValue;
        }

        if (!string.IsNullOrWhiteSpace(log.OldValue))
        {
            return log.OldValue;
        }

        if (!string.IsNullOrWhiteSpace(log.EntityName) || !string.IsNullOrWhiteSpace(log.EntityId))
        {
            return $"{log.EntityName ?? "Entity"} #{log.EntityId ?? "-"}";
        }

        return "No detail recorded.";
    }

    private static string GetActionGroup(string action)
    {
        if (StartsWithAny(action, "Create", "Assign", "Import", "Open", "Login"))
        {
            return "CREATE";
        }

        if (StartsWithAny(action, "Update", "Activate", "Deactivate", "Enable", "Disable", "Lock", "Unlock", "Reset", "Close", "Suspend"))
        {
            return "UPDATE";
        }

        if (StartsWithAny(action, "Delete", "Cancel"))
        {
            return "DELETE";
        }

        if (StartsWithAny(action, "Export", "Logout"))
        {
            return "EXPORT";
        }

        return "SYSTEM";
    }

    private static string GetModule(string action, string? entityName)
    {
        var value = $"{action} {entityName}".Trim();
        if (ContainsAny(value, "Inventory", "Stock"))
        {
            return "Inventory";
        }

        if (ContainsAny(value, "Order", "Payment", "Shift", "POS"))
        {
            return "Sales";
        }

        if (ContainsAny(value, "Product", "Category", "StoreProduct"))
        {
            return "Catalog";
        }

        if (ContainsAny(value, "Tenant", "Subscription", "SystemPayment", "Plan"))
        {
            return "Platform";
        }

        if (ContainsAny(value, "User", "Staff", "AspNetUser", "Login", "Logout"))
        {
            return "User";
        }

        if (ContainsAny(value, "Store"))
        {
            return "Store";
        }

        return "System";
    }

    private static string GetSeverity(string action)
    {
        if (IsCriticalAction(action))
        {
            return "critical";
        }

        if (IsWarningAction(action))
        {
            return "warning";
        }

        return "info";
    }

    private static bool IsWarningAction(string action)
        => ContainsAny(action, "Delete", "Cancel", "Lock", "Suspend", "Disable", "Failed");

    private static bool IsCriticalAction(string action)
        => ContainsAny(action, "CancelTenant", "DeleteStore");

    private static bool StartsWithAny(string value, params string[] prefixes)
        => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private Guid RequireTenantId()
    {
        if (!_currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException("Current user does not have a tenant.");
        }

        return _currentUser.TenantId.Value;
    }
}
