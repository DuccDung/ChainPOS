using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(StoreFlowDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardViewModel> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var ownerCount = await _db.AspNetUsers.CountAsync(
            x => x.Roles.Any(r => r.Id == AppRoles.Owner),
            cancellationToken);
        var tenantCount = await _db.Tenants.CountAsync(x => !x.IsDeleted, cancellationToken);
        var storeCount = await _db.Stores.CountAsync(x => !x.IsDeleted, cancellationToken);
        var revenue = await _db.SystemPayments
            .Where(x => x.Status == PaymentStatuses.Paid)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        return new DashboardViewModel
        {
            RoleName = "Super Admin",
            DisplayName = _currentUser.FullName ?? "Admin User",
            Subtitle = "Platform overview",
            WelcomeTitle = "Admin User",
            WelcomeDescription = "Here's what's happening in your SaaS platform today.",
            PrimaryActionText = "View Owners",
            PrimaryActionUrl = "/admin/owners",
            SecondaryActionText = "View Tenants",
            SecondaryActionUrl = "/admin/tenants",
            Metrics = new[]
            {
                Metric("total-owners", "Total Owners", ownerCount.ToString("N0"), "+ active", "Owner accounts", "orange", "users"),
                Metric("total-tenants", "Total Tenants", tenantCount.ToString("N0"), "live", "Active platform tenants", "amber", "building"),
                Metric("total-stores", "Total Stores", storeCount.ToString("N0"), "stores", "Stores across tenants", "yellow", "box"),
                Metric("saas-revenue", "SaaS Revenue", FormatCurrency(revenue), "paid", "Collected system payments", "emerald", "trend")
            },
            Activities = new[]
            {
                Activity("Owner management ready", "Create, lock and unlock owner accounts.", "Now", "orange"),
                Activity("Tenant control ready", "Suspend, activate or cancel tenant access.", "Now", "amber"),
                Activity("Audit logging enabled", "Important admin actions are recorded.", "Now", "emerald")
            }
        };
    }

    public async Task<DashboardViewModel> GetOwnerDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue)
        {
            return Empty("Owner", "Owner Dashboard", "Tenant is not assigned.");
        }

        var today = DateTime.Today;
        var storeCount = await _db.Stores.CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        var staffCount = await _db.AspNetUsers.CountAsync(
            x => x.TenantId == tenantId && x.Roles.Any(r => r.Id == AppRoles.Staff),
            cancellationToken);
        var productCount = await _db.Products.CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        var todayRevenue = await _db.Orders
            .Where(x => x.TenantId == tenantId && x.CreatedAt >= today && x.OrderStatus != OrderStatuses.Cancelled)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        var todayOrderCount = await _db.Orders.CountAsync(
            x => x.TenantId == tenantId && x.CreatedAt >= today && x.OrderStatus != OrderStatuses.Cancelled,
            cancellationToken);
        var lowStockCount = await _db.Inventories.CountAsync(
            x => x.TenantId == tenantId && x.Quantity > 0 && x.Quantity <= x.MinQuantity,
            cancellationToken);
        var recentOrders = await _db.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => new DashboardRecentOrderViewModel
            {
                Id = x.Id,
                OrderCode = x.OrderCode,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                StaffName = x.StaffUser != null ? x.StaffUser.FullName : null,
                TotalAmount = x.TotalAmount,
                OrderStatus = x.OrderStatus,
                PaymentStatus = x.PaymentStatus,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new DashboardViewModel
        {
            RoleName = "Owner",
            DisplayName = _currentUser.FullName ?? "Owner User",
            Subtitle = "Tenant dashboard",
            WelcomeTitle = _currentUser.FullName ?? "Owner User",
            WelcomeDescription = "Here's what's happening in your stores today.",
            PrimaryActionText = "View Stores",
            PrimaryActionUrl = "/owner/stores",
            SecondaryActionText = "View Staff",
            SecondaryActionUrl = "/owner/staff",
            Metrics = new[]
            {
                Metric("stores", "Stores", storeCount.ToString("N0"), "active", "Stores in your tenant", "orange", "building"),
                Metric("orders-today", "Orders Today", todayOrderCount.ToString("N0"), "today", "Completed orders", "amber", "order"),
                Metric("low-stock", "Low Stock", lowStockCount.ToString("N0"), "watch", "Items at or below minimum", "yellow", "box"),
                Metric("revenue-today", "Revenue Today", FormatCurrency(todayRevenue), "today", "Completed sales", "emerald", "trend")
            },
            Activities = new[]
            {
                Activity("Low stock watchlist", $"{lowStockCount:N0} inventory item(s) need attention.", "Live", lowStockCount > 0 ? "amber" : "emerald"),
                Activity("Recent orders loaded", $"{recentOrders.Count:N0} latest order(s) are available below.", "Now", "orange"),
                Activity("Tenant isolation active", "Owner data is scoped by tenant for store, stock and sales.", "Now", "emerald")
            },
            RecentOrders = recentOrders
        };
    }

    public async Task<DashboardViewModel> GetStaffDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;
        if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
        {
            return Empty("Staff", "Staff Dashboard", "Store access is not assigned.");
        }

        var today = DateTime.Today;
        var storeCount = await _db.UserStores.CountAsync(
            x => x.TenantId == tenantId && x.UserId == userId && x.IsActive,
            cancellationToken);
        var openShiftCount = await _db.Shifts.CountAsync(
            x => x.TenantId == tenantId && x.OpenedBy == userId && x.Status == ShiftStatuses.Open,
            cancellationToken);
        var orderCount = await _db.Orders.CountAsync(
            x => x.TenantId == tenantId && x.StaffUserId == userId && x.CreatedAt >= today,
            cancellationToken);
        var todayRevenue = await _db.Orders
            .Where(x => x.TenantId == tenantId
                && x.StaffUserId == userId
                && x.CreatedAt >= today
                && x.OrderStatus != OrderStatuses.Cancelled)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        return new DashboardViewModel
        {
            RoleName = "Staff",
            DisplayName = _currentUser.FullName ?? "Staff User",
            Subtitle = "Store operations",
            WelcomeTitle = _currentUser.FullName ?? "Staff User",
            WelcomeDescription = "Your assigned stores, shifts and sales activity.",
            PrimaryActionText = "Open POS",
            PrimaryActionUrl = "/staff/pos",
            SecondaryActionText = "Inventory",
            SecondaryActionUrl = "/staff/inventory",
            Metrics = new[]
            {
                Metric("assigned-stores", "Assigned Stores", storeCount.ToString("N0"), "stores", "Active store assignments", "orange", "building"),
                Metric("open-shifts", "Open Shifts", openShiftCount.ToString("N0"), "current", "Your open shifts", "amber", "clock"),
                Metric("orders-today", "Orders Today", orderCount.ToString("N0"), "today", "Orders created by you", "yellow", "order"),
                Metric("revenue-today", "Sales Today", FormatCurrency(todayRevenue), "today", "Your sales total", "emerald", "trend")
            },
            Activities = new[]
            {
                Activity("Store access checked", "Staff operations are scoped by assigned stores.", "Now", "orange"),
                Activity("POS module next", "Checkout flow will be implemented after inventory.", "Next", "amber"),
                Activity("Inventory access ready", "Stock views will use the store access service.", "Next", "emerald")
            }
        };
    }

    private static DashboardViewModel Empty(string role, string displayName, string description) => new()
    {
        RoleName = role,
        DisplayName = displayName,
        Subtitle = description,
        WelcomeTitle = displayName,
        WelcomeDescription = description,
        Metrics = Array.Empty<DashboardMetricViewModel>(),
        Activities = Array.Empty<DashboardActivityViewModel>()
    };

    private static DashboardMetricViewModel Metric(
        string key,
        string label,
        string value,
        string badge,
        string note,
        string tone,
        string icon)
        => new()
        {
            Key = key,
            Label = label,
            Value = value,
            Badge = badge,
            Note = note,
            Tone = tone,
            Icon = icon
        };

    private static DashboardActivityViewModel Activity(string title, string description, string timeText, string tone)
        => new()
        {
            Title = title,
            Description = description,
            TimeText = timeText,
            Tone = tone
        };

    private static string FormatCurrency(decimal value) => value.ToString("$#,##0.##");
}
