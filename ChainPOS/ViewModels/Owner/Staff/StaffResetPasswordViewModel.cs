using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Owner.Staff;

public sealed class StaffResetPasswordViewModel
{
    public string StaffId { get; set; } = string.Empty;

    public string StaffName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
