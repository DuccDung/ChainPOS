namespace ChainPOS.Constants;

public static class BillingCycles
{
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string Yearly = "Yearly";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Monthly,
        Quarterly,
        Yearly
    };
}
