namespace BasmaApi.Models;

public sealed class MemberPermissionGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public string PermissionKey { get; set; } = string.Empty;

    public Guid GrantedByMemberId { get; set; }

    public Member? GrantedByMember { get; set; }

    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
}