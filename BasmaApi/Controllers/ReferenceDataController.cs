using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/governorates")]
public sealed class ReferenceDataController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ReferenceDataController> _logger;

    private static readonly string[] DefaultGovernorateNames =
    {
        "القاهرة",
        "الإسكندرية",
        "الجيزة",
        "القليوبية",
        "الشرقية",
        "الغربية",
        "المنوفية",
        "الدقهلية",
        "البحيرة",
        "كفر الشيخ",
        "دمياط",
        "بورسعيد",
        "السويس",
        "الإسماعيلية",
        "شمال سيناء",
        "جنوب سيناء",
        "الفيوم",
        "بني سويف",
        "المنيا",
        "أسيوط",
        "سوهاج",
        "قنا",
        "الأقصر",
        "أسوان",
        "البحر الأحمر",
        "الوادي الجديد",
        "مطروح"
    };

    public ReferenceDataController(AppDbContext dbContext, ILogger<ReferenceDataController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GovernorateResponse>>> GetGovernorates(CancellationToken cancellationToken)
    {
        await EnsureGovernoratesSeededAsync(cancellationToken);

        var governoratesQuery = _dbContext.Governorates.AsNoTracking();
        if (User.Identity?.IsAuthenticated != true)
        {
            governoratesQuery = governoratesQuery.Where(governorate => governorate.IsVisibleInJoinForm);
        }

        var governorates = await governoratesQuery
            .OrderBy(governorate => governorate.Name)
            .Select(governorate => new GovernorateResponse(governorate.Id, governorate.Name, governorate.IsVisibleInJoinForm))
            .ToListAsync(cancellationToken);

        return Ok(governorates);
    }

    [AllowAnonymous]
    [HttpGet("{governorateId:guid}/committees")]
    public async Task<ActionResult<IEnumerable<CommitteeResponse>>> GetCommittees(Guid governorateId, [FromQuery] string? kind, CancellationToken cancellationToken)
    {
        var normalizedKind = kind?.Trim().ToLowerInvariant();

        async Task<List<CommitteeResponse>> LoadCommitteesAsync()
        {
            var sql = @"
SELECT
    c.Id,
    c.GovernorateId,
    COALESCE(g.Name, N'غير محددة') AS GovernorateName,
    c.Name,
    c.IsStudentClub,
    c.IsVisibleInJoinForm,
    c.CreatedAtUtc
FROM dbo.Committees AS c
LEFT JOIN dbo.Governorates AS g ON g.Id = c.GovernorateId
WHERE
    c.GovernorateId = @GovernorateId
    OR (
        NOT EXISTS (SELECT 1 FROM dbo.Committees WHERE GovernorateId = @GovernorateId)
        AND EXISTS (
            SELECT 1
            FROM dbo.Governorates AS selectedGovernorate
            WHERE selectedGovernorate.Id = @GovernorateId
              AND LOWER(selectedGovernorate.Name) = LOWER(COALESCE(g.Name, N''))
        )
    )";

            if (normalizedKind == "club")
            {
                sql += @" AND c.IsStudentClub = 1";
            }
            else if (normalizedKind != "all")
            {
                sql += @" AND c.IsStudentClub = 0";
            }

            sql += @" ORDER BY c.Name";

            var connection = _dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@GovernorateId";
            parameter.Value = governorateId;
            command.Parameters.Add(parameter);

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<CommitteeResponse>();

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new CommitteeResponse(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetBoolean(4),
                    reader.GetBoolean(5),
                    reader.GetDateTime(6)));
            }

            return results;
        }

        try
        {
            return Ok(await LoadCommitteesAsync());
        }
        catch (Exception ex) when (DatabaseSchemaEnsurer.IsSchemaMismatch(ex))
        {
            _logger.LogWarning(ex, "Committee list failed due to schema mismatch. Attempting schema repair.");
            try
            {
                DatabaseSchemaEnsurer.EnsureReferenceDataSchema(_dbContext);
                return Ok(await LoadCommitteesAsync());
            }
            catch (Exception repairEx)
            {
                _logger.LogError(repairEx, "Schema repair for committee list failed. Returning empty list to avoid request failure.");
                return Ok(Array.Empty<CommitteeResponse>());
            }
        }
    }

    [HttpPatch("{governorateId:guid}/join-visibility")]
    public async Task<ActionResult<GovernorateResponse>> UpdateJoinVisibility(Guid governorateId, [FromBody] GovernorateJoinVisibilityRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanManageJoinVisibility(currentMember))
        {
            return Forbid();
        }

        if (currentMember.Role == MemberRole.GovernorCommitteeCoordinator)
        {
            return Forbid();
        }

        var governorate = await _dbContext.Governorates.FirstOrDefaultAsync(item => item.Id == governorateId, cancellationToken);
        if (governorate is null)
        {
            return NotFound(new { message = "المحافظة غير موجودة." });
        }

        var canManageAnyGovernorate = currentMember.Role is MemberRole.President or MemberRole.VicePresident;
        if (!canManageAnyGovernorate && !IsSameGovernorate(currentMember, governorate))
        {
            return Forbid();
        }

        governorate.IsVisibleInJoinForm = request.IsVisibleInJoinForm;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Join form visibility for governorate {GovernorateId} set to {IsVisibleInJoinForm} by member {MemberId}.",
            governorateId,
            governorate.IsVisibleInJoinForm,
            currentMember.Id);

        return Ok(new GovernorateResponse(governorate.Id, governorate.Name, governorate.IsVisibleInJoinForm));
    }

    [HttpPatch("{governorateId:guid}/committees/{committeeId:guid}/join-visibility")]
    public async Task<ActionResult<CommitteeResponse>> UpdateCommitteeJoinVisibility(Guid governorateId, Guid committeeId, [FromBody] CommitteeJoinVisibilityRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        var canManageAsCommitteeCoordinator = currentMember.Role == MemberRole.GovernorCommitteeCoordinator;
        if (!canManageAsCommitteeCoordinator && !AccessControl.CanManageJoinVisibility(currentMember))
        {
            return Forbid();
        }

        var committee = await _dbContext.Committees
            .Include(item => item.Governorate)
            .FirstOrDefaultAsync(item => item.Id == committeeId && item.GovernorateId == governorateId, cancellationToken);

        if (committee is null)
        {
            return NotFound(new { message = "اللجنة غير موجودة." });
        }

        var canManageAnyGovernorate = currentMember.Role is MemberRole.President or MemberRole.VicePresident;
        if (!canManageAnyGovernorate)
        {
            if (canManageAsCommitteeCoordinator)
            {
                if (!IsSameCommittee(currentMember, committee))
                {
                    return Forbid();
                }
            }
            else if (!IsSameGovernorate(currentMember, committee.Governorate))
            {
                return Forbid();
            }
        }

        committee.IsVisibleInJoinForm = request.IsVisibleInJoinForm;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Join form visibility for committee {CommitteeId} in governorate {GovernorateId} set to {IsVisibleInJoinForm} by member {MemberId}.",
            committeeId,
            governorateId,
            committee.IsVisibleInJoinForm,
            currentMember.Id);

        return Ok(new CommitteeResponse(
            committee.Id,
            committee.GovernorateId,
            committee.Governorate.Name,
            committee.Name,
            committee.IsStudentClub,
            committee.IsVisibleInJoinForm,
            committee.CreatedAtUtc));
    }

    [HttpPost("{governorateId:guid}/committees")]
    public async Task<ActionResult<CommitteeResponse>> CreateCommittee(Guid governorateId, [FromBody] CommitteeCreateRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanManageCommitteeCatalog(currentMember))
        {
            return Forbid();
        }

        var governorate = await _dbContext.Governorates.FirstOrDefaultAsync(item => item.Id == governorateId, cancellationToken);
        if (governorate is null)
        {
            return NotFound(new { message = "المحافظة غير موجودة." });
        }

        if (currentMember.Role == MemberRole.GovernorCoordinator && !IsSameGovernorate(currentMember, governorate))
        {
            return Forbid();
        }

        var committeeName = request.Name.Trim();
        var exists = await _dbContext.Committees.AnyAsync(committee => committee.GovernorateId == governorate.Id && committee.Name == committeeName, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "هذه اللجنة موجودة بالفعل داخل المحافظة المحددة." });
        }

        var committee = new Committee
        {
            GovernorateId = governorate.Id,
            Name = committeeName,
            IsStudentClub = request.IsStudentClub
        };

        _dbContext.Committees.Add(committee);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCommittees), new { governorateId }, new CommitteeResponse(
            committee.Id,
            committee.GovernorateId,
            governorate.Name,
            committee.Name,
            committee.IsStudentClub,
            committee.IsVisibleInJoinForm,
            committee.CreatedAtUtc));
    }

    [HttpDelete("{governorateId:guid}/committees/{committeeId:guid}")]
    public async Task<IActionResult> DeleteCommittee(Guid governorateId, Guid committeeId, CancellationToken cancellationToken)
    {
        var currentMember = await GetCurrentMemberAsync(cancellationToken);
        if (currentMember is null)
        {
            return Unauthorized();
        }

        if (!AccessControl.CanManageCommitteeCatalog(currentMember))
        {
            return Forbid();
        }

        var governorate = await _dbContext.Governorates.FirstOrDefaultAsync(item => item.Id == governorateId, cancellationToken);
        if (governorate is null)
        {
            return NotFound(new { message = "المحافظة غير موجودة." });
        }

        if (currentMember.Role == MemberRole.GovernorCoordinator
            && !IsSameGovernorate(currentMember, governorate))
        {
            return Forbid();
        }

        var committee = await _dbContext.Committees.FirstOrDefaultAsync(item => item.Id == committeeId && item.GovernorateId == governorateId, cancellationToken);
        if (committee is null)
        {
            return NotFound(new { message = "اللجنة غير موجودة." });
        }

        var hasMembers = await _dbContext.Members.AnyAsync(member => member.CommitteeId == committeeId, cancellationToken);
        if (hasMembers)
        {
            return Conflict(new { message = "لا يمكن حذف اللجنة لوجود أعضاء مرتبطين بها." });
        }

        var hasJoinRequests = await _dbContext.TeamJoinRequests.AnyAsync(request => request.CommitteeId == committeeId, cancellationToken);
        if (hasJoinRequests)
        {
            return Conflict(new { message = "لا يمكن حذف اللجنة لوجود طلبات انضمام مرتبطة بها." });
        }

        _dbContext.Committees.Remove(committee);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task EnsureGovernoratesSeededAsync(CancellationToken cancellationToken)
    {
        var existingNames = await _dbContext.Governorates
            .AsNoTracking()
            .Select(governorate => governorate.Name)
            .ToListAsync(cancellationToken);

        var knownNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var missingGovernorates = DefaultGovernorateNames
            .Where(name => !knownNames.Contains(name))
            .ToList();

        if (missingGovernorates.Count == 0)
        {
            return;
        }

        foreach (var governorateName in missingGovernorates)
        {
            _dbContext.Governorates.Add(new Governorate { Name = governorateName });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogInformation(ex, "Governorates seed encountered a concurrent insert conflict. Request will continue.");
            // Another request may have seeded data concurrently.
            _dbContext.ChangeTracker.Clear();
        }
    }

    private static bool IsSameGovernorate(Member member, Governorate governorate)
    {
        if (member.GovernorateId is not null && member.GovernorateId == governorate.Id)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(member.GovernorName))
        {
            return string.Equals(member.GovernorName.Trim(), governorate.Name.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsSameCommittee(Member member, Committee committee)
    {
        if (member.CommitteeId is not null && member.CommitteeId == committee.Id)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(member.CommitteeName))
        {
            return string.Equals(member.CommitteeName.Trim(), committee.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                && IsSameGovernorate(member, committee.Governorate);
        }

        return false;
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
}
