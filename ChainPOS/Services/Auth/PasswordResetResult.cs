namespace ChainPOS.Services.Auth;

public sealed class PasswordResetResult
{
    private PasswordResetResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    public bool Succeeded { get; }

    public string Message { get; }

    public static PasswordResetResult Success(string message) => new(true, message);

    public static PasswordResetResult Failed(string message) => new(false, message);
}
