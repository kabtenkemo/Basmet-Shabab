namespace BasmaApi.Models;

public sealed class NewsPost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public Guid CreatedByMemberId { get; set; }

    public Member CreatedByMember { get; set; } = null!;

    public NewsAudienceType AudienceType { get; set; } = NewsAudienceType.All;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<NewsTargetRole> TargetRoles { get; set; } = new List<NewsTargetRole>();

    public ICollection<NewsTargetMember> TargetMembers { get; set; } = new List<NewsTargetMember>();
}