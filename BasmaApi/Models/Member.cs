namespace BasmaApi.Models;

public sealed class Member
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? NationalId { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public MemberRole Role { get; set; } = MemberRole.CommitteeMember;

    public string? GovernorName { get; set; }

    public string? CommitteeName { get; set; }

    public Guid? GovernorateId { get; set; }

    public Guid? CommitteeId { get; set; }

    public int Points { get; set; }

    public Guid? CreatedByMemberId { get; set; }

    public Member? CreatedByMember { get; set; }

    public ICollection<MemberTask> Tasks { get; set; } = new List<MemberTask>();

    public ICollection<MemberPermissionGrant> PermissionGrants { get; set; } = new List<MemberPermissionGrant>();

    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();

    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public ICollection<NewsPost> CreatedNewsPosts { get; set; } = new List<NewsPost>();
}