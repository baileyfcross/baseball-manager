namespace BaseballManager.Core.Players;

public sealed class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string PrimaryPosition { get; set; } = string.Empty;
}
