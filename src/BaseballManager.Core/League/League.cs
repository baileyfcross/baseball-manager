using BaseballManager.Core.Teams;

namespace BaseballManager.Core.League;

public sealed class League
{
    public string Name { get; set; } = string.Empty;
    public List<Team> Teams { get; } = new();
}
