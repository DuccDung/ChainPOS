using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Auth;
using ChainPOS.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IAuditLogService _auditLog;

    public AccountController(
        IAuthService authService,
        IPasswordResetService passwordResetService,
        IAuditLogService auditLog)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
        _auditLog = auditLog;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.PasswordSignInAsync(model, cancellationToken);
        if (!result.Succeeded || result.Principal is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể đăng nhập.");
            return View(model);
        }

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(14)
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            result.Principal,
            authProperties);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToRoleDashboard(result.PrimaryRole);
    }

    [HttpGet("forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _passwordResetService.RequestOwnerPasswordResetOtpAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(VerifyForgotPasswordOtp), new { email = model.Email.Trim() });
    }

    [HttpGet("forgot-password/verify")]
    [AllowAnonymous]
    public IActionResult VerifyForgotPasswordOtp(string? email = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        return View(new ResetPasswordWithOtpViewModel { Email = email?.Trim() ?? string.Empty });
    }

    [HttpPost("forgot-password/verify")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyForgotPasswordOtp(
        ResetPasswordWithOtpViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _passwordResetService.ResetOwnerPasswordWithOtpAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Login));
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _auditLog.LogAsync(
            "Logout",
            nameof(AspNetUser),
            userId,
            newValue: $"User={User.Identity?.Name}",
            cancellationToken: HttpContext.RequestAborted);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToRoleDashboard(string? role = null)
    {
        role ??= User.IsInRole(AppRoles.Admin)
            ? AppRoles.Admin
            : User.IsInRole(AppRoles.Owner)
                ? AppRoles.Owner
                : User.IsInRole(AppRoles.Staff)
                    ? AppRoles.Staff
                    : null;

        return role switch
        {
            AppRoles.Admin => LocalRedirect("/admin/dashboard"),
            AppRoles.Owner => LocalRedirect("/owner/dashboard"),
            AppRoles.Staff => LocalRedirect("/staff/dashboard"),
            _ => RedirectToAction(nameof(AccessDenied))
        };
    }
}
