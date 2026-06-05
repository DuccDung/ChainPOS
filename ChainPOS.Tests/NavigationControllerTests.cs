using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Controllers;
using ChainPOS.Services.Auth;
using ChainPOS.Tests.TestSupport;
using ChainPOS.ViewModels.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChainPOS.Tests;

public sealed class NavigationControllerTests
{
    [Fact]
    public void Home_index_redirects_to_login()
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance);

        var result = Assert.IsType<LocalRedirectResult>(controller.Index());

        Assert.Equal("/login", result.Url);
    }

    [Theory]
    [InlineData(AppRoles.Admin, "/admin/dashboard")]
    [InlineData(AppRoles.Owner, "/owner/dashboard")]
    [InlineData(AppRoles.Staff, "/staff/dashboard")]
    public void Authenticated_login_get_redirects_to_role_dashboard(string role, string expectedUrl)
    {
        var controller = CreateAccountController(CreatePrincipal(role));

        var result = Assert.IsType<LocalRedirectResult>(controller.Login());

        Assert.Equal(expectedUrl, result.Url);
    }

    [Fact]
    public void Anonymous_login_get_renders_login_view_with_return_url()
    {
        var controller = CreateAccountController(new ClaimsPrincipal(new ClaimsIdentity()));

        var result = Assert.IsType<ViewResult>(controller.Login("/owner/dashboard"));
        var model = Assert.IsType<LoginViewModel>(result.Model);

        Assert.Equal("/owner/dashboard", model.ReturnUrl);
    }

    private static AccountController CreateAccountController(ClaimsPrincipal user)
    {
        var controller = new AccountController(
            new StubAuthService(),
            new StubPasswordResetService(),
            new FakeAuditLogService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user
            }
        };

        return controller;
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"{role}-user"),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));

    private sealed class StubAuthService : IAuthService
    {
        public Task<LoginResult> PasswordSignInAsync(LoginViewModel model, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubPasswordResetService : IPasswordResetService
    {
        public Task<PasswordResetResult> RequestOwnerPasswordResetOtpAsync(
            ForgotPasswordViewModel model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PasswordResetResult> ResetOwnerPasswordWithOtpAsync(
            ResetPasswordWithOtpViewModel model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
