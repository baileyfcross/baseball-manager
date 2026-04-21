using BaseballManager.Core.Players;
using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Results;

namespace BaseballManager.Game.Data;

public static class LiveMatchStateMapper
{
    public static LiveMatchSaveState FromMatchState(MatchState matchState, float secondsUntilNextPitch, float ballHighlightTimer, bool isPaused)
    {
        return new LiveMatchSaveState
        {
            AwayTeam = ToSaveTeam(matchState.AwayTeam),
            HomeTeam = ToSaveTeam(matchState.HomeTeam),
            AwayRunsByInning = matchState.AwayRunsByInning.ToList(),
            HomeRunsByInning = matchState.HomeRunsByInning.ToList(),
            AwayErrors = matchState.AwayErrors,
            HomeErrors = matchState.HomeErrors,
            InningNumber = matchState.Inning.Number,
            IsTopHalf = matchState.Inning.IsTopHalf,
            Outs = matchState.Inning.Outs,
            Balls = matchState.Count.Balls,
            Strikes = matchState.Count.Strikes,
            FirstBaseRunnerId = matchState.Baserunners.FirstBaseRunnerId,
            SecondBaseRunnerId = matchState.Baserunners.SecondBaseRunnerId,
            ThirdBaseRunnerId = matchState.Baserunners.ThirdBaseRunnerId,
            BallX = matchState.Field.BallX,
            BallY = matchState.Field.BallY,
            BallVisible = matchState.Field.BallVisible,
            BallLabel = matchState.Field.BallLabel,
            HighlightedFielder = matchState.Field.HighlightedFielder,
            LatestEvent = CloneResultEvent(matchState.LatestEvent),
            IsGameOver = matchState.IsGameOver,
            CompletedPlays = matchState.CompletedPlays,
            SecondsUntilNextPitch = secondsUntilNextPitch,
            BallHighlightTimer = ballHighlightTimer,
            IsPaused = isPaused,
            SavedAtUtc = DateTime.UtcNow
        };
    }

    public static LiveMatchRestoreState ToRuntimeState(LiveMatchSaveState saveState)
    {
        var awayTeam = ToRuntimeTeam(saveState.AwayTeam, "Visitors", "VIS");
        var homeTeam = ToRuntimeTeam(saveState.HomeTeam, "Home", "HOME");
        var matchState = new MatchState(awayTeam, homeTeam)
        {
            LatestEvent = CloneResultEvent(saveState.LatestEvent),
            IsGameOver = saveState.IsGameOver,
            CompletedPlays = saveState.CompletedPlays
        };

        matchState.Inning.Number = Math.Max(1, saveState.InningNumber);
        matchState.EnsureInningTracked(matchState.Inning.Number);
        var awayLinescore = saveState.AwayRunsByInning is { Count: > 0 } ? saveState.AwayRunsByInning : [0];
        var homeLinescore = saveState.HomeRunsByInning is { Count: > 0 } ? saveState.HomeRunsByInning : [0];
        matchState.AwayRunsByInning.Clear();
        matchState.AwayRunsByInning.AddRange(awayLinescore);
        matchState.HomeRunsByInning.Clear();
        matchState.HomeRunsByInning.AddRange(homeLinescore);
        matchState.AwayErrors = Math.Max(0, saveState.AwayErrors);
        matchState.HomeErrors = Math.Max(0, saveState.HomeErrors);
        matchState.Inning.IsTopHalf = saveState.IsTopHalf;
        matchState.Inning.Outs = Math.Clamp(saveState.Outs, 0, 3);
        matchState.Count.Balls = Math.Clamp(saveState.Balls, 0, 4);
        matchState.Count.Strikes = Math.Clamp(saveState.Strikes, 0, 3);
        matchState.Baserunners.FirstBaseRunnerId = saveState.FirstBaseRunnerId;
        matchState.Baserunners.SecondBaseRunnerId = saveState.SecondBaseRunnerId;
        matchState.Baserunners.ThirdBaseRunnerId = saveState.ThirdBaseRunnerId;
        matchState.Field.BallX = saveState.BallX;
        matchState.Field.BallY = saveState.BallY;
        matchState.Field.BallVisible = saveState.BallVisible;
        matchState.Field.BallLabel = saveState.BallLabel ?? string.Empty;
        matchState.Field.HighlightedFielder = saveState.HighlightedFielder ?? string.Empty;

        return new LiveMatchRestoreState(
            matchState,
            Math.Max(0.1f, saveState.SecondsUntilNextPitch),
            Math.Max(0.25f, saveState.BallHighlightTimer),
            saveState.IsPaused);
    }

    private static MatchTeamSaveState ToSaveTeam(MatchTeamState team)
    {
        return new MatchTeamSaveState
        {
            Name = team.Name,
            Abbreviation = team.Abbreviation,
            Lineup = team.Lineup.ToList(),
            BenchPlayers = team.BenchPlayers.ToList(),
            BullpenPlayers = team.BullpenPlayers.ToList(),
            StartingPitcher = team.StartingPitcher,
            CurrentPitcher = team.CurrentPitcher,
            BattingIndex = team.BattingIndex,
            Runs = team.Runs,
            Hits = team.Hits,
            PitchCount = team.PitchCount,
            PitchCountsByPitcher = team.PitchCountsByPitcher.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    private static MatchTeamState ToRuntimeTeam(MatchTeamSaveState saveTeam, string fallbackName, string fallbackAbbreviation)
    {
        var lineup = saveTeam.Lineup?.Count > 0
            ? saveTeam.Lineup.Select(NormalizeSnapshot).ToList()
            : MatchTeamState.CreatePlaceholder(fallbackName, fallbackAbbreviation).Lineup;
        var benchPlayers = saveTeam.BenchPlayers?.Select(NormalizeSnapshot).ToList() ?? [];
        var bullpenPlayers = saveTeam.BullpenPlayers?.Select(NormalizeSnapshot).ToList() ?? [];
        var startingPitcher = NormalizeSnapshot(saveTeam.StartingPitcher
            ?? lineup.FirstOrDefault()
            ?? MatchTeamState.CreatePlaceholder(fallbackName, fallbackAbbreviation).StartingPitcher);
        var currentPitcher = NormalizeSnapshot(saveTeam.CurrentPitcher ?? startingPitcher);
        var pitchCountsByPitcher = saveTeam.PitchCountsByPitcher?
            .Where(pair => pair.Key != Guid.Empty)
            .ToDictionary(pair => pair.Key, pair => Math.Max(0, pair.Value)) ?? [];
        if (pitchCountsByPitcher.Count == 0 && saveTeam.PitchCount > 0)
        {
            pitchCountsByPitcher[currentPitcher.Id] = Math.Max(0, saveTeam.PitchCount);
        }

        var runtimeTeam = new MatchTeamState(
            string.IsNullOrWhiteSpace(saveTeam.Name) ? fallbackName : saveTeam.Name,
            string.IsNullOrWhiteSpace(saveTeam.Abbreviation) ? fallbackAbbreviation : saveTeam.Abbreviation,
            lineup,
            startingPitcher,
            benchPlayers,
            bullpenPlayers,
            currentPitcher,
            pitchCountsByPitcher)
        {
            BattingIndex = Math.Max(0, saveTeam.BattingIndex),
            Runs = Math.Max(0, saveTeam.Runs),
            Hits = Math.Max(0, saveTeam.Hits),
            PitchCount = Math.Max(0, saveTeam.PitchCount)
        };

        return runtimeTeam;
    }

    private static MatchPlayerSnapshot NormalizeSnapshot(MatchPlayerSnapshot snapshot)
    {
        var contact = snapshot.ContactRating > 0 ? snapshot.ContactRating : 50;
        var power = snapshot.PowerRating > 0 ? snapshot.PowerRating : 48;
        var discipline = snapshot.DisciplineRating > 0 ? snapshot.DisciplineRating : 50;
        var speed = snapshot.SpeedRating > 0 ? snapshot.SpeedRating : 50;
        var pitching = snapshot.PitchingRating > 0 ? snapshot.PitchingRating : (snapshot.PrimaryPosition is "SP" or "RP" ? 58 : 20);
        var fielding = snapshot.FieldingRating > 0 ? snapshot.FieldingRating : 52;
        var arm = snapshot.ArmRating > 0 ? snapshot.ArmRating : 50;
        var stamina = snapshot.StaminaRating > 0 ? snapshot.StaminaRating : (snapshot.PrimaryPosition is "SP" ? 68 : snapshot.PrimaryPosition is "RP" ? 58 : 55);
        var durability = snapshot.DurabilityRating > 0 ? snapshot.DurabilityRating : 55;
        var overall = snapshot.OverallRating > 0
            ? snapshot.OverallRating
            : Math.Clamp((int)Math.Round((contact + power + discipline + speed + pitching + fielding + arm + stamina + durability) / 9d), 1, 99);
        var throws = snapshot.Throws;
        var batting = snapshot.Batting ?? BattingProfileFactory.Create(snapshot.Id, BattingStyle.RightOnly, contact, power, discipline);

        return snapshot with
        {
            ContactRating = contact,
            PowerRating = power,
            DisciplineRating = discipline,
            SpeedRating = speed,
            PitchingRating = pitching,
            FieldingRating = fielding,
            ArmRating = arm,
            StaminaRating = stamina,
            DurabilityRating = durability,
            OverallRating = overall,
            Throws = throws,
            Batting = batting
        };
    }

    private static ResultEvent CloneResultEvent(ResultEvent? resultEvent)
    {
        if (resultEvent == null)
        {
            return new ResultEvent
            {
                Code = "READY",
                Description = "First pitch coming up."
            };
        }

        return new ResultEvent
        {
            Code = resultEvent.Code,
            Description = resultEvent.Description,
            EndsPlateAppearance = resultEvent.EndsPlateAppearance,
            IsBallInPlay = resultEvent.IsBallInPlay,
            CountsAsAtBat = resultEvent.CountsAsAtBat,
            CountsAsHit = resultEvent.CountsAsHit,
            IsWalk = resultEvent.IsWalk,
            IsStrikeout = resultEvent.IsStrikeout,
            BasesAwarded = resultEvent.BasesAwarded,
            RunsScored = resultEvent.RunsScored,
            OutsRecorded = resultEvent.OutsRecorded,
            BatterId = resultEvent.BatterId,
            PitcherId = resultEvent.PitcherId,
            ScoringPlayerIds = resultEvent.ScoringPlayerIds?.ToList() ?? [],
            BallX = resultEvent.BallX,
            BallY = resultEvent.BallY,
            Fielder = resultEvent.Fielder,
            IsGameOver = resultEvent.IsGameOver
        };
    }
}
