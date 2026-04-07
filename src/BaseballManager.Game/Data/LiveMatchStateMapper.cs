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
            StartingPitcher = team.StartingPitcher,
            BattingIndex = team.BattingIndex,
            Runs = team.Runs,
            Hits = team.Hits
        };
    }

    private static MatchTeamState ToRuntimeTeam(MatchTeamSaveState saveTeam, string fallbackName, string fallbackAbbreviation)
    {
        var lineup = saveTeam.Lineup?.Count > 0
            ? saveTeam.Lineup
            : MatchTeamState.CreatePlaceholder(fallbackName, fallbackAbbreviation).Lineup;
        var startingPitcher = saveTeam.StartingPitcher
            ?? lineup.FirstOrDefault()
            ?? MatchTeamState.CreatePlaceholder(fallbackName, fallbackAbbreviation).StartingPitcher;

        var runtimeTeam = new MatchTeamState(
            string.IsNullOrWhiteSpace(saveTeam.Name) ? fallbackName : saveTeam.Name,
            string.IsNullOrWhiteSpace(saveTeam.Abbreviation) ? fallbackAbbreviation : saveTeam.Abbreviation,
            lineup,
            startingPitcher)
        {
            BattingIndex = Math.Max(0, saveTeam.BattingIndex),
            Runs = Math.Max(0, saveTeam.Runs),
            Hits = Math.Max(0, saveTeam.Hits)
        };

        return runtimeTeam;
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
            RunsScored = resultEvent.RunsScored,
            OutsRecorded = resultEvent.OutsRecorded,
            BallX = resultEvent.BallX,
            BallY = resultEvent.BallY,
            Fielder = resultEvent.Fielder,
            IsGameOver = resultEvent.IsGameOver
        };
    }
}
