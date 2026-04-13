using BasmaApi.Contracts;
using BasmaApi.Data;
using BasmaApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasmaApi.Controllers;

[ApiController]
[Route("api/suggestions")]
[Authorize]
public sealed class SuggestionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SuggestionsController(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Get all suggestions with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SuggestionItemResponse>>> GetSuggestions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var skip = (page - 1) * pageSize;

        var suggestions = await _db.Suggestions
            .Include(s => s.CreatedByMember)
            .Include(s => s.Votes)
            .OrderByDescending(s => s.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        var response = suggestions.Select(s => new SuggestionItemResponse(
            SuggestionId: s.Id,
            Title: s.Title,
            Description: s.Description,
            CreatedByName: s.CreatedByMember?.FullName ?? "مستخدم محذوف",
            CreatedAtUtc: s.CreatedAtUtc,
            AcceptCount: s.AcceptCount,
            RejectCount: s.RejectCount,
            UserVote: s.Votes.FirstOrDefault(v => v.MemberId == currentMemberId)?.IsAccepted
        )).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Create a new suggestion
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SuggestionDetailResponse>> CreateSuggestion(
        [FromBody] SuggestionCreateRequest request)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var suggestion = new Suggestion
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CreatedByMemberId = currentMemberId,
            CreatedAtUtc = DateTime.UtcNow,
            AcceptCount = 0,
            RejectCount = 0
        };

        _db.Suggestions.Add(suggestion);
        await _db.SaveChangesAsync();

        // Reload to get related data
        await _db.Entry(suggestion).Reference(s => s.CreatedByMember).LoadAsync();

        return CreatedAtAction(nameof(GetSuggestions), new { id = suggestion.Id },
            new SuggestionDetailResponse(
                SuggestionId: suggestion.Id,
                Title: suggestion.Title,
                Description: suggestion.Description,
                CreatedByName: suggestion.CreatedByMember?.FullName ?? "مستخدم محذوف",
                CreatedAtUtc: suggestion.CreatedAtUtc,
                AcceptCount: suggestion.AcceptCount,
                RejectCount: suggestion.RejectCount,
                UserVote: null
            ));
    }

    /// <summary>
    /// Vote on a suggestion (accept or reject)
    /// </summary>
    [HttpPost("{id:guid}/vote")]
    public async Task<IActionResult> VoteSuggestion(
        [FromRoute] Guid id,
        [FromBody] VoteRequest request)
    {
        var currentMemberId = GetCurrentMemberId();
        if (currentMemberId == Guid.Empty)
            return Unauthorized();

        var suggestion = await _db.Suggestions
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (suggestion == null)
            return NotFound("الاقتراح غير موجود");

        var existingVote = suggestion.Votes.FirstOrDefault(v => v.MemberId == currentMemberId);

        if (existingVote != null)
        {
            // Update existing vote
            var oldVote = existingVote.IsAccepted;
            existingVote.IsAccepted = request.IsAccepted;

            if (oldVote && !request.IsAccepted)
            {
                suggestion.AcceptCount--;
                suggestion.RejectCount++;
            }
            else if (!oldVote && request.IsAccepted)
            {
                suggestion.AcceptCount++;
                suggestion.RejectCount--;
            }
        }
        else
        {
            // Create new vote
            var vote = new SuggestionVote
            {
                Id = Guid.NewGuid(),
                SuggestionId = id,
                MemberId = currentMemberId,
                IsAccepted = request.IsAccepted
            };

            suggestion.Votes.Add(vote);
            _db.SuggestionVotes.Add(vote);

            if (request.IsAccepted)
                suggestion.AcceptCount++;
            else
                suggestion.RejectCount++;
        }

        await _db.SaveChangesAsync();
        return Ok("تم تسجيل التصويت بنجاح");
    }

    private Guid GetCurrentMemberId()
    {
        var memberIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirst("memberId")?.Value;

        return Guid.TryParse(memberIdClaim, out var memberId) ? memberId : Guid.Empty;
    }
}

public class VoteRequest
{
    public bool IsAccepted { get; set; }
}
