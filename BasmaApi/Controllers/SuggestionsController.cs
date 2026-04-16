using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using BasmaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BasmaApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class SuggestionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SuggestionsController> _logger;

    public SuggestionsController(AppDbContext dbContext, ILogger<SuggestionsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SuggestionWithVoteResponse>>> List(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var query = _dbContext.Suggestions
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<SuggestionStatus>(status, true, out var statusEnum))
            {
                query = query.Where(s => s.Status == statusEnum);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(s =>
                EF.Functions.Like(s.Title, $"%{searchTerm}%") ||
                EF.Functions.Like(s.Description, $"%{searchTerm}%"));
        }

        var suggestions = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SuggestionWithVoteResponse(
                s.Id,
                s.Title,
                s.Description,
                s.Status.ToString(),
                s.Votes.Count(v => v.IsAcceptance),
                s.Votes.Count(v => !v.IsAcceptance),
                s.Votes.Where(v => v.VotedByMemberId == currentMemberId)
                    .Select(v => (bool?)v.IsAcceptance)
                    .FirstOrDefault(),
                s.CreatedByMember != null ? s.CreatedByMember.FullName : "غير معروف",
                s.CreatedByMember != null ? s.CreatedByMember.Role.ToString() : "غير معروف",
                s.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(suggestions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SuggestionWithVoteResponse>> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var suggestion = await _dbContext.Suggestions
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SuggestionWithVoteResponse(
                s.Id,
                s.Title,
                s.Description,
                s.Status.ToString(),
                s.Votes.Count(v => v.IsAcceptance),
                s.Votes.Count(v => !v.IsAcceptance),
                s.Votes.Where(v => v.VotedByMemberId == currentMemberId)
                    .Select(v => (bool?)v.IsAcceptance)
                    .FirstOrDefault(),
                s.CreatedByMember != null ? s.CreatedByMember.FullName : "غير معروف",
                s.CreatedByMember != null ? s.CreatedByMember.Role.ToString() : "غير معروف",
                s.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (suggestion is null)
            return NotFound();

        return Ok(suggestion);
    }

    [HttpPost]
    public async Task<ActionResult<SuggestionResponse>> Create(
        SuggestionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var currentMember = await _dbContext.Members.FindAsync(new object[] { currentMemberId }, cancellationToken);
        if (currentMember is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("العنوان والوصف مطلوبان");

        var suggestion = new Suggestion
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByMemberId = currentMemberId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = SuggestionStatus.Open,
            AcceptanceCount = 0,
            RejectionCount = 0
        };

        _dbContext.Suggestions.Add(suggestion);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Failed to create suggestion for member {MemberId}", currentMemberId);

            if (IsMissingSuggestionsSchema(dbEx))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "قاعدة البيانات غير محدثة لميزة المقترحات. يرجى تشغيل EF migrations على بيئة الإنتاج.");
            }

            return StatusCode(StatusCodes.Status500InternalServerError,
                "تعذر حفظ المقترح بسبب خطأ في قاعدة البيانات.");
        }

        return CreatedAtAction(nameof(Get), new { id = suggestion.Id }, MapToResponse(suggestion, currentMember));
    }

    [HttpPost("{id}/vote")]
    public async Task<ActionResult<SuggestionWithVoteResponse>> Vote(
        Guid id,
        SuggestionVoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var currentMemberExists = await _dbContext.Members
            .AsNoTracking()
            .AnyAsync(member => member.Id == currentMemberId, cancellationToken);

        if (!currentMemberExists)
        {
            return Unauthorized("بيانات الجلسة غير متزامنة مع المستخدم الحالي. يرجى تسجيل الدخول مرة أخرى.");
        }

        var suggestion = await _dbContext.Suggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (suggestion is null)
            return NotFound();

        // Check if user already voted
        var existingVote = await _dbContext.SuggestionVotes
            .FirstOrDefaultAsync(v => v.SuggestionId == id && v.VotedByMemberId == currentMemberId, cancellationToken);

        var effectiveVote = request.IsAcceptance;
        if (existingVote is not null)
        {
            if (existingVote.IsAcceptance != request.IsAcceptance)
            {
                existingVote.IsAcceptance = request.IsAcceptance;
            }
            else
            {
                effectiveVote = existingVote.IsAcceptance;
            }
        }
        else
        {
            var vote = new SuggestionVote
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                SuggestionId = id,
                VotedByMemberId = currentMemberId,
                IsAcceptance = request.IsAcceptance
            };

            _dbContext.SuggestionVotes.Add(vote);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Failed to save vote for suggestion {SuggestionId} by member {MemberId}", id, currentMemberId);

            if (IsVoteMemberForeignKeyViolation(dbEx))
            {
                return Unauthorized("بيانات الجلسة غير متزامنة مع المستخدم الحالي. يرجى تسجيل الدخول مرة أخرى.");
            }

            if (IsMissingSuggestionsSchema(dbEx))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "قاعدة البيانات غير محدثة لميزة المقترحات. يرجى تشغيل EF migrations على بيئة الإنتاج.");
            }

            return StatusCode(StatusCodes.Status500InternalServerError,
                "تعذر حفظ التصويت بسبب خطأ في قاعدة البيانات.");
        }

        var response = await _dbContext.Suggestions
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SuggestionWithVoteResponse(
                s.Id,
                s.Title,
                s.Description,
                s.Status.ToString(),
                s.Votes.Count(v => v.IsAcceptance),
                s.Votes.Count(v => !v.IsAcceptance),
                effectiveVote,
                s.CreatedByMember != null ? s.CreatedByMember.FullName : "غير معروف",
                s.CreatedByMember != null ? s.CreatedByMember.Role.ToString() : "غير معروف",
                s.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(response!);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "President,VicePresident,CentralMember")]
    public async Task<ActionResult<SuggestionResponse>> ChangeStatus(
        Guid id,
        SuggestionStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentMemberId = GetCurrentMemberId();
        var currentMember = await _dbContext.Members.FindAsync(new object[] { currentMemberId }, cancellationToken);

        if (currentMember is null || !AccessControl.CanManageUsers(currentMember))
            return Forbid();

        if (!Enum.TryParse<SuggestionStatus>(request.NewStatus, true, out var newStatus))
            return BadRequest("حالة غير صحيحة");

        var suggestion = await _dbContext.Suggestions
            .Include(s => s.CreatedByMember)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (suggestion is null)
            return NotFound();

        suggestion.Status = newStatus;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(suggestion, suggestion.CreatedByMember!));
    }

    private Guid GetCurrentMemberId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim?.Value, out var id) ? id : Guid.Empty;
    }

    private static bool IsMissingSuggestionsSchema(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("Suggestions", StringComparison.OrdinalIgnoreCase)
                || message.Contains("SuggestionVotes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsVoteMemberForeignKeyViolation(DbUpdateException exception)
    {
        var message = exception.ToString();

        return message.Contains("FK_SuggestionVotes_Members_VotedByMemberId", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                && message.Contains("VotedByMemberId", StringComparison.OrdinalIgnoreCase)
                && message.Contains("SuggestionVotes", StringComparison.OrdinalIgnoreCase));
    }

    private SuggestionResponse MapToResponse(Suggestion suggestion, Member createdByMember)
    {
        return new SuggestionResponse(
            suggestion.Id,
            suggestion.Title,
            suggestion.Description,
            suggestion.Status.ToString(),
            suggestion.AcceptanceCount,
            suggestion.RejectionCount,
            createdByMember.FullName,
            createdByMember.Role.ToString(),
            suggestion.CreatedAtUtc
        );
    }
}
