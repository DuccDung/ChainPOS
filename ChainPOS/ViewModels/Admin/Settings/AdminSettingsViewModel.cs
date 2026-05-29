using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Admin.Settings;

public sealed class AdminSettingsViewModel
{
    [Required]
    [StringLength(100)]
    public string SystemName { get; set; } = "ChainPOS";

    [EmailAddress]
    [StringLength(256)]
    public string SupportEmail { get; set; } = "support@chainpos.local";

    [StringLength(50)]
    public string SupportPhone { get; set; } = string.Empty;

    [Url]
    [StringLength(300)]
    public string PlatformUrl { get; set; } = "https://localhost:7219";

    [Required]
    [StringLength(80)]
    public string DefaultTimezone { get; set; } = "Asia/Bangkok";

    [Required]
    [StringLength(10)]
    public string DefaultCurrency { get; set; } = "VND";

    [Required]
    [StringLength(30)]
    public string DateFormat { get; set; } = "dd/MM/yyyy";

    public bool MaintenanceMode { get; set; }

    public bool EnableDebugLogging { get; set; }

    public bool RequireStrongPassword { get; set; } = true;

    public bool EnableAdminTwoFactor { get; set; }

    [Range(1, 20)]
    public int MaxLoginAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 30;

    [Range(15, 1440)]
    public int SessionTimeoutMinutes { get; set; } = 480;

    [Range(0, 365)]
    public int TrialDays { get; set; } = 14;

    [Range(0, 90)]
    public int RenewalReminderDays { get; set; } = 7;

    [Range(0, 90)]
    public int AllowExpiredTenantGraceDays { get; set; } = 3;

    [Range(0, 100)]
    public decimal BillingTaxRate { get; set; } = 0m;

    [Required]
    [StringLength(20)]
    public string InvoicePrefix { get; set; } = "INV";

    [Required]
    [StringLength(20)]
    public string OrderPrefix { get; set; } = "POS";

    [Range(0, 100)]
    public decimal PosTaxRate { get; set; } = 0m;

    public bool AllowNegativeStock { get; set; }

    [Range(0, 365)]
    public int AllowCancelAfterDays { get; set; } = 7;

    public bool EnableRealtimeNotifications { get; set; } = true;

    public bool EnableLowStockAlerts { get; set; } = true;

    public bool EnableSubscriptionExpiryAlerts { get; set; } = true;

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
