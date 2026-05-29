using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Owner.Staff;

public sealed class StaffCreateViewModel
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public List<Guid> StoreIds { get; set; } = new();

    public IReadOnlyList<StaffStoreOptionViewModel> Stores { get; set; } = Array.Empty<StaffStoreOptionViewModel>();
}
