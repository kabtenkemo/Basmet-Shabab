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
            var hasIsStudentClubColumn = await CommitteesColumnExistsAsync("IsStudentClub", cancellationToken);
            var hasIsVisibleInJoinFormColumn = await CommitteesColumnExistsAsync("IsVisibleInJoinForm", cancellationToken);
            var hasCreatedAtUtcColumn = await CommitteesColumnExistsAsync("CreatedAtUtc", cancellationToken);

            var isStudentClubSelect = hasIsStudentClubColumn
                ? "c.IsStudentClub"
                : "CAST(0 AS bit) AS IsStudentClub";
            var isVisibleInJoinFormSelect = hasIsVisibleInJoinFormColumn
                ? "c.IsVisibleInJoinForm"
                : "CAST(1 AS bit) AS IsVisibleInJoinForm";
            var createdAtUtcSelect = hasCreatedAtUtcColumn
                ? "c.CreatedAtUtc"
                : "SYSUTCDATETIME() AS CreatedAtUtc";

            var sql = @"
SELECT
    c.Id,
    c.GovernorateId,
    COALESCE(g.Name, N'غير محددة') AS GovernorateName,
    c.Name,
    " + isStudentClubSelect + @",
    " + isVisibleInJoinFormSelect + @",
    " + createdAtUtcSelect + @"
FROM dbo.Committees AS c
LEFT JOIN dbo.Governorates AS g ON g.Id = c.GovernorateId
WHERE (
    c.GovernorateId = @GovernorateId
    OR (
        NOT EXISTS (SELECT 1 FROM dbo.Committees WHERE GovernorateId = @GovernorateId)
        AND EXISTS (
            SELECT 1
            FROM dbo.Governorates AS selectedGovernorate
            WHERE selectedGovernorate.Id = @GovernorateId
              AND LOWER(selectedGovernorate.Name) = LOWER(COALESCE(g.Name, N''))
        )
    ))";

            if (normalizedKind == "club")
            {
                if (hasIsStudentClubColumn)
                {
                    sql += @" AND c.IsStudentClub = 1";
                }
                else
                {
                    return new List<CommitteeResponse>();
                }
            }
            else if (normalizedKind != "all")
            {
                if (hasIsStudentClubColumn)
                {
                    sql += @" AND c.IsStudentClub = 0";
                }
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

        async Task<ActionResult<GovernorateResponse>> ExecuteAsync()
        {
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

        try
        {
            return await ExecuteAsync();
        }
        catch (Exception ex) when (DatabaseSchemaEnsurer.IsSchemaMismatch(ex))
        {
            _logger.LogWarning(ex, "Governorate join visibility update failed due to schema mismatch. Attempting schema repair. GovernorateId={GovernorateId}", governorateId);

            DatabaseSchemaEnsurer.EnsureReferenceDataSchema(_dbContext);
            _dbContext.ChangeTracker.Clear();

            return await ExecuteAsync();
        }
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

        async Task<ActionResult<CommitteeResponse>> ExecuteAsync()
        {
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
                else if (committee.Governorate is null || !IsSameGovernorate(currentMember, committee.Governorate))
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
                committee.Governorate?.Name ?? "غير محددة",
                committee.Name,
                committee.IsStudentClub,
                committee.IsVisibleInJoinForm,
                committee.CreatedAtUtc));
        }

        try
        {
            return await ExecuteAsync();
        }
        catch (Exception ex) when (DatabaseSchemaEnsurer.IsSchemaMismatch(ex))
        {
            _logger.LogWarning(ex, "Committee join visibility update failed due to schema mismatch. Attempting schema repair. GovernorateId={GovernorateId} CommitteeId={CommitteeId}", governorateId, committeeId);

            DatabaseSchemaEnsurer.EnsureReferenceDataSchema(_dbContext);
            _dbContext.ChangeTracker.Clear();

            return await ExecuteAsync();
        }
    }

    [HttpPost("{governorateId:guid}/committees")]
    public async Task<ActionResult<CommitteeResponse>> CreateCommittee(Guid governorateId, [FromBody] CommitteeCreateRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "بيانات إنشاء اللجنة غير صالحة." });
        }

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

        var committeeName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(committeeName))
        {
            return BadRequest(new { message = "اسم اللجنة مطلوب." });
        }

        var normalizedCommitteeName = committeeName.ToLowerInvariant();
        var exists = await _dbContext.Committees.AnyAsync(
            committee => committee.GovernorateId == governorate.Id && committee.Name.ToLower() == normalizedCommitteeName,
            cancellationToken);
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
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseSchemaEnsurer.IsSchemaMismatch(ex))
        {
            _logger.LogWarning(ex, "Committee creation failed due to schema mismatch. Attempting schema repair.");
            try
            {
                DatabaseSchemaEnsurer.EnsureReferenceDataSchema(_dbContext);

                var duplicateAfterRepair = await _dbContext.Committees.AnyAsync(
                    item => item.GovernorateId == governorate.Id && item.Name.ToLower() == normalizedCommitteeName,
                    cancellationToken);
                if (duplicateAfterRepair)
                {
                    return Conflict(new { message = "هذه اللجنة موجودة بالفعل داخل المحافظة المحددة." });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception repairEx) when (DatabaseSchemaEnsurer.IsSchemaMismatch(repairEx))
            {
                _logger.LogWarning(repairEx, "Committee creation still failing after schema repair. Falling back to legacy insert.");

                var hasIsStudentClubColumn = await CommitteesColumnExistsAsync("IsStudentClub", cancellationToken);
                var hasIsVisibleInJoinFormColumn = await CommitteesColumnExistsAsync("IsVisibleInJoinForm", cancellationToken);
                var hasCreatedAtUtcColumn = await CommitteesColumnExistsAsync("CreatedAtUtc", cancellationToken);

                try
                {
                    await InsertCommitteeLegacyAsync(
                        committee,
                        request.IsStudentClub,
                        hasIsStudentClubColumn,
                        hasIsVisibleInJoinFormColumn,
                        hasCreatedAtUtcColumn,
                        cancellationToken);

                    _dbContext.Entry(committee).State = EntityState.Detached;

                    return CreatedAtAction(nameof(GetCommittees), new { governorateId }, new CommitteeResponse(
                        committee.Id,
                        committee.GovernorateId,
                        governorate.Name,
                        committee.Name,
                        hasIsStudentClubColumn ? request.IsStudentClub : false,
                        true,
                        hasCreatedAtUtcColumn ? committee.CreatedAtUtc : DateTime.UtcNow));
                }
                catch (DbUpdateException dbEx) when (DatabaseSchemaEnsurer.IsUniqueConstraintViolation(dbEx))
                {
                    _logger.LogWarning(dbEx, "Duplicate committee create attempt (legacy fallback) for governorate {GovernorateId} and name {CommitteeName}.", governorate.Id, committeeName);
                    return Conflict(new { message = "هذه اللجنة موجودة بالفعل داخل المحافظة المحددة." });
                }
                catch (SqlException sqlEx) when (DatabaseSchemaEnsurer.IsUniqueConstraintViolation(sqlEx))
                {
                    _logger.LogWarning(sqlEx, "Duplicate committee create attempt (legacy fallback) for governorate {GovernorateId} and name {CommitteeName}.", governorate.Id, committeeName);
                    return Conflict(new { message = "هذه اللجنة موجودة بالفعل داخل المحافظة المحددة." });
                }
            }
        }
        catch (DbUpdateException dbEx) when (DatabaseSchemaEnsurer.IsUniqueConstraintViolation(dbEx))
        {
            _logger.LogWarning(dbEx, "Duplicate committee create attempt for governorate {GovernorateId} and name {CommitteeName}.", governorate.Id, committeeName);
            return Conflict(new { message = "هذه اللجنة موجودة بالفعل داخل المحافظة المحددة." });
        }

        return CreatedAtAction(nameof(GetCommittees), new { governorateId }, new CommitteeResponse(
            committee.Id,
            committee.GovernorateId,
            governorate.Name,
            committee.Name,
            committee.IsStudentClub,
            committee.IsVisibleInJoinForm,
            committee.CreatedAtUtc));
    }

    private async Task<bool> CommitteesColumnExistsAsync(string columnName, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN COL_LENGTH('dbo.Committees', @ColumnName) IS NULL THEN 0 ELSE 1 END";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@ColumnName";
        parameter.Value = columnName;
        command.Parameters.Add(parameter);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private async Task InsertCommitteeLegacyAsync(
        Committee committee,
        bool isStudentClub,
        bool hasIsStudentClubColumn,
        bool hasIsVisibleInJoinFormColumn,
        bool hasCreatedAtUtcColumn,
        CancellationToken cancellationToken)
    {
        var columns = new List<string> { "Id", "GovernorateId", "Name" };
        var values = new List<string> { "@Id", "@GovernorateId", "@Name" };
        var parameters = new List<SqlParameter>
        {
            new("@Id", committee.Id),
            new("@GovernorateId", committee.GovernorateId),
            new("@Name", committee.Name)
        };

        if (hasIsStudentClubColumn)
        {
            columns.Add("IsStudentClub");
            values.Add("@IsStudentClub");
            parameters.Add(new SqlParameter("@IsStudentClub", isStudentClub));
        }

        if (hasIsVisibleInJoinFormColumn)
        {
            columns.Add("IsVisibleInJoinForm");
            values.Add("@IsVisibleInJoinForm");
            parameters.Add(new SqlParameter("@IsVisibleInJoinForm", true));
        }

        if (hasCreatedAtUtcColumn)
        {
            columns.Add("CreatedAtUtc");
            values.Add("@CreatedAtUtc");
            parameters.Add(new SqlParameter("@CreatedAtUtc", committee.CreatedAtUtc));
        }

        var sql = $"INSERT INTO dbo.Committees ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters.Cast<object>().ToArray(), cancellationToken);
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

        try
        {
            var deleted = await TryDeleteCommitteeAsync(committee, committeeId, cancellationToken);
            if (!deleted)
            {
                return Conflict(new { message = "لا يمكن حذف اللجنة لوجود بيانات مرتبطة بها." });
            }
        }
        catch (Exception ex) when (DatabaseSchemaEnsurer.IsSchemaMismatch(ex))
        {
            _logger.LogWarning(ex, "Committee delete failed due to schema mismatch. Attempting schema repair. CommitteeId={CommitteeId}", committeeId);

            DatabaseSchemaEnsurer.EnsureReferenceDataSchema(_dbContext);
            DatabaseSchemaEnsurer.EnsureJoinRequestsSchema(_dbContext);

            _dbContext.ChangeTracker.Clear();

            var committeeAfterRepair = await _dbContext.Committees
                .FirstOrDefaultAsync(item => item.Id == committeeId && item.GovernorateId == governorateId, cancellationToken);
            if (committeeAfterRepair is null)
            {
                return NotFound(new { message = "اللجنة غير موجودة." });
            }

            var deleted = await TryDeleteCommitteeAsync(committeeAfterRepair, committeeId, cancellationToken);
            if (!deleted)
            {
                return Conflict(new { message = "لا يمكن حذف اللجنة لوجود بيانات مرتبطة بها." });
            }
        }

        return NoContent();
    }

    private async Task<bool> TryDeleteCommitteeAsync(Committee committee, Guid committeeId, CancellationToken cancellationToken)
    {
        var hasMembersCommitteeColumn = await TableColumnExistsAsync("dbo.Members", "CommitteeId", cancellationToken);
        var hasMembers = hasMembersCommitteeColumn
            && await _dbContext.Members.AnyAsync(member => member.CommitteeId == committeeId, cancellationToken);
        if (hasMembers)
        {
            return false;
        }

        var hasJoinRequestsCommitteeColumn = await TableColumnExistsAsync("dbo.TeamJoinRequests", "CommitteeId", cancellationToken);
        var hasJoinRequests = hasJoinRequestsCommitteeColumn
            && await _dbContext.TeamJoinRequests.AnyAsync(request => request.CommitteeId == committeeId, cancellationToken);
        if (hasJoinRequests)
        {
            return false;
        }

        _dbContext.Committees.Remove(committee);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Committee delete blocked by related data. CommitteeId={CommitteeId}", committeeId);
            return false;
        }
        catch (SqlException ex) when (IsForeignKeyViolation(ex))
        {
            _logger.LogWarning(ex, "Committee delete blocked by FK constraint. CommitteeId={CommitteeId}", committeeId);
            return false;
        }
    }

    private static bool IsForeignKeyViolation(SqlException exception)
    {
        foreach (SqlError error in exception.Errors)
        {
            if (error.Number == 547)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TableColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CASE
    WHEN OBJECT_ID(@TableName, 'U') IS NULL THEN 0
    WHEN COL_LENGTH(@TableName, @ColumnName) IS NULL THEN 0
    ELSE 1
END";

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@TableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@ColumnName";
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
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
