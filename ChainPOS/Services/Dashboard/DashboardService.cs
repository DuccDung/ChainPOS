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
                Metric("Total Owners", ownerCount.ToString("N0"), "+ active", "Owner accounts", "orange", "users"),
                Metric("Total Tenants", tenantCount.ToString("N0"), "live", "Active platform tenants", "amber", "building"),
                Metric("Total Stores", storeCount.ToString("N0"), "stores", "Stores across tenants", "yellow", "box"),
                Metric("SaaS Revenue", FormatCurrency(revenue), "paid", "Collected system payments", "emerald", "trend")
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
                Metric("Stores", storeCount.ToString("N0"), "active", "Stores in your tenant", "orange", "building"),
                Metric("Staff", staffCount.ToString("N0"), "team", "Staff accounts", "amber", "users"),
                Metric("Products", productCount.ToString("N0"), "items", "Product catalog", "yellow", "box"),
                Metric("Revenue Today", FormatCurrency(todayRevenue), "today", "Completed sales", "emerald", "trend")
            },
            Activities = new[]
            {
                Activity("Store access protected", "Owner data is scoped by tenant.", "Now", "orange"),
                Activity("Staff assignment active", "Staff access will use UserStores.", "Now", "amber"),
                Activity("Inventory and POS next", "Product, inventory and POS modules are next in roadmap.", "Next", "emerald")
            }
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
                Metric("Assigned Stores", storeCount.ToString("N0"), "stores", "Active store assignments", "orange", "building"),
                Metric("Open Shifts", openShiftCount.ToString("N0"), "current", "Your open shifts", "amber", "clock"),
                Metric("Orders Today", orderCount.ToString("N0"), "today", "Orders created by you", "yellow", "order"),
                Metric("Sales Today", FormatCurrency(todayRevenue), "today", "Your sales total", "emerald", "trend")
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
        string label,
        string value,
        string badge,
        string note,
        string tone,
        string icon)
        => new()
        {
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
