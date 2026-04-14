using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class MembersController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public MembersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("me")]
    public async Task<ActionResult<DashboardMeResponse>> Me(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        return Ok(MapMe(currentMember));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MemberListItemResponse>>> List(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanManageUsers(currentMember))
        {
            return Forbid();
        }

        var query = _dbContext.Members
            .AsNoTracking()
            .Include(member => member.PermissionGrants)
            .AsQueryable();

        query = ApplyScopeFilter(query, currentMember);

        var members = await query
            .OrderByDescending(member => member.Points)
            .ThenBy(member => member.FullName)
            .ToListAsync(cancellationToken);

        return Ok(members.Select(MapListItem));
    }

    [HttpPost]
    public async Task<ActionResult<MemberListItemResponse>> Create([FromBody] MemberCreateRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        // Validate role
        if (!Enum.TryParse<MemberRole>(request.Role, ignoreCase: true, out var targetRole))
        {
            return BadRequest(new { message = "الدور غير صالح." });
        }

        // Validate name: must have at least 4 parts
        if (!HasAtLeastFourNameParts(request.FullName))
        {
            var parts = request.FullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return BadRequest(new { message = $"الاسم رباعي مطلوب (أدخل الآن {parts} أجزاء فقط)." });
        }

        // Validate national ID format
        var nationalId = NormalizeNationalId(request.NationalId);
        if (nationalId.Length != 14 || nationalId.Any(character => character < '0' || character > '9'))
        {
            return BadRequest(new { message = "الرقم القومي يجب أن يكون 14 رقمًا بلا فراغات أو أحرف." });
        }

        // Validate birth date
        if (request.BirthDate is null)
        {
            return BadRequest(new { message = "تاريخ الميلاد مطلوب." });
        }

        // Validate permission to create this role
        if (!AccessControl.CanCreateMember(currentMember, targetRole))
        {
            return Forbid();
        }

        // Validate email format and uniqueness
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "البريد الإلكتروني مطلوب." });
        }

        try
        {
            var emailAddress = new System.Net.Mail.MailAddress(email);
        }
        catch
        {
            return BadRequest(new { message = "البريد الإلكتروني غير صحيح." });
        }

        var emailExists = await _dbContext.Members.AnyAsync(member => member.Email.ToLower() == email, cancellationToken);
        if (emailExists)
        {
            return Conflict(new { message = "هذا البريد مستخدم بالفعل. جرب بريد آخر." });
        }

        // Validate national ID uniqueness
        var nationalIdExists = await _dbContext.Members.AnyAsync(member => member.NationalId == nationalId, cancellationToken);
        if (nationalIdExists)
        {
            return Conflict(new { message = "الرقم القومي مستخدم بالفعل." });
        }

        // Resolve governorate and committee for user's role
        var scopeResult = await ResolveScopeAsync(targetRole, request.GovernorateId, request.CommitteeId, cancellationToken);
        if (scopeResult.ErrorMessage is not null)
        {
            return BadRequest(new { message = scopeResult.ErrorMessage });
        }

        var member = new Member
        {
            FullName = request.FullName.Trim(),
            Email = email,
            NationalId = nationalId,
            BirthDate = request.BirthDate,
            Role = targetRole,
            GovernorateId = scopeResult.GovernorateId,
            CommitteeId = scopeResult.CommitteeId,
            GovernorName = scopeResult.GovernorName,
            CommitteeName = scopeResult.CommitteeName,
            CreatedByMemberId = currentMember.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123", 12),  // BCrypt with 12 rounds
            MustChangePassword = true
        };

        _dbContext.Members.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), MapListItem(member));
    }

    [HttpPost("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] GrantRoleRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<MemberRole>(request.Role, ignoreCase: true, out var targetRole))
        {
            return BadRequest(new { message = "الدور غير صالح." });
        }

        if (!AccessControl.CanAssignRole(currentMember, targetRole))
        {
            return Forbid();
        }

        var member = await _dbContext.Members.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        if (!AccessControl.CanManageMember(currentMember, member))
        {
            return Forbid();
        }

        member.Role = targetRole;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/permissions")]
    public async Task<IActionResult> GrantPermission(Guid id, [FromBody] GrantPermissionRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var member = await _dbContext.Members
            .Include(item => item.PermissionGrants)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (member is null)
        {
            return NotFound();
        }

        if (!AccessControl.CanManageMember(currentMember, member) && !AccessControl.CanManageUsers(currentMember))
        {
            return Forbid();
        }

        var normalizedKey = request.PermissionKey.Trim();
        if (member.PermissionGrants.Any(grant => grant.PermissionKey == normalizedKey))
        {
            return Conflict(new { message = "الصلاحية مضافة بالفعل." });
        }

        _dbContext.PermissionGrants.Add(new MemberPermissionGrant
        {
            MemberId = member.Id,
            PermissionKey = normalizedKey,
            GrantedByMemberId = currentMember.Id
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/points")]
    public async Task<IActionResult> AdjustPoints(Guid id, [FromBody] PointAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (request.Amount == 0)
        {
            return BadRequest(new { message = "قيمة النقاط يجب ألا تكون صفرًا." });
        }

        var member = await _dbContext.Members.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        if (!AccessControl.CanManageMember(currentMember, member) && !AccessControl.CanManagePoints(currentMember))
        {
            return Forbid();
        }

        member.Points += request.Amount;
        _dbContext.PointTransactions.Add(new PointTransaction
        {
            MemberId = member.Id,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            RelatedByMemberId = currentMember.Id
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members
            .Include(member => member.PermissionGrants)
            .FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);
    }

    private static DashboardMeResponse MapMe(Member member)
    {
        return new DashboardMeResponse(
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
            member.MustChangePassword);
    }

    private static MemberListItemResponse MapListItem(Member member)
    {
        return new MemberListItemResponse(
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
            member.CreatedAtUtc);
    }

    private static IQueryable<Member> ApplyScopeFilter(IQueryable<Member> query, Member actor)
    {
        if (AccessControl.CanViewAllMembers(actor))
        {
            return query;
        }

        if (actor.Role == MemberRole.GovernorCoordinator)
        {
            return query.Where(member => member.GovernorName == actor.GovernorName);
        }

        if (actor.Role == MemberRole.GovernorCommitteeCoordinator)
        {
            return query.Where(member => member.GovernorName == actor.GovernorName && member.CommitteeName == actor.CommitteeName);
        }

        return query.Where(member => member.Id == actor.Id);
    }

    private static string? NormalizeScopeName(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private async Task<ScopeResolutionResult> ResolveScopeAsync(MemberRole role, Guid? governorateId, Guid? committeeId, CancellationToken cancellationToken)
    {
        if (role is MemberRole.President or MemberRole.VicePresident or MemberRole.CentralMember)
        {
            if (governorateId is not null || committeeId is not null)
            {
                return new ScopeResolutionResult(null, null, null, null, "هذه الأدوار لا تحتاج إلى محافظة أو لجنة.");
            }

            return new ScopeResolutionResult(null, null, null, null, null);
        }

        if (governorateId is null)
        {
            return new ScopeResolutionResult(null, null, null, null, "المحافظة مطلوبة لهذا الدور.");
        }

        var governorate = await _dbContext.Governorates.FirstOrDefaultAsync(item => item.Id == governorateId.Value, cancellationToken);
        if (governorate is null)
        {
            return new ScopeResolutionResult(null, null, null, null, "المحافظة المختارة غير موجودة.");
        }

        if (role == MemberRole.GovernorCoordinator)
        {
            if (committeeId is not null)
            {
                return new ScopeResolutionResult(null, null, null, null, "منسق المحافظة لا يحتاج إلى لجنة عند الإنشاء.");
            }

            return new ScopeResolutionResult(governorate.Id, null, governorate.Name, null, null);
        }

        if (committeeId is null)
        {
            return new ScopeResolutionResult(null, null, null, null, "اللجنة مطلوبة لهذا الدور.");
        }

        var committee = await _dbContext.Committees.FirstOrDefaultAsync(item => item.Id == committeeId.Value, cancellationToken);
        if (committee is null || committee.GovernorateId != governorate.Id)
        {
            return new ScopeResolutionResult(null, null, null, null, "اللجنة المختارة لا تتبع المحافظة المحددة.");
        }

        return new ScopeResolutionResult(governorate.Id, committee.Id, governorate.Name, committee.Name, null);
    }

    private sealed record ScopeResolutionResult(Guid? GovernorateId, Guid? CommitteeId, string? GovernorName, string? CommitteeName, string? ErrorMessage);

    private static string NormalizeNationalId(string value)
    {
        return new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
    }

    private static bool HasAtLeastFourNameParts(string fullName)
    {
        return fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length >= 4;
    }
}