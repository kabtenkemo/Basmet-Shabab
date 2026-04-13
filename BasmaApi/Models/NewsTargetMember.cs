namespace BasmaApi.Models;

public sealed class NewsTargetMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NewsPostId { get; set; }

    public NewsPost NewsPost { get; set; } = null!;

    public Guid MemberId { get; set; }

    public Member Member { get; set; } = null!;
}