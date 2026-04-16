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

        var governorates = await _dbContext.Governorates
            .AsNoTracking()
            .OrderBy(governorate => governorate.Name)
            .Select(governorate => new GovernorateResponse(governorate.Id, governorate.Name))
            .ToListAsync(cancellationToken);

        return Ok(governorates);
    }

    [AllowAnonymous]
    [HttpGet("{governorateId:guid}/committees")]
    public async Task<ActionResult<IEnumerable<CommitteeResponse>>> GetCommittees(Guid governorateId, CancellationToken cancellationToken)
    {
        var committees = await _dbContext.Committees
            .AsNoTracking()
            .Where(committee => committee.GovernorateId == governorateId)
            .OrderBy(committee => committee.Name)
            .Select(committee => new CommitteeResponse(
                committee.Id,
                committee.GovernorateId,
                committee.Governorate.Name,
                committee.Name,
                committee.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(committees);
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

        if (currentMember.Role == MemberRole.GovernorCoordinator && !string.Equals(currentMember.GovernorName, governorate.Name, StringComparison.OrdinalIgnoreCase))
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
            Name = committeeName
        };

        _dbContext.Committees.Add(committee);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCommittees), new { governorateId }, new CommitteeResponse(
            committee.Id,
            committee.GovernorateId,
            governorate.Name,
            committee.Name,
            committee.CreatedAtUtc));
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

    private async Task<Member?> GetCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var memberId = User.GetMemberId();
        if (memberId is null)
        {
            return null;
        }

        return await _dbContext.Members.FirstOrDefaultAsync(member => member.Id == memberId.Value, cancellationToken);
    }
}
