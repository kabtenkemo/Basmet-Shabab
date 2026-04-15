namespace BasmaApi.Models;

public sealed class TeamJoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? NationalId { get; set; }

    public DateOnly? BirthDate { get; set; }

    public Guid GovernorateId { get; set; }

    public Governorate Governorate { get; set; } = null!;

    public Guid? CommitteeId { get; set; }

    public Committee? Committee { get; set; }

    public string Motivation { get; set; } = string.Empty;

    public string? Experience { get; set; }

    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;

    public string? AdminNotes { get; set; }

    public Guid? AssignedToMemberId { get; set; }

    public Member? AssignedToMember { get; set; }

    public Guid? ReviewedByMemberId { get; set; }

    public Member? ReviewedByMember { get; set; }
}
