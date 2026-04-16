using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Route("api/join-requests")]
public sealed class JoinRequestsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public JoinRequestsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<TeamJoinRequestResponse>> Create([FromBody] TeamJoinRequestCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 4)
        {
            return BadRequest(new { message = "الاسم الرباعي مطلوب لتقديم طلب الالتحاق." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "البريد الإلكتروني مطلوب." });
        }

        try
        {
            _ = new System.Net.Mail.MailAddress(request.Email.Trim());
        }
        catch
        {
            return BadRequest(new { message = "البريد الإلكتروني غير صحيح." });
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.PhoneNumber.Trim().Length < 8)
        {
            return BadRequest(new { message = "رقم الهاتف مطلوب ويجب أن يكون صحيحًا." });
        }

        var nationalId = request.NationalId.Trim();
        if (nationalId.Length != 14 || nationalId.Any(character => character < '0' || character > '9'))
        {
            return BadRequest(new { message = "الرقم القومي مطلوب ويجب أن يكون 14 رقمًا." });
        }

        if (string.IsNullOrWhiteSpace(request.Motivation) || request.Motivation.Trim().Length < 20)
        {
            return BadRequest(new { message = "اكتب نبذة مناسبة عن سبب الانضمام لا تقل عن 20 حرفًا." });
        }

        var governorate = await _dbContext.Governorates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.GovernorateId, cancellationToken);

        if (governorate is null)
        {
            return BadRequest(new { message = "المحافظة المختارة غير موجودة." });
        }

        Committee? committee = null;
        if (request.CommitteeId is not null)
        {
            committee = await _dbContext.Committees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.CommitteeId.Value && item.GovernorateId == request.GovernorateId, cancellationToken);

            if (committee is null)
            {
                return BadRequest(new { message = "اللجنة المختارة لا تتبع المحافظة المحددة." });
            }
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var duplicatePendingRequest = await _dbContext.TeamJoinRequests.AnyAsync(
            item => item.Email == normalizedEmail && item.GovernorateId == request.GovernorateId && item.Status == JoinRequestStatus.Pending,
            cancellationToken);

        if (duplicatePendingRequest)
        {
            return Conflict(new { message = "يوجد طلب التحاق مفتوح بالفعل بنفس البريد الإلكتروني في هذه المحافظة." });
        }

        var duplicateNationalIdPendingRequest = await _dbContext.TeamJoinRequests.AnyAsync(
            item => item.NationalId == nationalId && item.GovernorateId == request.GovernorateId && item.Status == JoinRequestStatus.Pending,
            cancellationToken);

        if (duplicateNationalIdPendingRequest)
        {
            return Conflict(new { message = "يوجد طلب التحاق مفتوح بالفعل بنفس الرقم القومي في هذه المحافظة." });
        }

        var assignedCoordinator = await _dbContext.Members
            .AsNoTracking()
            .Where(member => member.Role == MemberRole.GovernorCoordinator && member.GovernorateId == request.GovernorateId)
            .OrderBy(member => member.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var joinRequest = new TeamJoinRequest
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber.Trim(),
            NationalId = nationalId,
            BirthDate = request.BirthDate,
            GovernorateId = governorate.Id,
            CommitteeId = committee?.Id,
            Motivation = request.Motivation.Trim(),
            Experience = string.IsNullOrWhiteSpace(request.Experience) ? null : request.Experience.Trim(),
            AssignedToMemberId = assignedCoordinator?.Id
        };

        _dbContext.TeamJoinRequests.Add(joinRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.TeamJoinRequests
            .AsNoTracking()
            .Include(item => item.Governorate)
            .Include(item => item.Committee)
            .Include(item => item.AssignedToMember)
            .Include(item => item.ReviewedByMember)
            .FirstAsync(item => item.Id == joinRequest.Id, cancellationToken);

        return Ok(MapResponse(created));
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeamJoinRequestResponse>>> List(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanReviewJoinRequests(currentMember))
        {
            return Forbid();
        }

        var query = _dbContext.TeamJoinRequests
            .AsNoTracking()
            .Include(item => item.Governorate)
            .Include(item => item.Committee)
            .Include(item => item.AssignedToMember)
            .Include(item => item.ReviewedByMember)
            .AsQueryable();

        if (currentMember.Role is not (MemberRole.President or MemberRole.VicePresident)
            && currentMember.GovernorateId is not null)
        {
            query = query.Where(item => item.GovernorateId == currentMember.GovernorateId);
        }

        var items = await query
            .OrderBy(item => item.Status)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapResponse));
    }

    [Authorize]
    [HttpPut("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] TeamJoinRequestReviewRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanReviewJoinRequests(currentMember))
        {
            return Forbid();
        }

        if (!Enum.TryParse<JoinRequestStatus>(request.Status, true, out var nextStatus) || nextStatus == JoinRequestStatus.Reviewed || nextStatus == JoinRequestStatus.Pending)
        {
            return BadRequest(new { message = "حالة الطلب غير صالحة. اختر Accepted أو Rejected." });
        }

        var item = await _dbContext.TeamJoinRequests.FirstOrDefaultAsync(joinRequest => joinRequest.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (currentMember.Role is not (MemberRole.President or MemberRole.VicePresident)
            && currentMember.GovernorateId is not null
            && item.GovernorateId != currentMember.GovernorateId)
        {
            return Forbid();
        }

        item.Status = nextStatus;
        item.AdminNotes = string.IsNullOrWhiteSpace(request.AdminNotes) ? null : request.AdminNotes.Trim();
        item.ReviewedAtUtc = DateTime.UtcNow;
        item.ReviewedByMemberId = currentMember.Id;

        if (item.AssignedToMemberId is null && currentMember.Role == MemberRole.GovernorCoordinator)
        {
            item.AssignedToMemberId = currentMember.Id;
        }

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

    private static TeamJoinRequestResponse MapResponse(TeamJoinRequest item)
    {
        return new TeamJoinRequestResponse(
            item.Id,
            item.FullName,
            item.Email,
            item.PhoneNumber,
            item.NationalId,
            item.BirthDate,
            item.GovernorateId,
            item.Governorate.Name,
            item.CommitteeId,
            item.Committee?.Name,
            item.Motivation,
            item.Experience,
            item.Status.ToString(),
            item.AdminNotes,
            item.AssignedToMemberId,
            item.AssignedToMember?.FullName,
            item.ReviewedByMemberId,
            item.ReviewedByMember?.FullName,
            item.CreatedAtUtc,
            item.ReviewedAtUtc);
    }
}
