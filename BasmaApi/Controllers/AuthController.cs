using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly IAuditLogService _auditLogService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController>? _logger;

    public AuthController(AppDbContext dbContext, IPasswordService passwordService, IAuditLogService auditLogService, ITokenService tokenService, ILogger<AuthController>? logger = null)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _auditLogService = auditLogService;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new { message = "التسجيل الخارجي غير متاح. إنشاء الحسابات يتم من داخل المناصب فقط." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        // FIX: Normalize email to lowercase for case-insensitive search
        // This prevents login failures when user enters "PRESIDENT@BASMET.LOCAL" instead of "president@basmet.local"
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var currentIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        
        // Validate input
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { message = "البريد الإلكتروني وكلمة المرور مطلوبان." });
        }

        // Rate limiting: check for recent failed attempts
        var recentFailedAttempts = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.ActionType == "Login_Failed" &&
                log.IPAddress == currentIpAddress &&
                log.TimestampUtc > DateTime.UtcNow.AddMinutes(-15))
            .CountAsync(cancellationToken);

        if (recentFailedAttempts >= 10)
        {
            await LogFailedLogin(email, currentIpAddress, "متعددة محاولات فاشلة من نفس الـ IP", cancellationToken);
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "تم تجاوز عدد محاولات الدخول المسموحة. يرجى المحاولة بعد 15 دقيقة." });
        }
        
        var member = await _dbContext.Members
            .Include(candidate => candidate.PermissionGrants)
            .FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);
        
        if (member is null)
        {
            await LogFailedLogin(email, currentIpAddress, "بريد إلكتروني غير موجود", cancellationToken);
            return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });
        }

        // Validate that password hash exists
        if (string.IsNullOrWhiteSpace(member.PasswordHash))
        {
            await LogFailedLogin(email, currentIpAddress, "حساب بدون كلمة سر", cancellationToken);
            return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });
        }

        try
        {
            var passwordVerified = _passwordService.VerifyPassword(request.Password, member.PasswordHash);
            if (!passwordVerified)
            {
                await LogFailedLogin(email, currentIpAddress, "كلمة سر غير صحيحة", cancellationToken);
                return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "خطأ في التحقق من كلمة المرور للعضو {MemberId}", member.Id);
            await LogFailedLogin(email, currentIpAddress, $"خطأ في التحقق: {ex.Message}", cancellationToken);
            return Unauthorized(new { message = "خطأ تقني في التحقق. يرجى المحاولة بعد قليل." });
        }

        var (token, expiresAtUtc) = _tokenService.CreateToken(member);
        await _auditLogService.RecordAsync(
            "Login_Success",
            "User",
            member.Id,
            member.FullName,
            currentIpAddress,
            member.Id.ToString(),
            null,
            new { member.FullName, member.Email, member.Role, expiresAtUtc },
            cancellationToken);

        return Ok(new AuthResponse(
            member.Id,
            member.FullName,
            member.Email,
            member.Role.ToString(),
            member.NationalId,
            member.BirthDate,
            member.GovernorName,
            member.CommitteeName,
            member.Points,
            AccessControl.GetEffectivePermissions(member),
            member.MustChangePassword,
            token,
            expiresAtUtc));
    }

    private async Task LogFailedLogin(string email, string ipAddress, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _auditLogService.RecordAsync(
                "Login_Failed",
                "User",
                null,
                email,
                ipAddress,
                null,
                null,
                new { reason },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "خطأ في تسجيل محاولة دخول فاشلة");
        }
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var member = await GetCurrentMemberAsync(cancellationToken);
        if (member is null)
        {
            return Unauthorized();
        }

        if (!_passwordService.VerifyPassword(request.CurrentPassword, member.PasswordHash))
        {
            return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة." });
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "كلمة المرور الجديدة يجب أن تكون مختلفة عن الحالية." });
        }

        member.PasswordHash = _passwordService.HashPassword(request.NewPassword);
        member.MustChangePassword = false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.RecordAsync(
            "ChangePassword",
            "Member",
            member.Id,
            member.FullName,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            member.Id.ToString(),
            new { MustChangePassword = true },
            new { MustChangePassword = false },
            cancellationToken);

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح." });
    }

    [HttpGet("me")]
    public async Task<ActionResult<MemberSummaryResponse>> Me(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return Unauthorized();
        }

        var member = await _dbContext.Members
            .AsNoTracking()
            .Include(item => item.PermissionGrants)
            .FirstOrDefaultAsync(item => item.Id == memberId.Value, cancellationToken);
        if (member is null)
        {
            return Unauthorized();
        }

        return Ok(new MemberSummaryResponse(
            member.Id,
            member.FullName,
            member.Email,
            member.Role.ToString(),
            member.NationalId,
            member.BirthDate,
            member.GovernorName,
            member.CommitteeName,
            member.Points,
            AccessControl.GetEffectivePermissions(member),
            member.MustChangePassword));
    }

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members
            .Include(item => item.PermissionGrants)
            .FirstOrDefaultAsync(item => item.Id == memberId.Value, cancellationToken);
    }
}