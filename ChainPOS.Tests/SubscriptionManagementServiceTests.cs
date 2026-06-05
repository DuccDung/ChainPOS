using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Subscriptions;
using ChainPOS.Tests.TestSupport;
using ChainPOS.ViewModels.Admin.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChainPOS.Tests;

public sealed class SubscriptionManagementServiceTests
{
    [Fact]
    public async Task Create_subscription_expires_existing_same_day_subscription_without_invalid_date_range()
    {
        await using var db = TestDb.Create();
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.Today);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Billing Tenant",
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = "Business",
            Price = 100m,
            BillingCycle = BillingCycles.Monthly,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        var existingSubscription = new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            StartDate = startDate,
            EndDate = startDate.AddMonths(1).AddDays(-1),
            Status = SubscriptionStatuses.Active,
            AutoRenew = true,
            CreatedAt = DateTime.UtcNow
        };
        db.TenantSubscriptions.Add(existingSubscription);
        await db.SaveChangesAsync();

        var service = new SubscriptionManagementService(
            db,
            new FakeAuditLogService(),
            new FakeCurrentUserService { Roles = new[] { AppRoles.Admin } },
            new FakeRealtimeNotifier());

        var result = await service.CreateTenantSubscriptionAsync(
            new TenantSubscriptionCreateViewModel
            {
                TenantId = tenantId,
                PlanId = planId,
                StartDate = startDate,
                EndDate = startDate.AddMonths(1).AddDays(-1),
                Status = SubscriptionStatuses.Active,
                AutoRenew = true,
                CreatePendingPayment = true,
                PaymentMethod = PaymentMethods.SePay
            },
            currentUserId: "admin-test");

        Assert.True(result.Succeeded, result.Error);

        var expiredSubscription = await db.TenantSubscriptions.SingleAsync(x => x.Id == existingSubscription.Id);
        Assert.Equal(SubscriptionStatuses.Expired, expiredSubscription.Status);
        Assert.Equal(expiredSubscription.StartDate, expiredSubscription.EndDate);
        Assert.True(expiredSubscription.EndDate >= expiredSubscription.StartDate);
        Assert.NotNull(result.PaymentId);
    }
}
