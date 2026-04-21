using BaseballManager.Core.Players;

namespace BaseballManager.Core.Teams;

public sealed class Lineup
{
    public Guid TeamId { get; init; }
    public List<Guid> BattingOrder { get; } = new();

    public Lineup Clone()
    {
        var copy = new Lineup { TeamId = TeamId };
        copy.BattingOrder.AddRange(BattingOrder);
        return copy;
    }

    public bool IsValidForRoster(IEnumerable<Player> roster)
    {
        var rosterIds = roster.Select(player => player.Id).ToHashSet();

        return BattingOrder.Count == 9
               && BattingOrder.Distinct().Count() == BattingOrder.Count
               && BattingOrder.All(rosterIds.Contains);
    }
}
