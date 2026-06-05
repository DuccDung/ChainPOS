using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Auth;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedAccessAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly StoreFlowDbContext _db;
    private readonly PasswordHasher<AspNetUser> _passwordHasher;
    private readonly IAuditLogService _auditLog;

    public AuthService(
        StoreFlowDbContext db,
        PasswordHasher<AspNetUser> passwordHasher,
        IAuditLogService auditLog)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _auditLog = auditLog;
    }

    public async Task<LoginResult> PasswordSignInAsync(LoginViewModel model, CancellationToken cancellationToken = default)
    {
        var normalizedLogin = Normalize(model.UserNameOrEmail);
        var user = await _db.AspNetUsers
            .Include(x => x.Roles)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(
                x => x.NormalizedEmail == normalizedLogin || x.NormalizedUserName == normalizedLogin,
                cancellationToken);

        if (user is null)
        {
            return LoginResult.Failed("Email/tên đăng nhập hoặc mật khẩu không đúng.");
        }

        if (!IsActive(user.Status))
        {
            return LoginResult.Failed("Tài khoản hiện không được phép đăng nhập.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return LoginResult.Failed("Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return LoginResult.Failed("Tài khoản chưa có mật khẩu hợp lệ.");
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            await HandleFailedPasswordAsync(user, cancellationToken);
            return LoginResult.Failed("Email/tên đăng nhập hoặc mật khẩu không đúng.");
        }

        var roleNames = user.Roles
            .Select(x => x.Name ?? x.NormalizedName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roleNames.Length == 0)
        {
            return LoginResult.Failed("Tài khoản chưa được gán vai trò.");
        }

        var primaryRole = GetPrimaryRole(roleNames);
        if (primaryRole is null)
        {
            return LoginResult.Failed("Vai trò tài khoản không hợp lệ.");
        }

        if (!string.Equals(primaryRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            if (!user.TenantId.HasValue || user.Tenant is null)
            {
                return LoginResult.Failed("Tài khoản chưa được gán tenant.");
            }

            if (IsBlockedTenant(user.Tenant.Status))
            {
                return LoginResult.Failed("Tenant đang bị tạm khóa hoặc đã hủy.");
            }
        }

        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogForUserAsync(
            "Login",
            user.Id,
            nameof(AspNetUser),
            user.Id,
            newValue: $"Email={user.Email}; Role={primaryRole}",
            tenantId: user.TenantId,
            cancellationToken: cancellationToken);

        var principal = BuildPrincipal(user, roleNames);
        return LoginResult.Success(principal, primaryRole);
    }

    private async Task HandleFailedPasswordAsync(AspNetUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount += 1;
        if (user.LockoutEnabled && user.AccessFailedCount >= MaxFailedAccessAttempts)
        {
            user.LockoutEnd = DateTimeOffset.UtcNow.Add(LockoutDuration);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ClaimsPrincipal BuildPrincipal(AspNetUser user, IEnumerable<string> roleNames)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            claims.Add(new Claim(AppClaimTypes.FullName, user.FullName));
        }

        if (!string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            claims.Add(new Claim(AppClaimTypes.SecurityStamp, user.SecurityStamp));
        }

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim(AppClaimTypes.TenantId, user.TenantId.Value.ToString()));
        }

        claims.AddRange(roleNames.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static string? GetPrimaryRole(IEnumerable<string> roleNames)
    {
        if (roleNames.Any(x => string.Equals(x, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            return AppRoles.Admin;
        }

        if (roleNames.Any(x => string.Equals(x, AppRoles.Owner, StringComparison.OrdinalIgnoreCase)))
        {
            return AppRoles.Owner;
        }

        if (roleNames.Any(x => string.Equals(x, AppRoles.Staff, StringComparison.OrdinalIgnoreCase)))
        {
            return AppRoles.Staff;
        }

        return null;
    }

    private static bool IsActive(string? status)
        => string.Equals(status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockedTenant(string? status)
        => string.Equals(status, TenantStatuses.Suspended, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, TenantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
        => value.Trim().ToUpperInvariant();
}
