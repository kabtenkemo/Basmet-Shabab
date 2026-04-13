namespace BasmaApi.Models;

public sealed class TaskTargetMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }

    public MemberTask Task { get; set; } = null!;

    public Guid MemberId { get; set; }

    public Member Member { get; set; } = null!;
}