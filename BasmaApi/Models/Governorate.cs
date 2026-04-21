namespace BasmaApi.Models;

public sealed class Governorate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool IsVisibleInJoinForm { get; set; } = true;

    public ICollection<Committee> Committees { get; set; } = new List<Committee>();
}