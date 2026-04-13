using System.Text.Json;
using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Services;

public interface IComplaintEscalationService
{
    Task<Complaint?> GetComplaintAsync(Guid complaintId, CancellationToken cancellationToken);
    Task<ComplaintDetailResponse?> GetDetailAsync(Guid complaintId, CancellationToken cancellationToken);
    Task<Member?> ResolveAssignedMemberAsync(Complaint complaint, CancellationToken cancellationToken);
    Task<CommentResult> AddCommentAsync(Complaint complaint, Member? actor, string notes, CancellationToken cancellationToken);
    Task<CommentResult> EscalateAsync(Complaint complaint, Member? actor, string? notes, bool automatic, CancellationToken cancellationToken);
    Task<List<Complaint>> GetEscalationCandidatesAsync(CancellationToken cancellationToken);
}

public sealed record CommentResult(Complaint Complaint, ComplaintHistoryAction Action);

public sealed class ComplaintEscalationService : IComplaintEscalationService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public ComplaintEscalationService(AppDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    public async Task<Complaint?> GetComplaintAsync(Guid complaintId, CancellationToken cancellationToken)
    {
        return await _dbContext.Complaints
            .Include(item => item.Member)
            .Include(item => item.AssignedToMember)
            .Include(item => item.ReviewedByMember)
            .Include(item => item.Histories)
                .ThenInclude(item => item.PerformedByUser)
            .FirstOrDefaultAsync(item => item.Id == complaintId, cancellationToken);
    }

    public async Task<ComplaintDetailResponse?> GetDetailAsync(Guid complaintId, CancellationToken cancellationToken)
    {
        var complaint = await GetComplaintAsync(complaintId, cancellationToken);
        return complaint is null ? null : MapDetail(complaint);
    }

    public async Task<Member?> ResolveAssignedMemberAsync(Complaint complaint, CancellationToken cancellationToken)
    {
        var memberId = await ResolveAssignedMemberIdAsync(complaint, cancellationToken);
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members.FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);
    }

    public async Task<CommentResult> AddCommentAsync(Complaint complaint, Member? actor, string notes, CancellationToken cancellationToken)
    {
        complaint.LastActionDateUtc = DateTime.UtcNow;
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.ComplaintHistories.Add(new ComplaintHistory
        {
            ComplaintId = complaint.Id,
            Action = ComplaintHistoryAction.Commented,
            PerformedByUserId = actor?.Id,
            Notes = notes.Trim(),
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new CommentResult(complaint, ComplaintHistoryAction.Commented);
    }

    public async Task<CommentResult> EscalateAsync(Complaint complaint, Member? actor, string? notes, bool automatic, CancellationToken cancellationToken)
    {
        if (complaint.Status == ComplaintStatus.Resolved)
        {
            return new CommentResult(complaint, ComplaintHistoryAction.Resolved);
        }

        if (complaint.EscalationLevel < 3)
        {
            complaint.EscalationLevel += 1;
        }

        var assignedMember = await ResolveAssignedMemberAsync(complaint, cancellationToken);
        complaint.AssignedToMemberId = assignedMember?.Id;
        complaint.Status = ComplaintStatus.InReview;
        complaint.LastActionDateUtc = DateTime.UtcNow;
        complaint.UpdatedAtUtc = DateTime.UtcNow;
        complaint.ReviewedByMemberId = actor?.Id;

        _dbContext.ComplaintHistories.Add(new ComplaintHistory
        {
            ComplaintId = complaint.Id,
            Action = ComplaintHistoryAction.Escalated,
            PerformedByUserId = actor?.Id,
            Notes = string.IsNullOrWhiteSpace(notes)
                ? (automatic ? "Escalated automatically." : "Escalated manually.")
                : notes.Trim(),
            TimestampUtc = DateTime.UtcNow
        });

        if (complaint.AssignedToMemberId is not null)
        {
            _dbContext.ComplaintHistories.Add(new ComplaintHistory
            {
                ComplaintId = complaint.Id,
                Action = ComplaintHistoryAction.Assigned,
                PerformedByUserId = actor?.Id,
                Notes = assignedMember is null ? "Assigned to unassigned queue." : $"Assigned to {assignedMember.FullName}",
                TimestampUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.RecordAsync(
            automatic ? "Escalate" : "Assign",
            "Complaint",
            complaint.Id.ToString(),
            null,
            new
            {
                complaint.EscalationLevel,
                complaint.AssignedToMemberId,
                complaint.Status,
                complaint.LastActionDateUtc
            },
            cancellationToken);

        return new CommentResult(complaint, ComplaintHistoryAction.Escalated);
    }

    public async Task<List<Complaint>> GetEscalationCandidatesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var complaints = await _dbContext.Complaints
            .Include(item => item.Member)
            .Where(item => item.Status != ComplaintStatus.Resolved)
            .ToListAsync(cancellationToken);

        return complaints
            .Where(item => now - item.LastActionDateUtc >= GetThreshold(item.Priority))
            .ToList();
    }

    private async Task<Guid?> ResolveAssignedMemberIdAsync(Complaint complaint, CancellationToken cancellationToken)
    {
        var assignedMember = await ResolveAssignedMemberEntityAsync(complaint, cancellationToken);
        return assignedMember?.Id;
    }

    private async Task<Member?> ResolveAssignedMemberEntityAsync(Complaint complaint, CancellationToken cancellationToken)
    {
        var targetRole = GetTargetRole(complaint.EscalationLevel);
        if (targetRole is null)
        {
            return null;
        }

        IQueryable<Member> query = _dbContext.Members;

        query = targetRole switch
        {
            MemberRole.GovernorCommitteeCoordinator => query.Where(member => member.Role == MemberRole.GovernorCommitteeCoordinator && member.GovernorName == complaint.Member!.GovernorName && member.CommitteeName == complaint.Member.CommitteeName),
            MemberRole.GovernorCoordinator => query.Where(member => member.Role == MemberRole.GovernorCoordinator && member.GovernorName == complaint.Member!.GovernorName),
            MemberRole.President => query.Where(member => member.Role == MemberRole.President),
            _ => query.Where(member => member.Role == targetRole)
        };

        return await query.OrderByDescending(member => member.Points).FirstOrDefaultAsync(cancellationToken);
    }

    private static TimeSpan GetThreshold(ComplaintPriority priority)
    {
        return priority switch
        {
            ComplaintPriority.Low => TimeSpan.FromHours(72),
            ComplaintPriority.Medium => TimeSpan.FromHours(48),
            ComplaintPriority.High => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(48)
        };
    }

    private static MemberRole? GetTargetRole(int escalationLevel)
    {
        return escalationLevel switch
        {
            0 => MemberRole.GovernorCommitteeCoordinator,
            1 => MemberRole.GovernorCoordinator,
            2 => MemberRole.President,
            _ => null
        };
    }

    private static ComplaintDetailResponse MapDetail(Complaint complaint)
    {
        return new ComplaintDetailResponse(
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
}