using ChainPOS.Constants;
using ChainPOS.Filters;
using ChainPOS.Models;
using ChainPOS.Realtime;
using ChainPOS.Services.Admin;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Auth;
using ChainPOS.Services.Common;
using ChainPOS.Services.Dashboard;
using ChainPOS.Services.Inventory;
using ChainPOS.Services.Owner;
using ChainPOS.Services.Reports;
using ChainPOS.Services.Realtime;
using ChainPOS.Services.Sales;
using ChainPOS.Services.Security;
using ChainPOS.Services.Seed;
using ChainPOS.Services.Subscriptions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set it via User Secrets, environment variables, or appsettings.Local.json.");
}

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<RequireTenantFilter>();
});
builder.Services.AddSignalR();
builder.Services.AddDbContext<StoreFlowDbContext>(options =>
    options.UseSqlServer(defaultConnectionString));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PasswordHasher<AspNetUser>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<RefreshUserClaimsCookieEvents>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IStoreAccessService, StoreAccessService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAdminManagementService, AdminManagementService>();
builder.Services.AddScoped<IAdminSettingsService, AdminSettingsService>();
builder.Services.AddScoped<IOwnerStoreService, OwnerStoreService>();
builder.Services.AddScoped<IOwnerStaffService, OwnerStaffService>();
builder.Services.AddScoped<IOwnerCategoryService, OwnerCategoryService>();
builder.Services.AddScoped<IOwnerProductService, OwnerProductService>();
builder.Services.AddScoped<IOwnerStoreProductService, OwnerStoreProductService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IPosService, PosService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ISubscriptionManagementService, SubscriptionManagementService>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.Name = "ChainPOS.Auth";
        options.EventsType = typeof(RefreshUserClaimsCookieEvents);
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole(AppRoles.Admin));
    options.AddPolicy("RequireOwner", policy => policy.RequireRole(AppRoles.Owner));
    options.AddPolicy("RequireStaff", policy => policy.RequireRole(AppRoles.Staff));
    options.AddPolicy("RequireOwnerOrStaff", policy => policy.RequireRole(AppRoles.Owner, AppRoles.Staff));
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentDataSeeder");
    try
    {
        await DevelopmentDataSeeder.SeedAsync(
            scope.ServiceProvider.GetRequiredService<StoreFlowDbContext>(),
            app.Configuration,
            scope.ServiceProvider.GetRequiredService<PasswordHasher<AspNetUser>>());
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not seed development authentication data.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHub<ChainPosHub>("/hubs/chainpos");

app.MapGet("/", () => Results.Redirect("/login"));

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();


app.Run();
