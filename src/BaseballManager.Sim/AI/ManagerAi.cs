using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;

namespace BaseballManager.Sim.AI;

public sealed record ManagerAiRosterCandidate(
    MatchPlayerSnapshot Player,
    int AvailabilityPriority,
    bool IsAvailable,
    int? PreferredLineupSlot = null,
    int? RotationSlot = null);

public sealed record ManagerAiTeamPlan(
    MatchPlayerSnapshot? StartingPitcher,
    IReadOnlyList<MatchPlayerSnapshot> Lineup,
    IReadOnlyList<MatchPlayerSnapshot> Bench,
    IReadOnlyList<MatchPlayerSnapshot> Bullpen);

public sealed class ManagerAi
{
    private static readonly string[] DefensiveSlots = ["C", "IF", "IF", "IF", "IF", "OF", "OF", "OF", "UTIL"];

    public ManagerAiTeamPlan BuildTeamPlan(IReadOnlyList<ManagerAiRosterCandidate> roster)
    {
        var hitters = ResolvePriorityOrder(roster.Where(candidate => !IsPitcher(candidate.Player)).ToList());
        var pitchers = ResolvePriorityOrder(roster.Where(candidate => IsPitcher(candidate.Player)).ToList());

        var starterCandidate = SelectStartingPitcherCandidate(pitchers);
        var startingPitcher = starterCandidate?.Player;

        var lineup = BuildLineup(hitters);
        var lineupIds = lineup.Select(player => player.Id).ToHashSet();
        var bench = hitters
            .Where(candidate => !lineupIds.Contains(candidate.Player.Id))
            .Select(candidate => candidate.Player)
            .ToList();
        var bullpen = pitchers
            .Where(candidate => candidate.Player.Id != startingPitcher?.Id)
            .Select(candidate => candidate.Player)
            .ToList();

        return new ManagerAiTeamPlan(startingPitcher, lineup, bench, bullpen);
    }

    public bool TryApplyDefensiveManagerDecision(MatchState state)
    {
        var defensiveTeam = state.DefensiveTeam;
        if (defensiveTeam.BullpenPlayers.Count == 0)
        {
            return false;
        }

        var currentPitcher = defensiveTeam.CurrentPitcher;
        var currentPitchCount = defensiveTeam.CurrentPitcherPitchCount;
        var isStarter = currentPitcher.Id == defensiveTeam.StartingPitcher.Id;
        var comfortLimit = (isStarter ? 72 : 24) + (currentPitcher.StaminaRating / 2);
        var hardLimit = comfortLimit + (isStarter ? 16 : 10);
        var scoreMargin = Math.Abs(state.HomeTeam.Runs - state.AwayTeam.Runs);
        var highLeverage = state.Inning.Number >= 7 && scoreMargin <= 2;
        var shouldReplace = currentPitchCount >= hardLimit
            || (currentPitchCount >= comfortLimit && (state.Inning.Number >= 6 || currentPitcher.PitchingRating <= 58))
            || (highLeverage && currentPitchCount >= comfortLimit - 6);
        if (!shouldReplace)
        {
            return false;
        }

        var replacement = defensiveTeam.BullpenPlayers
            .OrderByDescending(player => ScoreReliever(player, highLeverage))
            .ThenByDescending(player => player.OverallRating)
            .FirstOrDefault();
        if (replacement == null || !defensiveTeam.TrySubstitutePitcher(replacement.Id, out var outgoingPitcher, out var incomingPitcher))
        {
            return false;
        }

        state.Field.ResetToPitcher();
        state.LatestEvent = new ResultEvent
        {
            Code = "SUB",
            Description = $"{defensiveTeam.Abbreviation} goes to the bullpen: {incomingPitcher.FullName} replaces {outgoingPitcher.FullName}.",
            BatterId = state.CurrentBatter.Id,
            PitcherId = incomingPitcher.Id,
            BallX = state.Field.BallX,
            BallY = state.Field.BallY,
            Fielder = "P"
        };

        return true;
    }

    private static List<MatchPlayerSnapshot> BuildLineup(IReadOnlyList<ManagerAiRosterCandidate> hitters)
    {
        var remaining = hitters.ToList();
        var selected = new List<MatchPlayerSnapshot>();

        foreach (var slot in DefensiveSlots)
        {
            if (remaining.Count == 0)
            {
                break;
            }

            var candidate = remaining
                .OrderByDescending(candidate => ScoreLineupFit(candidate, slot))
                .ThenByDescending(candidate => candidate.Player.OverallRating)
                .First();
            selected.Add(candidate.Player);
            remaining.RemoveAll(existing => existing.Player.Id == candidate.Player.Id);
        }

        return OrderBattingLineup(selected);
    }

    private static List<MatchPlayerSnapshot> OrderBattingLineup(IReadOnlyList<MatchPlayerSnapshot> players)
    {
        var remaining = players.ToList();
        var ordered = new List<MatchPlayerSnapshot>();

        AddBest(remaining, ordered, player => player.ContactRating * 3 + player.DisciplineRating * 2 + player.SpeedRating * 2 + player.OverallRating);
        AddBest(remaining, ordered, player => player.ContactRating * 2 + player.DisciplineRating * 2 + player.SpeedRating + player.OverallRating * 2);
        AddBest(remaining, ordered, player => player.OverallRating * 3 + player.ContactRating + player.PowerRating + player.DisciplineRating);
        AddBest(remaining, ordered, player => player.PowerRating * 3 + player.OverallRating * 2 + player.ContactRating);

        foreach (var player in remaining
                     .OrderByDescending(player => player.OverallRating * 2 + player.PowerRating + player.ContactRating + player.DisciplineRating))
        {
            ordered.Add(player);
        }

        return ordered;
    }

    private static void AddBest(List<MatchPlayerSnapshot> remaining, List<MatchPlayerSnapshot> ordered, Func<MatchPlayerSnapshot, int> scoreSelector)
    {
        if (remaining.Count == 0)
        {
            return;
        }

        var selected = remaining
            .OrderByDescending(scoreSelector)
            .ThenByDescending(player => player.OverallRating)
            .First();
        ordered.Add(selected);
        remaining.RemoveAll(player => player.Id == selected.Id);
    }

    private static ManagerAiRosterCandidate? SelectStartingPitcherCandidate(IReadOnlyList<ManagerAiRosterCandidate> pitchers)
    {
        return pitchers
            .OrderByDescending(ScoreStartingPitcher)
            .ThenByDescending(candidate => candidate.Player.OverallRating)
            .FirstOrDefault();
    }

    private static List<ManagerAiRosterCandidate> ResolvePriorityOrder(IReadOnlyList<ManagerAiRosterCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var available = candidates
            .Where(candidate => candidate.IsAvailable)
            .OrderBy(candidate => candidate.AvailabilityPriority)
            .ThenBy(candidate => candidate.PreferredLineupSlot ?? 99)
            .ThenBy(candidate => candidate.RotationSlot ?? 99)
            .ToList();
        var unavailable = candidates
            .Where(candidate => !candidate.IsAvailable)
            .OrderBy(candidate => candidate.AvailabilityPriority)
            .ThenBy(candidate => candidate.PreferredLineupSlot ?? 99)
            .ThenBy(candidate => candidate.RotationSlot ?? 99)
            .ToList();

        return [.. available, .. unavailable];
    }

    private static int ScoreLineupFit(ManagerAiRosterCandidate candidate, string slot)
    {
        var player = candidate.Player;
        var positionBonus = slot switch
        {
            "C" when MatchesPosition(player, "C") => 45,
            "IF" when MatchesPosition(player, "IF") => 35,
            "OF" when MatchesPosition(player, "OF") => 35,
            "UTIL" => 20,
            _ => 0
        };

        return (player.OverallRating * 5)
            + (player.ContactRating * 2)
            + player.PowerRating
            + player.DisciplineRating
            + player.SpeedRating
            + player.FieldingRating
            + positionBonus
            - candidate.AvailabilityPriority;
    }

    private static int ScoreStartingPitcher(ManagerAiRosterCandidate candidate)
    {
        var player = candidate.Player;
        var starterBonus = player.PrimaryPosition == "SP" ? 45 : 0;
        var rotationBonus = candidate.RotationSlot.HasValue ? Math.Max(0, 18 - ((candidate.RotationSlot.Value - 1) * 4)) : 0;
        return (player.OverallRating * 5)
            + (player.PitchingRating * 4)
            + (player.StaminaRating * 3)
            + player.DurabilityRating
            + starterBonus
            + rotationBonus
            - candidate.AvailabilityPriority;
    }

    private static int ScoreReliever(MatchPlayerSnapshot player, bool highLeverage)
    {
        var leverageBonus = highLeverage ? player.DurabilityRating : player.StaminaRating;
        var relieverBonus = player.PrimaryPosition == "RP" ? 20 : 0;
        return (player.PitchingRating * 4)
            + (player.OverallRating * 3)
            + (player.StaminaRating * 2)
            + leverageBonus
            + relieverBonus;
    }

    private static bool MatchesPosition(MatchPlayerSnapshot player, string positionGroup)
    {
        return positionGroup switch
        {
            "C" => player.PrimaryPosition == "C" || player.SecondaryPosition == "C",
            "IF" => player.PrimaryPosition is "IF" or "1B" or "2B" or "3B" or "SS"
                || player.SecondaryPosition is "IF" or "1B" or "2B" or "3B" or "SS",
            "OF" => player.PrimaryPosition is "OF" or "LF" or "CF" or "RF"
                || player.SecondaryPosition is "OF" or "LF" or "CF" or "RF",
            _ => true
        };
    }

    private static bool IsPitcher(MatchPlayerSnapshot player)
    {
        return player.PrimaryPosition is "SP" or "RP";
    }
}
