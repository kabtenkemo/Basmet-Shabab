namespace BasmaApi.Models;

public sealed class Committee
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GovernorateId { get; set; }

    public Governorate Governorate { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}