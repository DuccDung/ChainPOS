using ChainPOS.ViewModels.Auth;

namespace ChainPOS.Services.Auth;

public interface IAuthService
{
    Task<LoginResult> PasswordSignInAsync(LoginViewModel model, CancellationToken cancellationToken = default);
}
