namespace ChainPOS.Realtime;

public static class RealtimeGroups
{
    public const string PlatformAdmins = "platform-admins";

    public static string Tenant(Guid tenantId) => $"tenant:{tenantId:N}";

    public static string Store(Guid tenantId, Guid storeId) => $"tenant:{tenantId:N}:store:{storeId:N}";
}
