using BaseballManager.Core.Players;

namespace BaseballManager.Core.Teams;

public sealed class Team
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<Player> Roster { get; } = new();
    public TeamLineupPresets OffensiveLineupPresets { get; set; } = new();
}
