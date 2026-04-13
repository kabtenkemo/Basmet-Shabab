namespace BasmaApi.Models;

public sealed class Suggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid CreatedByMemberId { get; set; }

    public Member? CreatedByMember { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int AcceptCount { get; set; }

    public int RejectCount { get; set; }

    public ICollection<SuggestionVote> Votes { get; set; } = new List<SuggestionVote>();
}

public sealed class SuggestionVote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid SuggestionId { get; set; }

    public Suggestion? Suggestion { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public bool IsAccepted { get; set; }
}
