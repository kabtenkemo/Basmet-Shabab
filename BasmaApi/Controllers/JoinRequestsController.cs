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
    private sealed record JoinRequestRow(
        Guid Id,
        string FullName,
        string Email,
        string PhoneNumber,
        string? NationalId,
        DateOnly? BirthDate,
        Guid GovernorateId,
        Guid? CommitteeId,
        string Motivation,
        string? Experience,
        string Status,
        string? AdminNotes,
        Guid? AssignedToMemberId,
        Guid? ReviewedByMemberId,
        DateTime CreatedAtUtc,
        DateTime? ReviewedAtUtc);

    private readonly AppDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<JoinRequestsController> _logger;

    public JoinRequestsController(
        AppDbContext dbContext,
        IPasswordService passwordService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<JoinRequestsController> logger)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
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

        if (!governorate.IsVisibleInJoinForm)
        {
            return BadRequest(new { message = "التقديم على هذه المحافظة متوقف حاليًا. اختر محافظة أخرى." });
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

            if (!committee.IsVisibleInJoinForm)
            {
                return BadRequest(new { message = "التقديم على هذه اللجنة متوقف حاليًا. اختر لجنة أخرى." });
            }

            if (committee.IsStudentClub)
            {
                return BadRequest(new { message = "التقديم على النوادي الطلابية غير متاح من فورم المحافظات." });
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

        async Task<List<JoinRequestRow>> LoadItemsAsync()
        {
            var query = _dbContext.TeamJoinRequests
                .AsNoTracking()
                .Select(item => new JoinRequestRow(
                    item.Id,
                    item.FullName,
                    item.Email,
                    item.PhoneNumber,
                    item.NationalId,
                    item.BirthDate,
                    item.GovernorateId,
                    item.CommitteeId,
                    item.Motivation,
                    item.Experience,
                    item.Status.ToString(),
                    item.AdminNotes,
                    item.AssignedToMemberId,
                    item.ReviewedByMemberId,
                    item.CreatedAtUtc,
                    item.ReviewedAtUtc));

            if (currentMember.Role is not (MemberRole.President or MemberRole.VicePresident))
            {
                var governorName = string.IsNullOrWhiteSpace(currentMember.GovernorName)
                    ? null
                    : currentMember.GovernorName.Trim().ToLowerInvariant();

                if (currentMember.GovernorateId is not null && governorName is not null)
                {
                    query = query.Where(item => item.GovernorateId == currentMember.GovernorateId);
                }
                else if (currentMember.GovernorateId is not null)
                {
                    query = query.Where(item => item.GovernorateId == currentMember.GovernorateId);
                }
            }

            return await query
                .OrderBy(item => item.Status)
                .ThenByDescending(item => item.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        async Task<Dictionary<Guid, string>> LoadGovernorateNamesAsync()
        {
            return await _dbContext.Governorates
                .AsNoTracking()
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        }

        async Task<Dictionary<Guid, string>> LoadCommitteeNamesAsync()
        {
            return await _dbContext.Committees
                .AsNoTracking()
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        }

        async Task<Dictionary<Guid, string>> LoadMemberNamesAsync()
        {
            return await _dbContext.Members
                .AsNoTracking()
                .ToDictionaryAsync(item => item.Id, item => item.FullName, cancellationToken);
        }

        List<JoinRequestRow> items;
        try
        {
            items = await LoadItemsAsync();
        }
        catch (Exception ex) when (DatabaseSchemaEnsurer.IsSchemaMismatch(ex))
        {
            _logger.LogWarning(ex, "Join request list failed due to schema mismatch. Attempting schema repair.");
            try
            {
                DatabaseSchemaEnsurer.EnsureReferenceDataSchema(_dbContext);
                DatabaseSchemaEnsurer.EnsureJoinRequestsSchema(_dbContext);
                items = await LoadItemsAsync();
            }
            catch (Exception repairEx)
            {
                _logger.LogError(repairEx, "Schema repair for join request list failed. Returning empty list to avoid request failure.");
                items = [];
            }
        }

        if (items.Count == 0)
        {
            return Ok(Array.Empty<TeamJoinRequestResponse>());
        }

        Dictionary<Guid, string> governorateNames;
        Dictionary<Guid, string> committeeNames;
        Dictionary<Guid, string> memberNames;

        try
        {
            governorateNames = await LoadGovernorateNamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Governorate name lookup failed for join request list. Names will be omitted.");
            governorateNames = [];
        }

        try
        {
            committeeNames = await LoadCommitteeNamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Committee name lookup failed for join request list. Committee names will be omitted.");
            committeeNames = [];
        }

        try
        {
            memberNames = await LoadMemberNamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Member name lookup failed for join request list. Member names will be omitted.");
            memberNames = [];
        }

        if (currentMember.Role is not (MemberRole.President or MemberRole.VicePresident)
            && currentMember.GovernorateId is null
            && !string.IsNullOrWhiteSpace(currentMember.GovernorName))
        {
            var normalizedGovernorName = currentMember.GovernorName.Trim().ToLowerInvariant();
            var governorateIds = governorateNames
                .Where(item => string.Equals(item.Value.Trim(), normalizedGovernorName, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Key)
                .ToHashSet();

            if (governorateIds.Count > 0)
            {
                items = items.Where(item => governorateIds.Contains(item.GovernorateId)).ToList();
            }
        }

        var responses = items
            .Select(item => new TeamJoinRequestResponse(
                item.Id,
                item.FullName,
                item.Email,
                item.PhoneNumber,
                item.NationalId,
                item.BirthDate,
                item.GovernorateId,
                governorateNames.TryGetValue(item.GovernorateId, out var governorateName) ? governorateName : "غير محددة",
                item.CommitteeId,
                item.CommitteeId.HasValue && committeeNames.TryGetValue(item.CommitteeId.Value, out var committeeName) ? committeeName : null,
                item.Motivation,
                item.Experience,
                item.Status,
                item.AdminNotes,
                item.AssignedToMemberId,
                item.AssignedToMemberId.HasValue && memberNames.TryGetValue(item.AssignedToMemberId.Value, out var assignedName) ? assignedName : null,
                item.ReviewedByMemberId,
                item.ReviewedByMemberId.HasValue && memberNames.TryGetValue(item.ReviewedByMemberId.Value, out var reviewedName) ? reviewedName : null,
                item.CreatedAtUtc,
                item.ReviewedAtUtc))
            .ToList();

        _logger.LogInformation(
            "Join requests list for {MemberId} role {Role} governorateId {GovernorateId} governorName {GovernorName} returned {Count} items.",
            currentMember.Id,
            currentMember.Role,
            currentMember.GovernorateId,
            currentMember.GovernorName,
            responses.Count);

        return Ok(responses);
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

        var item = await _dbContext.TeamJoinRequests
            .Include(joinRequest => joinRequest.Governorate)
            .Include(joinRequest => joinRequest.Committee)
            .FirstOrDefaultAsync(joinRequest => joinRequest.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (currentMember.Role is not (MemberRole.President or MemberRole.VicePresident)
            && !IsSameGovernorate(currentMember, item))
        {
            return Forbid();
        }

        if (nextStatus == JoinRequestStatus.Rejected)
        {
            _dbContext.TeamJoinRequests.Remove(item);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "تم رفض الطلب وحذفه من القائمة." });
        }

        var normalizedEmail = item.Email.Trim().ToLowerInvariant();
        var emailExists = await _dbContext.Members.AnyAsync(member => member.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Conflict(new { message = "هذا البريد مرتبط بعضو مسجل بالفعل." });
        }

        if (!string.IsNullOrWhiteSpace(item.NationalId))
        {
            var nationalIdExists = await _dbContext.Members.AnyAsync(member => member.NationalId == item.NationalId, cancellationToken);
            if (nationalIdExists)
            {
                return Conflict(new { message = "الرقم القومي مسجل بالفعل." });
            }
        }

        string initialPassword;
        try
        {
            initialPassword = ResolveDefaultMemberPassword();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Cannot accept join request {JoinRequestId} because default password is not configured.", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "تعذر قبول الطلب بسبب إعدادات كلمة المرور الافتراضية. تواصل مع مسؤول النظام." });
        }

        var newMember = new Member
        {
            FullName = item.FullName.Trim(),
            Email = normalizedEmail,
            NationalId = item.NationalId,
            BirthDate = item.BirthDate,
            Role = MemberRole.CommitteeMember,
            GovernorateId = item.GovernorateId,
            CommitteeId = item.CommitteeId,
            GovernorName = item.Governorate.Name,
            CommitteeName = item.Committee?.Name,
            CreatedByMemberId = currentMember.Id,
            PasswordHash = _passwordService.HashPassword(initialPassword),
            MustChangePassword = true
        };

        _dbContext.Members.Add(newMember);

        _dbContext.TeamJoinRequests.Remove(item);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "تم قبول الطلب وإنشاء حساب للمتقدم وحذفه من قائمة الطلبات." });
    }

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        var member = await _dbContext.Members
            .Include(member => member.PermissionGrants)
            .FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);

        if (member is not null
            && member.GovernorateId is null
            && !string.IsNullOrWhiteSpace(member.GovernorName))
        {
            var normalizedName = member.GovernorName.Trim().ToLower();
            var governorate = await _dbContext.Governorates
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Name.ToLower() == normalizedName, cancellationToken);

            if (governorate is not null)
            {
                member.GovernorateId = governorate.Id;
                member.GovernorName = governorate.Name;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return member;
    }

    private string ResolveDefaultMemberPassword()
    {
        var configuredPassword = _configuration["Members:DefaultPassword"];
        if (!string.IsNullOrWhiteSpace(configuredPassword))
        {
            return configuredPassword;
        }

        if (_environment.IsDevelopment())
        {
            return "Test123";
        }

        throw new InvalidOperationException("Members:DefaultPassword must be configured outside Development.");
    }

    private static TeamJoinRequestResponse MapResponse(TeamJoinRequest item)
    {
        var governorateName = item.Governorate?.Name ?? "غير محددة";
        var committeeName = item.Committee?.Name;

        return new TeamJoinRequestResponse(
            item.Id,
            item.FullName,
            item.Email,
            item.PhoneNumber,
            item.NationalId,
            item.BirthDate,
            item.GovernorateId,
            governorateName,
            item.CommitteeId,
            committeeName,
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

    private static bool IsSameGovernorate(Member member, TeamJoinRequest request)
    {
        if (member.GovernorateId is not null && member.GovernorateId == request.GovernorateId)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(member.GovernorName) && request.Governorate is not null)
        {
            return string.Equals(member.GovernorName.Trim(), request.Governorate.Name.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
