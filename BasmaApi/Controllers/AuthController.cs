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
        var email = request.Email.Trim().ToLowerInvariant();
        var member = await _dbContext.Members
            .Include(candidate => candidate.PermissionGrants)
            .FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        if (member is null)
        {
            return Unauthorized(new { message = "بيانات الدخول غير صحيحة." });
        }

        if (!_passwordService.VerifyPassword(request.Password, member.PasswordHash))
        {
            return Unauthorized(new { message = "بيانات الدخول غير صحيحة." });
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
            token,
            expiresAtUtc));
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
            AccessControl.GetEffectivePermissions(member)));
    }
}