using System.Security.Claims;

namespace ChainPOS.Services.Auth;

public sealed class LoginResult
{
    private LoginResult(bool succeeded, ClaimsPrincipal? principal, string? primaryRole, string? errorMessage)
    {
        Succeeded = succeeded;
        Principal = principal;
        PrimaryRole = primaryRole;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public ClaimsPrincipal? Principal { get; }

    public string? PrimaryRole { get; }

    public string? ErrorMessage { get; }

    public static LoginResult Success(ClaimsPrincipal principal, string primaryRole)
        => new(true, principal, primaryRole, null);

    public static LoginResult Failed(string errorMessage)
        => new(false, null, null, errorMessage);
}
