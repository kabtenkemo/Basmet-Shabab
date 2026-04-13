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

    public AuthController(AppDbContext dbContext, IPasswordService passwordService, IAuditLogService auditLogService, ITokenService tokenService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _auditLogService = auditLogService;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new { message = "التسجيل الخارجي غير متاح. إنشاء الحسابات يتم من داخل المناصب فقط." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        
        var member = await _dbContext.Members
            .Include(candidate => candidate.PermissionGrants)
            .FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);
        
        if (member is null)
        {
            return Unauthorized(new { message = "بيانات الدخول غير صحيحة." });
        }

        // Validate that password hash exists
        if (string.IsNullOrWhiteSpace(member.PasswordHash))
        {
            return Unauthorized(new { message = "بيانات الدخول غير صحيحة. حساب بدون كلمة سر." });
        }

        // Validate password is provided
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { message = "كلمة المرور مطلوبة." });
        }

        try
        {
            var passwordVerified = _passwordService.VerifyPassword(request.Password, member.PasswordHash);
            if (!passwordVerified)
            {
                return Unauthorized(new { message = "بيانات الدخول غير صحيحة." });
            }
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = "خطأ في التحقق من كلمة المرور.", error = ex.Message });
        }

        var (token, expiresAtUtc) = _tokenService.CreateToken(member);
        await _auditLogService.RecordAsync(
            "Login",
            "User",
            member.Id,
            member.FullName,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            member.Id.ToString(),
            null,
            new { member.FullName, member.Email, member.Role },
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