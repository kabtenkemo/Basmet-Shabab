namespace BasmaApi.Models;

public sealed class PointTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public int Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? RelatedByMemberId { get; set; }

    public Member? RelatedByMember { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}