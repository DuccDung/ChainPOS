using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Email;
using ChainPOS.ViewModels.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Auth;

public sealed class PasswordResetService : IPasswordResetService
{
    private const string TokenProvider = "ChainPOS";
    private const string TokenName = "OwnerPasswordResetOtp";
    private const int OtpExpiryMinutes = 10;
    private const int MaxOtpAttempts = 5;
    private const int ResendCooldownSeconds = 60;
    private const string GenericOtpSentMessage =
        "Nếu email thuộc tài khoản Owner hợp lệ, mã OTP đã được gửi. Vui lòng kiểm tra hộp thư.";

    private readonly StoreFlowDbContext _db;
    private readonly PasswordHasher<AspNetUser> _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        StoreFlowDbContext db,
        PasswordHasher<AspNetUser> passwordHasher,
        IEmailSender emailSender,
        IAuditLogService auditLog,
        ILogger<PasswordResetService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<PasswordResetResult> RequestOwnerPasswordResetOtpAsync(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadActiveOwnerByEmailAsync(model.Email, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return PasswordResetResult.Success(GenericOtpSentMessage);
        }

        var now = DateTime.UtcNow;
        var tokenEntity = await LoadTokenAsync(user.Id, cancellationToken);
        if (tokenEntity is not null
            && TryReadToken(tokenEntity.Value, out var existingToken)
            && existingToken.CreatedAtUtc.AddSeconds(ResendCooldownSeconds) > now)
        {
            return PasswordResetResult.Success(GenericOtpSentMessage);
        }

        var otp = GenerateOtp();
        var token = new StoredPasswordResetOtp
        {
            OtpHash = _passwordHasher.HashPassword(user, otp),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(OtpExpiryMinutes),
            FailedAttempts = 0
        };

        tokenEntity ??= new AspNetUserToken
        {
            UserId = user.Id,
            LoginProvider = TokenProvider,
            Name = TokenName
        };
        tokenEntity.Value = JsonSerializer.Serialize(token);

        if (_db.Entry(tokenEntity).State == EntityState.Detached)
        {
            _db.AspNetUserTokens.Add(tokenEntity);
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailSender.SendAsync(
                user.Email,
                "Mã OTP đặt lại mật khẩu ChainPOS",
                BuildOtpEmailBody(user, otp),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not send owner password reset OTP email to {Email}.", user.Email);
            _db.AspNetUserTokens.Remove(tokenEntity);
            await _db.SaveChangesAsync(cancellationToken);
            return PasswordResetResult.Failed("Không gửi được OTP qua email. Vui lòng kiểm tra cấu hình SMTP và thử lại.");
        }

        await _auditLog.LogForUserAsync(
            "RequestOwnerPasswordResetOtp",
            user.Id,
            nameof(AspNetUser),
            user.Id,
            newValue: "Owner password reset OTP sent.",
            tenantId: user.TenantId,
            cancellationToken: cancellationToken);

        return PasswordResetResult.Success(GenericOtpSentMessage);
    }

    public async Task<PasswordResetResult> ResetOwnerPasswordWithOtpAsync(
        ResetPasswordWithOtpViewModel model,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadActiveOwnerByEmailAsync(model.Email, cancellationToken);
        if (user is null)
        {
            return PasswordResetResult.Failed("Email hoặc mã OTP không hợp lệ.");
        }

        var tokenEntity = await LoadTokenAsync(user.Id, cancellationToken);
        if (tokenEntity is null || !TryReadToken(tokenEntity.Value, out var token))
        {
            return PasswordResetResult.Failed("Mã OTP không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        if (token.ExpiresAtUtc <= DateTime.UtcNow || token.FailedAttempts >= MaxOtpAttempts)
        {
            _db.AspNetUserTokens.Remove(tokenEntity);
            await _db.SaveChangesAsync(cancellationToken);
            return PasswordResetResult.Failed("Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, token.OtpHash, model.Otp.Trim());
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            token.FailedAttempts += 1;
            tokenEntity.Value = JsonSerializer.Serialize(token);
            await _db.SaveChangesAsync(cancellationToken);
            return PasswordResetResult.Failed("Mã OTP không đúng.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = user.Id;
        _db.AspNetUserTokens.Remove(tokenEntity);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogForUserAsync(
            "ResetOwnerPassword",
            user.Id,
            nameof(AspNetUser),
            user.Id,
            newValue: "Owner password reset by email OTP.",
            tenantId: user.TenantId,
            cancellationToken: cancellationToken);

        return PasswordResetResult.Success("Mật khẩu đã được đổi. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    private async Task<AspNetUser?> LoadActiveOwnerByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = Normalize(email);
        return await _db.AspNetUsers
            .Include(x => x.Roles)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail
                    && x.Status == UserStatuses.Active
                    && x.Roles.Any(r => r.Id == AppRoles.Owner),
                cancellationToken);
    }

    private async Task<AspNetUserToken?> LoadTokenAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.AspNetUserTokens.FirstOrDefaultAsync(
            x => x.UserId == userId
                && x.LoginProvider == TokenProvider
                && x.Name == TokenName,
            cancellationToken);
    }

    private static bool TryReadToken(string? value, out StoredPasswordResetOtp token)
    {
        token = new StoredPasswordResetOtp();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<StoredPasswordResetOtp>(value);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.OtpHash))
            {
                return false;
            }

            token = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GenerateOtp()
        => RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);

    private static string BuildOtpEmailBody(AspNetUser user, string otp)
    {
        var displayName = WebUtility.HtmlEncode(user.FullName ?? user.Email ?? "Owner");
        var encodedOtp = WebUtility.HtmlEncode(otp);

        return $"""
            <div style="font-family:Arial,sans-serif;color:#111827;line-height:1.6">
                <h2 style="margin:0 0 12px;color:#f97316">ChainPOS - Đặt lại mật khẩu</h2>
                <p>Xin chào <strong>{displayName}</strong>,</p>
                <p>Mã OTP đặt lại mật khẩu Owner của bạn là:</p>
                <p style="font-size:28px;font-weight:700;letter-spacing:6px;margin:18px 0;color:#111827">{encodedOtp}</p>
                <p>Mã có hiệu lực trong <strong>{OtpExpiryMinutes} phút</strong>. Không chia sẻ mã này cho người khác.</p>
                <p>Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>
            </div>
            """;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private sealed class StoredPasswordResetOtp
    {
        public string OtpHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public int FailedAttempts { get; set; }
    }
}
