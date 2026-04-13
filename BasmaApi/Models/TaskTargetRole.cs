namespace BasmaApi.Models;

public sealed class TaskTargetRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }

    public MemberTask Task { get; set; } = null!;

    public MemberRole Role { get; set; }
}