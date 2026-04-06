namespace BaseballManager.Core.Teams;

public sealed class Lineup
{
    public Guid TeamId { get; init; }
    public List<Guid> BattingOrder { get; } = new();
}
