namespace BasmaApi.Models;

public enum SuggestionStatus
{
    Open,
    Accepted,
    Rejected
}

public sealed class Suggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid CreatedByMemberId { get; set; }

    public Member? CreatedByMember { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public SuggestionStatus Status { get; set; } = SuggestionStatus.Open;

    public int AcceptanceCount { get; set; }

    public int RejectionCount { get; set; }

    public ICollection<SuggestionVote> Votes { get; set; } = new List<SuggestionVote>();
}

public sealed class SuggestionVote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid SuggestionId { get; set; }

    public Suggestion? Suggestion { get; set; }

    public Guid VotedByMemberId { get; set; }

    public Member? VotedByMember { get; set; }

    public bool IsAcceptance { get; set; }
}
