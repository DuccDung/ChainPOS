using ChainPOS.ViewModels.Auth;

namespace ChainPOS.Services.Auth;

public interface IPasswordResetService
{
    Task<PasswordResetResult> RequestOwnerPasswordResetOtpAsync(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetOwnerPasswordWithOtpAsync(
        ResetPasswordWithOtpViewModel model,
        CancellationToken cancellationToken = default);
}
