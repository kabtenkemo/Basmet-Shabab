namespace BasmaApi.Models;

public sealed class Complaint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

    public ComplaintPriority Priority { get; set; } = ComplaintPriority.Medium;

    public int EscalationLevel { get; set; }

    public DateTime LastActionDateUtc { get; set; } = DateTime.UtcNow;

    public Guid? AssignedToMemberId { get; set; }

    public Member? AssignedToMember { get; set; }

    public string? AdminReply { get; set; }

    public Guid? ReviewedByMemberId { get; set; }

    public Member? ReviewedByMember { get; set; }

    public ICollection<ComplaintHistory> Histories { get; set; } = new List<ComplaintHistory>();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}