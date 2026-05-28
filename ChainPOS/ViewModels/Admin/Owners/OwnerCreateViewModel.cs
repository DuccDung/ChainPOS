using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Admin.Owners;

public sealed class OwnerCreateViewModel
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Owner name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Owner phone")]
    public string? PhoneNumber { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Tenant name")]
    public string TenantName { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Tax code")]
    public string? TaxCode { get; set; }

    [StringLength(500)]
    [Display(Name = "Tenant address")]
    public string? TenantAddress { get; set; }

    [StringLength(50)]
    [Display(Name = "Tenant phone")]
    public string? TenantPhone { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
