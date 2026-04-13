namespace BasmaApi.Models;

public sealed class NewsTargetRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NewsPostId { get; set; }

    public NewsPost NewsPost { get; set; } = null!;

    public MemberRole Role { get; set; }
}