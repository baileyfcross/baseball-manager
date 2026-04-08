using BaseballManager.Sim.Baserunning;
using BaseballManager.Sim.Engine;
using BaseballManager.Sim.Fielding;
using BaseballManager.Sim.Results;

namespace BaseballManager.Sim.AtBat;

public readonly record struct PlateAppearanceOutcome(
    string Code,
    string Description,
    bool IsOut,
    bool IsWalk,
    int BasesAwarded,
    bool CountsAsHit,
    float BallX,
    float BallY,
    string Fielder,
    string BallLabel,
    int OutsBeforePlay,
    Guid BatterId);

public sealed class AtBatResolver
{
    private readonly PitchResolver _pitchResolver = new();
    private readonly ContactResolver _contactResolver = new();
    private readonly BattedBallResolver _battedBallResolver = new();
    private readonly FieldingResolver _fieldingResolver = new();
    private readonly BaserunningResolver _baserunningResolver = new();

    public ResultEvent Resolve(MatchState state, Random random)
    {
        var batter = state.CurrentBatter;
        state.DefensiveTeam.PitchCount++;
        var pitch = _pitchResolver.ResolvePitch(state, random);

        switch (pitch.OutcomeType)
        {
            case PitchOutcomeType.Ball:
                state.Count.Balls++;
                state.Field.BallX = 0.52f;
                state.Field.BallY = 0.66f;
                state.Field.BallLabel = "Ball";
                state.Field.HighlightedFielder = "C";

                if (state.Count.Balls >= 4)
                {
                    var walkOutcome = new PlateAppearanceOutcome(
                        Code: "Walk",
                        Description: $"{batter.FullName} draws a walk.",
                        IsOut: false,
                        IsWalk: true,
                        BasesAwarded: 1,
                        CountsAsHit: false,
                        BallX: 0.52f,
                        BallY: 0.66f,
                        Fielder: "C",
                        BallLabel: "Walk",
                        OutsBeforePlay: state.Inning.Outs,
                        BatterId: batter.Id);
                    return FinalizePlateAppearance(state, walkOutcome, random);
                }

                return BuildCountEvent(state, "Ball", $"{pitch.Description} Count is {state.Count.Display}.");

            case PitchOutcomeType.CalledStrike:
            case PitchOutcomeType.SwingingStrike:
                state.Count.Strikes++;
                state.Field.BallX = 0.50f;
                state.Field.BallY = 0.60f;
                state.Field.BallLabel = "Strike";
                state.Field.HighlightedFielder = "C";

                if (state.Count.Strikes >= 3)
                {
                    var strikeoutText = pitch.OutcomeType == PitchOutcomeType.CalledStrike
                        ? $"{batter.FullName} is rung up on a called third strike."
                        : $"{batter.FullName} swings through strike three.";
                    var strikeoutOutcome = new PlateAppearanceOutcome(
                        Code: "Strikeout",
                        Description: strikeoutText,
                        IsOut: true,
                        IsWalk: false,
                        BasesAwarded: 0,
                        CountsAsHit: false,
                        BallX: 0.50f,
                        BallY: 0.60f,
                        Fielder: "C",
                        BallLabel: "K",
                        OutsBeforePlay: state.Inning.Outs,
                        BatterId: batter.Id);
                    return FinalizePlateAppearance(state, strikeoutOutcome, random);
                }

                return BuildCountEvent(state, "Strike", $"{pitch.Description} Count is {state.Count.Display}.");

            case PitchOutcomeType.Foul:
                if (state.Count.Strikes < 2)
                {
                    state.Count.Strikes++;
                }

                state.Field.BallX = 0.18f + ((float)random.NextDouble() * 0.64f);
                state.Field.BallY = 0.80f;
                state.Field.BallLabel = "Foul";
                state.Field.HighlightedFielder = "C";
                return BuildCountEvent(state, "Foul", $"{pitch.Description} Count is {state.Count.Display}.");

            default:
                var contactQuality = _contactResolver.ResolveContact(state, random);
                var battedBall = _battedBallResolver.ResolveBattedBall(state, contactQuality, random);
                var fielding = _fieldingResolver.ResolveFielding(battedBall, random);
                var outcome = new PlateAppearanceOutcome(
                    Code: battedBall.OutcomeType.ToString(),
                    Description: fielding.Description,
                    IsOut: battedBall.OutcomeType is BattedBallOutcomeType.Groundout or BattedBallOutcomeType.Flyout,
                    IsWalk: false,
                    BasesAwarded: battedBall.OutcomeType switch
                    {
                        BattedBallOutcomeType.Single => 1,
                        BattedBallOutcomeType.Double => 2,
                        BattedBallOutcomeType.Triple => 3,
                        BattedBallOutcomeType.HomeRun => 4,
                        _ => 0
                    },
                    CountsAsHit: battedBall.OutcomeType is BattedBallOutcomeType.Single or BattedBallOutcomeType.Double or BattedBallOutcomeType.Triple or BattedBallOutcomeType.HomeRun,
                    BallX: battedBall.BallX,
                    BallY: battedBall.BallY,
                    Fielder: fielding.Fielder,
                    BallLabel: battedBall.OutcomeType.ToString(),
                    OutsBeforePlay: state.Inning.Outs,
                    BatterId: batter.Id);
                return FinalizePlateAppearance(state, outcome, random);
        }
    }

    private static ResultEvent BuildCountEvent(MatchState state, string code, string description)
    {
        return new ResultEvent
        {
            Code = code,
            Description = description,
            EndsPlateAppearance = false,
            IsBallInPlay = false,
            CountsAsAtBat = false,
            CountsAsHit = false,
            IsWalk = false,
            IsStrikeout = false,
            BasesAwarded = 0,
            RunsScored = 0,
            OutsRecorded = 0,
            BatterId = state.CurrentBatter.Id,
            PitcherId = state.CurrentPitcher.Id,
            BallX = state.Field.BallX,
            BallY = state.Field.BallY,
            Fielder = state.Field.HighlightedFielder,
            IsGameOver = false
        };
    }

    private ResultEvent FinalizePlateAppearance(MatchState state, PlateAppearanceOutcome outcome, Random random)
    {
        var offense = state.OffensiveTeam;
        var runsScored = 0;
        var baserunningText = string.Empty;

        if (outcome.CountsAsHit)
        {
            offense.Hits++;
        }

        if (outcome.IsOut)
        {
            state.Inning.Outs++;
        }

        var baserunningResult = _baserunningResolver.ResolveAdvance(state, outcome, random);
        runsScored = baserunningResult.RunsScored;
        baserunningText = baserunningResult.AdditionalDescription;
        offense.Runs += runsScored;

        state.Field.BallX = outcome.BallX;
        state.Field.BallY = outcome.BallY;
        state.Field.BallVisible = true;
        state.Field.BallLabel = outcome.BallLabel;
        state.Field.HighlightedFielder = outcome.Fielder;

        state.Count.Reset();
        offense.AdvanceBatter();
        state.CompletedPlays++;

        var description = string.IsNullOrWhiteSpace(baserunningText)
            ? outcome.Description
            : $"{outcome.Description} {baserunningText}";

        var transitionText = HandleInningTransitions(state);
        if (!string.IsNullOrWhiteSpace(transitionText))
        {
            description = $"{description} {transitionText}";
        }

        var isBallInPlay = !outcome.IsWalk
            && !string.Equals(outcome.Code, "Strikeout", StringComparison.OrdinalIgnoreCase);

        return new ResultEvent
        {
            Code = outcome.Code,
            Description = description.Trim(),
            EndsPlateAppearance = true,
            IsBallInPlay = isBallInPlay,
            CountsAsAtBat = !outcome.IsWalk,
            CountsAsHit = outcome.CountsAsHit,
            IsWalk = outcome.IsWalk,
            IsStrikeout = string.Equals(outcome.Code, "Strikeout", StringComparison.OrdinalIgnoreCase),
            BasesAwarded = outcome.BasesAwarded,
            RunsScored = runsScored,
            OutsRecorded = outcome.IsOut ? 1 : 0,
            BatterId = outcome.BatterId,
            PitcherId = state.CurrentPitcher.Id,
            BallX = outcome.BallX,
            BallY = outcome.BallY,
            Fielder = outcome.Fielder,
            IsGameOver = state.IsGameOver
        };
    }

    private static string HandleInningTransitions(MatchState state)
    {
        if (!state.Inning.IsTopHalf && state.Inning.Number >= 9 && state.HomeTeam.Runs > state.AwayTeam.Runs)
        {
            state.IsGameOver = true;
            return $"{state.HomeTeam.Name} walk it off!";
        }

        if (state.Inning.Outs < 3)
        {
            return string.Empty;
        }

        state.Baserunners.Clear();
        state.Count.Reset();

        if (state.Inning.IsTopHalf)
        {
            state.Inning.IsTopHalf = false;
            state.Inning.Outs = 0;

            if (state.Inning.Number >= 9 && state.HomeTeam.Runs > state.AwayTeam.Runs)
            {
                state.IsGameOver = true;
                return $"Side retired. {state.HomeTeam.Name} win {state.HomeTeam.Runs}-{state.AwayTeam.Runs}.";
            }

            state.Field.ResetToPitcher();
            return $"Three outs. Bottom {state.Inning.Number} coming up.";
        }

        if (state.Inning.Number >= 9 && state.HomeTeam.Runs != state.AwayTeam.Runs)
        {
            state.IsGameOver = true;
            var winner = state.HomeTeam.Runs > state.AwayTeam.Runs ? state.HomeTeam.Name : state.AwayTeam.Name;
            var highScore = Math.Max(state.HomeTeam.Runs, state.AwayTeam.Runs);
            var lowScore = Math.Min(state.HomeTeam.Runs, state.AwayTeam.Runs);
            return $"Ballgame. {winner} win {highScore}-{lowScore}.";
        }

        state.Inning.Number++;
        state.Inning.IsTopHalf = true;
        state.Inning.Outs = 0;
        state.Field.ResetToPitcher();
        return $"Side retired. Top {state.Inning.Number} coming up.";
    }
}
