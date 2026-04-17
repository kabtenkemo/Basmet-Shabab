namespace BasmaApi.Models;

public sealed class ImportantContact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string PositionTitle { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid CreatedByMemberId { get; set; }

    public Member CreatedByMember { get; set; } = null!;
}
