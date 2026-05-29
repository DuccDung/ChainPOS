using ChainPOS.Services.Common;

namespace ChainPOS.Tests.TestSupport;

internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public bool IsAuthenticated { get; init; } = true;

    public string? UserId { get; init; }

    public Guid? TenantId { get; init; }

    public string? FullName { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
