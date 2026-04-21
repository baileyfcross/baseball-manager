using BaseballManager.Core.Players;

namespace BaseballManager.Sim.Engine;

public sealed class MatchTeamState
{
    public MatchTeamState(
        string name,
        string abbreviation,
        IEnumerable<MatchPlayerSnapshot> lineup,
        MatchPlayerSnapshot startingPitcher,
        IEnumerable<MatchPlayerSnapshot>? benchPlayers = null,
        IEnumerable<MatchPlayerSnapshot>? bullpenPlayers = null,
        MatchPlayerSnapshot? currentPitcher = null,
        IDictionary<Guid, int>? pitchCountsByPitcher = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unknown Team" : name;
        Abbreviation = string.IsNullOrWhiteSpace(abbreviation) ? Name[..Math.Min(4, Name.Length)].ToUpperInvariant() : abbreviation.ToUpperInvariant();
        Lineup = lineup.Take(9).ToList();
        if (Lineup.Count == 0)
        {
            Lineup.Add(CreatePlaceholderPlayer("Fallback Batter", "DH"));
        }

        StartingPitcher = startingPitcher;
        CurrentPitcher = currentPitcher ?? startingPitcher;
        BenchPlayers = benchPlayers?.ToList() ?? [];
        BullpenPlayers = bullpenPlayers?.ToList() ?? [];
        PitchCountsByPitcher = pitchCountsByPitcher?.ToDictionary(pair => pair.Key, pair => Math.Max(0, pair.Value)) ?? [];
        EnsurePitcherTracked(StartingPitcher);
        EnsurePitcherTracked(CurrentPitcher);
    }

    public string Name { get; }

    public string Abbreviation { get; }

    public List<MatchPlayerSnapshot> Lineup { get; }

    public List<MatchPlayerSnapshot> BenchPlayers { get; }

    public List<MatchPlayerSnapshot> BullpenPlayers { get; }

    public MatchPlayerSnapshot StartingPitcher { get; }

    public MatchPlayerSnapshot CurrentPitcher { get; private set; }

    public Dictionary<Guid, int> PitchCountsByPitcher { get; }

    public int BattingIndex { get; set; }

    public int Runs { get; set; }

    public int Hits { get; set; }

    public int PitchCount { get; set; }

    public int CurrentPitcherPitchCount => GetPitchCountForPitcher(CurrentPitcher.Id);

    public MatchPlayerSnapshot CurrentBatter => Lineup[BattingIndex % Lineup.Count];

    public void AdvanceBatter()
    {
        BattingIndex = (BattingIndex + 1) % Lineup.Count;
    }

    public void RecordPitchThrown()
    {
        PitchCount++;
        EnsurePitcherTracked(CurrentPitcher);
        PitchCountsByPitcher[CurrentPitcher.Id]++;
    }

    public int GetPitchCountForPitcher(Guid playerId)
    {
        return PitchCountsByPitcher.TryGetValue(playerId, out var pitchCount)
            ? pitchCount
            : 0;
    }

    public MatchPlayerSnapshot? FindPlayer(Guid playerId)
    {
        var lineupPlayer = Lineup.FirstOrDefault(player => player.Id == playerId);
        if (lineupPlayer != null)
        {
            return lineupPlayer;
        }

        if (CurrentPitcher.Id == playerId)
        {
            return CurrentPitcher;
        }

        if (StartingPitcher.Id == playerId)
        {
            return StartingPitcher;
        }

        return BenchPlayers.FirstOrDefault(player => player.Id == playerId)
            ?? BullpenPlayers.FirstOrDefault(player => player.Id == playerId);
    }

    public MatchPlayerSnapshot? FindFielder(string positionLabel)
    {
        if (string.IsNullOrWhiteSpace(positionLabel))
        {
            return null;
        }

        if (string.Equals(positionLabel, "P", StringComparison.OrdinalIgnoreCase))
        {
            return CurrentPitcher;
        }

        var exactMatch = Lineup.FirstOrDefault(player =>
            string.Equals(player.DefensivePosition, positionLabel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(player.PrimaryPosition, positionLabel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(player.SecondaryPosition, positionLabel, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch;
        }

        return positionLabel.ToUpperInvariant() switch
        {
            "LF" or "CF" or "RF" => Lineup.FirstOrDefault(player => player.DefensivePosition is "OF" or "LF" or "CF" or "RF" || player.PrimaryPosition is "OF" or "LF" or "CF" or "RF"),
            "1B" or "2B" or "3B" or "SS" => Lineup.FirstOrDefault(player => player.DefensivePosition is "IF" or "1B" or "2B" or "3B" or "SS" || player.PrimaryPosition is "IF" or "1B" or "2B" or "3B" or "SS"),
            "C" => Lineup.FirstOrDefault(player => string.Equals(player.DefensivePosition, "C", StringComparison.OrdinalIgnoreCase) || string.Equals(player.PrimaryPosition, "C", StringComparison.OrdinalIgnoreCase)),
            _ => Lineup.FirstOrDefault()
        };
    }

    public static MatchTeamState CreatePlaceholder(string name, string abbreviation)
    {
        var lineup = Enumerable.Range(1, 9)
            .Select(slot => CreatePlaceholderPlayer($"{name} Batter {slot}", slot % 2 == 0 ? "IF" : "OF"))
            .ToList();
        var pitcher = CreatePlaceholderPlayer($"{name} Pitcher", "SP", pitching: 62);
        return new MatchTeamState(name, abbreviation, lineup, pitcher);
    }

    public bool CanSubstitutePitcher(Guid playerId)
    {
        return BullpenPlayers.Any(player => player.Id == playerId);
    }

    public bool TrySubstitutePitcher(Guid playerId, out MatchPlayerSnapshot outgoingPitcher, out MatchPlayerSnapshot incomingPitcher)
    {
        outgoingPitcher = CurrentPitcher;
        incomingPitcher = CurrentPitcher;

        var replacementIndex = BullpenPlayers.FindIndex(player => player.Id == playerId);
        if (replacementIndex < 0)
        {
            return false;
        }

        incomingPitcher = BullpenPlayers[replacementIndex];
        BullpenPlayers.RemoveAt(replacementIndex);
        CurrentPitcher = incomingPitcher;
        EnsurePitcherTracked(CurrentPitcher);
        return true;
    }

    public bool CanSubstituteLineupPlayer(int lineupIndex, Guid playerId)
    {
        return lineupIndex >= 0 && lineupIndex < Lineup.Count && BenchPlayers.Any(player => player.Id == playerId);
    }

    public bool TrySubstituteLineupPlayer(int lineupIndex, Guid playerId, out MatchPlayerSnapshot outgoingPlayer, out MatchPlayerSnapshot incomingPlayer)
    {
        outgoingPlayer = Lineup[Math.Clamp(lineupIndex, 0, Lineup.Count - 1)];
        incomingPlayer = outgoingPlayer;

        if (lineupIndex < 0 || lineupIndex >= Lineup.Count)
        {
            return false;
        }

        var replacementIndex = BenchPlayers.FindIndex(player => player.Id == playerId);
        if (replacementIndex < 0)
        {
            return false;
        }

        incomingPlayer = BenchPlayers[replacementIndex];
        outgoingPlayer = Lineup[lineupIndex];
        BenchPlayers.RemoveAt(replacementIndex);
        Lineup[lineupIndex] = incomingPlayer;
        return true;
    }

    private static MatchPlayerSnapshot CreatePlaceholderPlayer(string name, string position, int pitching = 24)
    {
        var stamina = string.Equals(position, "SP", StringComparison.OrdinalIgnoreCase) ? 68 : 55;
        var overall = (int)Math.Round((52 + 50 + 50 + 50 + pitching + 52 + 50 + stamina + 55) / 9d);

        return new MatchPlayerSnapshot(
            Guid.NewGuid(),
            name,
            position,
            string.Empty,
            position,
            27,
            52,
            50,
            50,
            50,
            pitching,
            52,
            50,
            stamina,
            55,
                overall,
                Handedness.Right,
                BattingProfile.FromSingleSide(BattingStyle.RightOnly, 52, 50, 50));
    }

    private void EnsurePitcherTracked(MatchPlayerSnapshot pitcher)
    {
        if (!PitchCountsByPitcher.ContainsKey(pitcher.Id))
        {
            PitchCountsByPitcher[pitcher.Id] = 0;
        }
    }
}
