using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ComplaintsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IComplaintEscalationService _complaintEscalationService;

    public ComplaintsController(AppDbContext dbContext, IComplaintEscalationService complaintEscalationService)
    {
        _dbContext = dbContext;
        _complaintEscalationService = complaintEscalationService;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ComplaintResponse>>> Mine(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var complaints = await _dbContext.Complaints
            .AsNoTracking()
            .Where(complaint => complaint.MemberId == currentMember.Id)
            .Include(complaint => complaint.Member)
            .Include(complaint => complaint.AssignedToMember)
            .Include(complaint => complaint.Histories)
                .ThenInclude(history => history.PerformedByUser)
            .OrderByDescending(complaint => complaint.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(complaints.Select(MapDetail));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ComplaintDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var complaint = await _complaintEscalationService.GetComplaintAsync(id, cancellationToken);
        if (complaint is null)
        {
            return NotFound();
        }

        if (complaint.MemberId != currentMember.Id && !AccessControl.CanManageComplaints(currentMember))
        {
            return Forbid();
        }

        return Ok(MapDetail(complaint));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ComplaintListItemResponse>>> List(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanManageComplaints(currentMember))
        {
            return Forbid();
        }

        var complaints = await _dbContext.Complaints
            .AsNoTracking()
            .Include(complaint => complaint.Member)
            .Include(complaint => complaint.AssignedToMember)
            .OrderByDescending(complaint => complaint.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(complaints.Select(complaint => new ComplaintListItemResponse(
            complaint.Id,
            complaint.MemberId,
            complaint.Member?.FullName ?? string.Empty,
            complaint.Subject,
            complaint.Message,
            complaint.Status.ToString(),
            complaint.AdminReply,
            complaint.Priority.ToString(),
            complaint.EscalationLevel,
            complaint.LastActionDateUtc,
            complaint.AssignedToMemberId,
            complaint.AssignedToMember?.FullName,
            complaint.CreatedAtUtc,
            complaint.UpdatedAtUtc)));
    }

    [HttpPost]
    public async Task<ActionResult<ComplaintResponse>> Create([FromBody] ComplaintRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var complaint = new Complaint
        {
            Member = currentMember,
            MemberId = currentMember.Id,
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            Priority = request.Priority ?? ComplaintPriority.Medium,
            EscalationLevel = 0,
            LastActionDateUtc = DateTime.UtcNow,
            Status = ComplaintStatus.Open
        };

        complaint.AssignedToMemberId = (await _complaintEscalationService.ResolveAssignedMemberAsync(complaint, cancellationToken))?.Id;

        _dbContext.ComplaintHistories.Add(new ComplaintHistory
        {
            ComplaintId = complaint.Id,
            Action = ComplaintHistoryAction.Created,
            PerformedByUserId = currentMember.Id,
            Notes = complaint.Subject,
            TimestampUtc = DateTime.UtcNow
        });

        if (complaint.AssignedToMemberId is not null)
        {
            _dbContext.ComplaintHistories.Add(new ComplaintHistory
            {
                ComplaintId = complaint.Id,
                Action = ComplaintHistoryAction.Assigned,
                PerformedByUserId = currentMember.Id,
                Notes = "Initial assignment",
                TimestampUtc = DateTime.UtcNow
            });
        }

        _dbContext.Complaints.Add(complaint);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await MapDetailAsync(complaint.Id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ComplaintReviewRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanManageComplaints(currentMember))
        {
            return Forbid();
        }

        var complaint = await _dbContext.Complaints
            .Include(item => item.Member)
            .Include(item => item.AssignedToMember)
            .Include(item => item.Histories)
                .ThenInclude(history => history.PerformedByUser)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (complaint is null)
        {
            return NotFound();
        }

        complaint.Status = request.Status;
        complaint.AdminReply = request.AdminReply?.Trim();
        complaint.ReviewedByMemberId = currentMember.Id;
        complaint.LastActionDateUtc = DateTime.UtcNow;
        complaint.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.ComplaintHistories.Add(new ComplaintHistory
        {
            ComplaintId = complaint.Id,
            Action = request.Status == ComplaintStatus.Resolved || request.Status == ComplaintStatus.Rejected
                ? ComplaintHistoryAction.Resolved
                : ComplaintHistoryAction.Commented,
            PerformedByUserId = currentMember.Id,
            Notes = request.AdminReply?.Trim(),
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] ComplaintCommentRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var complaint = await _dbContext.Complaints.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (complaint is null)
        {
            return NotFound();
        }

        if (!AccessControl.CanCommentOnComplaint(currentMember, complaint) && !AccessControl.CanManageComplaints(currentMember))
        {
            return Forbid();
        }

        complaint.LastActionDateUtc = DateTime.UtcNow;
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.ComplaintHistories.Add(new ComplaintHistory
        {
            ComplaintId = complaint.Id,
            Action = ComplaintHistoryAction.Commented,
            PerformedByUserId = currentMember.Id,
            Notes = request.Notes.Trim(),
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] ComplaintEscalateRequest? request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var complaint = await _dbContext.Complaints
            .Include(item => item.Member)
            .Include(item => item.AssignedToMember)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (complaint is null)
        {
            return NotFound();
        }

        if (!AccessControl.CanEscalateComplaint(currentMember, complaint))
        {
            return Forbid();
        }

        await _complaintEscalationService.EscalateAsync(complaint, currentMember, request?.Notes, automatic: false, cancellationToken);
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

    private static ComplaintResponse MapDetail(Complaint complaint)
    {
        return new ComplaintResponse(
            complaint.Id,
            complaint.MemberId,
            complaint.Member?.FullName ?? string.Empty,
            complaint.Subject,
            complaint.Message,
            complaint.Status.ToString(),
            complaint.AdminReply,
            complaint.Priority.ToString(),
            complaint.EscalationLevel,
            complaint.LastActionDateUtc,
            complaint.AssignedToMemberId,
            complaint.AssignedToMember?.FullName,
            complaint.CreatedAtUtc,
            complaint.UpdatedAtUtc,
            complaint.Histories
                .OrderByDescending(history => history.TimestampUtc)
                .Select(history => new ComplaintHistoryResponse(
                    history.Id,
                    history.ComplaintId,
                    history.Action.ToString(),
                    history.PerformedByUserId,
                    history.PerformedByUser?.FullName,
                    history.Notes,
                    history.TimestampUtc))
                .ToList());
    }

    private async Task<ComplaintResponse> MapDetailAsync(Guid complaintId, CancellationToken cancellationToken)
    {
        var complaint = await _dbContext.Complaints
            .AsNoTracking()
            .Include(item => item.Member)
            .Include(item => item.AssignedToMember)
            .Include(item => item.Histories)
                .ThenInclude(history => history.PerformedByUser)
            .FirstAsync(item => item.Id == complaintId, cancellationToken);

        return MapDetail(complaint);
    }
}