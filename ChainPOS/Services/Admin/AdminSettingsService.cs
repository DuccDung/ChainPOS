using System.Text.Json;
using ChainPOS.Services.Audit;
using ChainPOS.ViewModels.Admin.Settings;

namespace ChainPOS.Services.Admin;

public sealed class AdminSettingsService : IAdminSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly IAuditLogService _auditLog;

    public AdminSettingsService(IWebHostEnvironment environment, IAuditLogService auditLog)
    {
        _settingsPath = Path.Combine(environment.ContentRootPath, "App_Data", "settings", "admin-settings.json");
        _auditLog = auditLog;
    }

    public async Task<AdminSettingsViewModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AdminSettingsViewModel();
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AdminSettingsViewModel>(stream, JsonOptions, cancellationToken);
        return settings ?? new AdminSettingsViewModel();
    }

    public async Task SaveSettingsAsync(
        AdminSettingsViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var oldSettings = await GetSettingsAsync(cancellationToken);
        model.UpdatedAt = DateTime.UtcNow;
        model.UpdatedBy = currentUserId;

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var stream = File.Create(_settingsPath))
        {
            await JsonSerializer.SerializeAsync(stream, model, JsonOptions, cancellationToken);
        }

        await _auditLog.LogAsync(
            "UpdateSystemSettings",
            "AdminSettings",
            "platform",
            oldValue: JsonSerializer.Serialize(Snapshot(oldSettings), JsonOptions),
            newValue: JsonSerializer.Serialize(Snapshot(model), JsonOptions),
            cancellationToken: cancellationToken);
    }

    private static object Snapshot(AdminSettingsViewModel settings) => new
    {
        settings.SystemName,
        settings.DefaultTimezone,
        settings.DefaultCurrency,
        settings.MaintenanceMode,
        settings.RequireStrongPassword,
        settings.MaxLoginAttempts,
        settings.TrialDays,
        settings.RenewalReminderDays,
        settings.OrderPrefix,
        settings.InvoicePrefix,
        settings.EnableRealtimeNotifications
    };
}
