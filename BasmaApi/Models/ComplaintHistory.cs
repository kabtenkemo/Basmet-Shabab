namespace BasmaApi.Models;

public sealed class ComplaintHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ComplaintId { get; set; }

    public Complaint? Complaint { get; set; }

    public ComplaintHistoryAction Action { get; set; }

    public Guid? PerformedByUserId { get; set; }

    public Member? PerformedByUser { get; set; }

    public string? Notes { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}