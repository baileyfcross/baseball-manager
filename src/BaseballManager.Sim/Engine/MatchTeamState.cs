namespace BaseballManager.Sim.Engine;

public sealed class MatchTeamState
{
    public MatchTeamState(string name, string abbreviation, IEnumerable<MatchPlayerSnapshot> lineup, MatchPlayerSnapshot startingPitcher)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unknown Team" : name;
        Abbreviation = string.IsNullOrWhiteSpace(abbreviation) ? Name[..Math.Min(4, Name.Length)].ToUpperInvariant() : abbreviation.ToUpperInvariant();
        Lineup = lineup.Take(9).ToList();
        if (Lineup.Count == 0)
        {
            Lineup.Add(CreatePlaceholderPlayer("Fallback Batter", "DH"));
        }

        StartingPitcher = startingPitcher;
    }

    public string Name { get; }

    public string Abbreviation { get; }

    public List<MatchPlayerSnapshot> Lineup { get; }

    public MatchPlayerSnapshot StartingPitcher { get; }

    public int BattingIndex { get; set; }

    public int Runs { get; set; }

    public int Hits { get; set; }

    public MatchPlayerSnapshot CurrentBatter => Lineup[BattingIndex % Lineup.Count];

    public void AdvanceBatter()
    {
        BattingIndex = (BattingIndex + 1) % Lineup.Count;
    }

    public MatchPlayerSnapshot? FindPlayer(Guid playerId)
    {
        var lineupPlayer = Lineup.FirstOrDefault(player => player.Id == playerId);
        if (lineupPlayer != null)
        {
            return lineupPlayer;
        }

        return StartingPitcher.Id == playerId ? StartingPitcher : null;
    }

    public static MatchTeamState CreatePlaceholder(string name, string abbreviation)
    {
        var lineup = Enumerable.Range(1, 9)
            .Select(slot => CreatePlaceholderPlayer($"{name} Batter {slot}", slot % 2 == 0 ? "IF" : "OF"))
            .ToList();
        var pitcher = CreatePlaceholderPlayer($"{name} Pitcher", "SP", pitching: 62);
        return new MatchTeamState(name, abbreviation, lineup, pitcher);
    }

    private static MatchPlayerSnapshot CreatePlaceholderPlayer(string name, string position, int pitching = 24)
    {
        return new MatchPlayerSnapshot(
            Guid.NewGuid(),
            name,
            position,
            string.Empty,
            27,
            52,
            50,
            50,
            50,
            pitching);
    }
}
