using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Auth;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email owner.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    [Display(Name = "Email owner")]
    public string Email { get; set; } = string.Empty;
}
