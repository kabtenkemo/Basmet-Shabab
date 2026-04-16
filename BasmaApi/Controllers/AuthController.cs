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

    public AuthController(
        AppDbContext dbContext, 
        IPasswordService passwordService, 
        IAuditLogService auditLogService, 
        ITokenService tokenService, 
        ILogger<AuthController>? logger = null)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _auditLogService = auditLogService;
        _tokenService = tokenService;
        _logger = logger;
    }

    private string GetClientIpAddress()
    {
        // Check X-Forwarded-For header (used by proxies like Netlify, Cloudflare, Nginx, etc.)
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ips = forwardedFor.ToString().Split(',');
            if (ips.Length > 0 && !string.IsNullOrWhiteSpace(ips[0]))
            {
                return ips[0].Trim();
            }
        }

        // Check CF-Connecting-IP header (Cloudflare)
        if (HttpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfConnectingIp))
        {
            var ip = cfConnectingIp.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        // Check X-Real-IP header (Nginx and others)
        if (HttpContext.Request.Headers.TryGetValue("X-Real-IP", out var xRealIp))
        {
            var ip = xRealIp.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        // Fallback to direct connection IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        
        // Validate input
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { message = "البريد الإلكتروني وكلمة المرور مطلوبان." });
        }

        var member = await _dbContext.Members
            .Include(candidate => candidate.PermissionGrants)
            .FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);
        
        if (member is null)
        {
            await LogFailedLogin(email, GetClientIpAddress(), "بريد إلكتروني غير موجود", cancellationToken);
            return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });
        }

        // Validate that password hash exists
        if (string.IsNullOrWhiteSpace(member.PasswordHash))
        {
            await LogFailedLogin(email, GetClientIpAddress(), "حساب بدون كلمة سر", cancellationToken);
            return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });
        }

        try
        {
            var passwordVerified = await VerifyAndUpgradePasswordAsync(member, request.Password, cancellationToken);
            if (!passwordVerified)
            {
                await LogFailedLogin(email, GetClientIpAddress(), "كلمة سر غير صحيحة", cancellationToken);
                return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "خطأ في التحقق من كلمة المرور للعضو {MemberId}", member.Id);
            await LogFailedLogin(email, GetClientIpAddress(), $"خطأ في التحقق: {ex.Message}", cancellationToken);
            return Unauthorized(new { message = "خطأ تقني في التحقق. يرجى المحاولة بعد قليل." });
        }

        var (token, expiresAtUtc) = _tokenService.CreateToken(member);
        await _auditLogService.RecordAsync(
            "Login_Success",
            "User",
            member.Id,
            member.FullName,
            GetClientIpAddress(),
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

    private async Task<bool> VerifyAndUpgradePasswordAsync(Member member, string rawPassword, CancellationToken cancellationToken)
    {
        if (_passwordService.VerifyPassword(rawPassword, member.PasswordHash))
        {
            return true;
        }

        // Compatibility path: if an old record stored a plain-text password, upgrade it after first successful login.
        if (string.Equals(member.PasswordHash, rawPassword, StringComparison.Ordinal))
        {
            member.PasswordHash = _passwordService.HashPassword(rawPassword);
            member.MustChangePassword = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
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