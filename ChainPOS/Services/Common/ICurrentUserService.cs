namespace ChainPOS.Services.Common;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    Guid? TenantId { get; }

    string? FullName { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}
