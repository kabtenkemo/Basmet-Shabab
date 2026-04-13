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
public sealed class TasksController : ControllerBase
{
    private const int CompletionPoints = 5;
    private readonly AppDbContext _dbContext;

    public TasksController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetMyTasks(CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var tasks = await _dbContext.Tasks
            .AsNoTracking()
            .Include(task => task.TargetRoles)
            .Include(task => task.TargetMembers)
            .OrderByDescending(task => task.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(tasks.Where(task => IsVisibleToMember(task, currentMember)).Select(Map));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> GetTask(Guid id, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var task = await _dbContext.Tasks
            .Include(item => item.TargetRoles)
            .Include(item => item.TargetMembers)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        if (!IsVisibleToMember(task, currentMember))
        {
            return NotFound();
        }

        return Ok(Map(task));
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> CreateTask([FromBody] TaskRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<TaskAudienceType>(request.AudienceType, ignoreCase: true, out var audienceType))
        {
            return BadRequest(new { message = "تصنيف المهمة غير صالح." });
        }

        if (currentMember.Role == MemberRole.GovernorCommitteeCoordinator && audienceType != TaskAudienceType.Members)
        {
            return BadRequest(new { message = "منسق اللجنة يمكنه إسناد المهمة لأعضاء لجنته فقط." });
        }

        if (currentMember.Role == MemberRole.GovernorCoordinator && audienceType != TaskAudienceType.Members)
        {
            return BadRequest(new { message = "منسق المحافظة يمكنه إسناد المهمة فقط لمنسقي اللجان داخل محافظته." });
        }

        var targetRoles = ParseTargetRoles(request.TargetRoles, audienceType, out var targetRolesError);
        if (targetRolesError is not null)
        {
            return BadRequest(new { message = targetRolesError });
        }

        var (targetMemberIds, targetMembersError) = await ParseTargetMemberIdsAsync(currentMember, request.TargetMemberIds, audienceType, cancellationToken);
        if (targetMembersError is not null)
        {
            return BadRequest(new { message = targetMembersError });
        }

        if (audienceType == TaskAudienceType.Members && targetMemberIds.Count == 0)
        {
            return BadRequest(new { message = "اختر عضوًا واحدًا على الأقل." });
        }

        if (audienceType == TaskAudienceType.Roles && targetRoles.Count == 0)
        {
            return BadRequest(new { message = "اختر منصبًا واحدًا على الأقل." });
        }

        var task = new MemberTask
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            AudienceType = audienceType,
            IsCompleted = request.IsCompleted,
            DueDate = request.DueDate,
            MemberId = currentMember.Id
        };

        _dbContext.Tasks.Add(task);

        foreach (var role in targetRoles)
        {
            _dbContext.TaskTargetRoles.Add(new TaskTargetRole
            {
                Task = task,
                Role = role
            });
        }

        foreach (var targetMemberId in targetMemberIds)
        {
            _dbContext.TaskTargetMembers.Add(new TaskTargetMember
            {
                Task = task,
                MemberId = targetMemberId
            });
        }

        if (task.IsCompleted)
        {
            await AddCompletionPointsAsync(task.MemberId, "إضافة مهمة مكتملة", cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, Map(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> UpdateTask(Guid id, [FromBody] TaskRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var task = await GetOwnedTaskAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        if (!Enum.TryParse<TaskAudienceType>(request.AudienceType, ignoreCase: true, out var audienceType))
        {
            return BadRequest(new { message = "تصنيف المهمة غير صالح." });
        }

        if (currentMember.Role == MemberRole.GovernorCommitteeCoordinator && audienceType != TaskAudienceType.Members)
        {
            return BadRequest(new { message = "منسق اللجنة يمكنه إسناد المهمة لأعضاء لجنته فقط." });
        }

        if (currentMember.Role == MemberRole.GovernorCoordinator && audienceType != TaskAudienceType.Members)
        {
            return BadRequest(new { message = "منسق المحافظة يمكنه إسناد المهمة فقط لمنسقي اللجان داخل محافظته." });
        }

        var targetRoles = ParseTargetRoles(request.TargetRoles, audienceType, out var targetRolesError);
        if (targetRolesError is not null)
        {
            return BadRequest(new { message = targetRolesError });
        }

        var (targetMemberIds, targetMembersError) = await ParseTargetMemberIdsAsync(currentMember, request.TargetMemberIds, audienceType, cancellationToken);
        if (targetMembersError is not null)
        {
            return BadRequest(new { message = targetMembersError });
        }

        if (audienceType == TaskAudienceType.Members && targetMemberIds.Count == 0)
        {
            return BadRequest(new { message = "اختر عضوًا واحدًا على الأقل." });
        }

        if (audienceType == TaskAudienceType.Roles && targetRoles.Count == 0)
        {
            return BadRequest(new { message = "اختر منصبًا واحدًا على الأقل." });
        }

        var wasCompleted = task.IsCompleted;
        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.AudienceType = audienceType;
        task.IsCompleted = request.IsCompleted;
        task.DueDate = request.DueDate;

        _dbContext.TaskTargetRoles.RemoveRange(task.TargetRoles);
        _dbContext.TaskTargetMembers.RemoveRange(task.TargetMembers);
        task.TargetRoles.Clear();
        task.TargetMembers.Clear();

        foreach (var role in targetRoles)
        {
            _dbContext.TaskTargetRoles.Add(new TaskTargetRole
            {
                Task = task,
                Role = role
            });
        }

        foreach (var targetMemberId in targetMemberIds)
        {
            _dbContext.TaskTargetMembers.Add(new TaskTargetMember
            {
                Task = task,
                MemberId = targetMemberId
            });
        }

        if (!wasCompleted && task.IsCompleted)
        {
            await AddCompletionPointsAsync(task.MemberId, $"إكمال المهمة: {task.Title}", cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(task));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        var task = await GetOwnedTaskAsync(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid? GetMemberId()
    {
        return User.GetMemberId();
    }

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members.FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);
    }

    private async Task<MemberTask?> GetOwnedTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var memberId = GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Tasks
            .Include(task => task.TargetRoles)
            .Include(task => task.TargetMembers)
            .FirstOrDefaultAsync(task => task.Id == taskId && task.MemberId == memberId.Value, cancellationToken);
    }

    private static TaskResponse Map(MemberTask task)
    {
        return new TaskResponse(
            task.Id,
            task.Title,
            task.Description,
            task.AudienceType.ToString(),
            task.IsCompleted,
            task.DueDate,
            task.CreatedAt,
            task.TargetRoles.Select(item => item.Role.ToString()).ToList(),
            task.TargetMembers.Select(item => item.MemberId).ToList());
    }

    private static bool IsVisibleToMember(MemberTask task, Member member)
    {
        if (task.MemberId == member.Id)
        {
            return true;
        }

        return task.AudienceType switch
        {
            TaskAudienceType.All => true,
            TaskAudienceType.Members => task.TargetMembers.Any(target => target.MemberId == member.Id),
            TaskAudienceType.Roles => task.TargetRoles.Any(target => target.Role == member.Role),
            _ => false
        };
    }

    private static List<MemberRole> ParseTargetRoles(IReadOnlyList<string>? roles, TaskAudienceType audienceType, out string? errorMessage)
    {
        errorMessage = null;

        if (audienceType != TaskAudienceType.Roles)
        {
            return [];
        }

        if (roles is null)
        {
            return [];
        }

        var parsedRoles = new List<MemberRole>();
        foreach (var role in roles)
        {
            if (!Enum.TryParse<MemberRole>(role, ignoreCase: true, out var parsedRole))
            {
                errorMessage = "يوجد منصب غير صالح.";
                return [];
            }

            if (!parsedRoles.Contains(parsedRole))
            {
                parsedRoles.Add(parsedRole);
            }
        }

        return parsedRoles;
    }

    private async Task<(List<Guid> memberIds, string? errorMessage)> ParseTargetMemberIdsAsync(Member actor, IReadOnlyList<Guid>? memberIds, TaskAudienceType audienceType, CancellationToken cancellationToken)
    {
        if (audienceType != TaskAudienceType.Members)
        {
            return ([], null);
        }

        if (memberIds is null)
        {
            return ([], null);
        }

        var distinctIds = memberIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return ([], null);
        }

        var targetMembers = await _dbContext.Members
            .AsNoTracking()
            .Where(member => distinctIds.Contains(member.Id))
            .ToListAsync(cancellationToken);

        if (targetMembers.Count != distinctIds.Count)
        {
            return ([], "يوجد عضو غير صالح ضمن القائمة.");
        }

        if (actor.Role == MemberRole.GovernorCommitteeCoordinator)
        {
            var invalidTarget = targetMembers.Any(member =>
                member.Role != MemberRole.CommitteeMember
                || !string.Equals(member.GovernorName, actor.GovernorName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(member.CommitteeName, actor.CommitteeName, StringComparison.OrdinalIgnoreCase));

            if (invalidTarget)
            {
                return ([], "منسق اللجنة يمكنه اختيار أعضاء لجنته داخل محافظته فقط.");
            }
        }

        if (actor.Role == MemberRole.GovernorCoordinator)
        {
            var invalidTarget = targetMembers.Any(member =>
                member.Role != MemberRole.GovernorCommitteeCoordinator
                || !string.Equals(member.GovernorName, actor.GovernorName, StringComparison.OrdinalIgnoreCase));

            if (invalidTarget)
            {
                return ([], "منسق المحافظة يمكنه اختيار منسقي اللجان داخل محافظته فقط.");
            }
        }

        if (actor.Role == MemberRole.CentralMember)
        {
            return ([], "لا يمكن لعضو المركزية إنشاء مهمة لأعضاء محددين.");
        }

        return (distinctIds, null);
    }

    private async Task AddCompletionPointsAsync(Guid memberId, string reason, CancellationToken cancellationToken)
    {
        var member = await _dbContext.Members.FirstOrDefaultAsync(item => item.Id == memberId, cancellationToken);
        if (member is null)
        {
            return;
        }

        member.Points += CompletionPoints;
        _dbContext.PointTransactions.Add(new PointTransaction
        {
            MemberId = member.Id,
            Amount = CompletionPoints,
            Reason = reason
        });
    }
}