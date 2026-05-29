namespace ChainPOS.Constants;

public static class SubscriptionStatuses
{
    public const string Active = "Active";
    public const string Trial = "Trial";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public const string Suspended = "Suspended";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Active,
        Trial,
        Expired,
        Cancelled,
        Suspended
    };
}
