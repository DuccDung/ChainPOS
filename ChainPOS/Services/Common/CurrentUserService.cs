using System.Security.Claims;
using ChainPOS.Constants;

namespace ChainPOS.Services.Common;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public Guid? TenantId
    {
        get
        {
            var value = User?.FindFirstValue(AppClaimTypes.TenantId);
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }

    public string? FullName => User?.FindFirstValue(AppClaimTypes.FullName);

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray() ?? Array.Empty<string>();

    public bool IsInRole(string role) => User?.IsInRole(role) == true;
}
