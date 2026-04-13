namespace BasmaApi.Models;

public sealed class MemberTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskAudienceType AudienceType { get; set; } = TaskAudienceType.All;

    public bool IsCompleted { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public ICollection<TaskTargetRole> TargetRoles { get; set; } = new List<TaskTargetRole>();

    public ICollection<TaskTargetMember> TargetMembers { get; set; } = new List<TaskTargetMember>();
}