using BaseballManager.Sim.AtBat;
using BaseballManager.Sim.Engine;

namespace BaseballManager.Sim.Baserunning;

public readonly record struct BaserunningResult(int RunsScored, string AdditionalDescription);

public sealed class BaserunningResolver
{
    public BaserunningResult ResolveAdvance(MatchState state, PlateAppearanceOutcome outcome, Random random)
    {
        return outcome.Code switch
        {
            "Walk" => ProcessWalk(state, outcome),
            "Single" => ProcessSingle(state, outcome, random),
            "Double" => ProcessDouble(state, outcome, random),
            "Triple" => ProcessTriple(state, outcome),
            "HomeRun" => ProcessHomeRun(state, outcome),
            "Groundout" => ProcessGroundout(state, outcome),
            "Flyout" => ProcessFlyout(state, outcome, random),
            _ => new BaserunningResult(0, string.Empty)
        };
    }

    private static BaserunningResult ProcessWalk(MatchState state, PlateAppearanceOutcome outcome)
    {
        var first = state.Baserunners.FirstBaseRunnerId;
        var second = state.Baserunners.SecondBaseRunnerId;
        var third = state.Baserunners.ThirdBaseRunnerId;
        var runs = 0;
        var note = string.Empty;

        if (first.HasValue)
        {
            if (second.HasValue)
            {
                if (third.HasValue)
                {
                    runs++;
                    note = $"{GetRunnerName(state, third)} scores on the bases-loaded walk.";
                }

                third = second;
            }

            second = first;
        }

        first = outcome.BatterId;
        state.Baserunners.FirstBaseRunnerId = first;
        state.Baserunners.SecondBaseRunnerId = second;
        state.Baserunners.ThirdBaseRunnerId = third;
        return new BaserunningResult(runs, note);
    }

    private static BaserunningResult ProcessSingle(MatchState state, PlateAppearanceOutcome outcome, Random random)
    {
        var first = state.Baserunners.FirstBaseRunnerId;
        var second = state.Baserunners.SecondBaseRunnerId;
        var third = state.Baserunners.ThirdBaseRunnerId;
        var newFirst = outcome.BatterId;
        Guid? newSecond = null;
        Guid? newThird = null;
        var runs = 0;
        var notes = new List<string>();

        if (third.HasValue)
        {
            runs++;
            notes.Add($"{GetRunnerName(state, third)} scores.");
        }

        if (second.HasValue)
        {
            if (random.NextDouble() < 0.75d)
            {
                runs++;
                notes.Add($"{GetRunnerName(state, second)} scores from second.");
            }
            else
            {
                newThird = second;
            }
        }

        if (first.HasValue)
        {
            if (!newThird.HasValue && random.NextDouble() < 0.30d)
            {
                newThird = first;
            }
            else
            {
                newSecond = first;
            }
        }

        state.Baserunners.FirstBaseRunnerId = newFirst;
        state.Baserunners.SecondBaseRunnerId = newSecond;
        state.Baserunners.ThirdBaseRunnerId = newThird;
        return new BaserunningResult(runs, string.Join(" ", notes));
    }

    private static BaserunningResult ProcessDouble(MatchState state, PlateAppearanceOutcome outcome, Random random)
    {
        var first = state.Baserunners.FirstBaseRunnerId;
        var second = state.Baserunners.SecondBaseRunnerId;
        var third = state.Baserunners.ThirdBaseRunnerId;
        Guid? newThird = null;
        var runs = 0;
        var notes = new List<string>();

        if (third.HasValue)
        {
            runs++;
            notes.Add($"{GetRunnerName(state, third)} scores.");
        }

        if (second.HasValue)
        {
            runs++;
            notes.Add($"{GetRunnerName(state, second)} scores easily.");
        }

        if (first.HasValue)
        {
            if (random.NextDouble() < 0.55d)
            {
                runs++;
                notes.Add($"{GetRunnerName(state, first)} comes all the way around to score.");
            }
            else
            {
                newThird = first;
            }
        }

        state.Baserunners.FirstBaseRunnerId = null;
        state.Baserunners.SecondBaseRunnerId = outcome.BatterId;
        state.Baserunners.ThirdBaseRunnerId = newThird;
        return new BaserunningResult(runs, string.Join(" ", notes));
    }

    private static BaserunningResult ProcessTriple(MatchState state, PlateAppearanceOutcome outcome)
    {
        var runs = 0;
        var notes = new List<string>();

        foreach (var runner in new[] { state.Baserunners.FirstBaseRunnerId, state.Baserunners.SecondBaseRunnerId, state.Baserunners.ThirdBaseRunnerId })
        {
            if (runner.HasValue)
            {
                runs++;
                notes.Add($"{GetRunnerName(state, runner)} scores.");
            }
        }

        state.Baserunners.Clear();
        state.Baserunners.ThirdBaseRunnerId = outcome.BatterId;
        return new BaserunningResult(runs, string.Join(" ", notes));
    }

    private static BaserunningResult ProcessHomeRun(MatchState state, PlateAppearanceOutcome outcome)
    {
        var runs = 1;
        var notes = new List<string>();

        foreach (var runner in new[] { state.Baserunners.FirstBaseRunnerId, state.Baserunners.SecondBaseRunnerId, state.Baserunners.ThirdBaseRunnerId })
        {
            if (runner.HasValue)
            {
                runs++;
                notes.Add($"{GetRunnerName(state, runner)} scores.");
            }
        }

        state.Baserunners.Clear();
        return new BaserunningResult(runs, string.Join(" ", notes));
    }

    private static BaserunningResult ProcessGroundout(MatchState state, PlateAppearanceOutcome outcome)
    {
        if (outcome.OutsBeforePlay >= 2)
        {
            return new BaserunningResult(0, string.Empty);
        }

        var notes = new List<string>();
        if (state.Baserunners.SecondBaseRunnerId.HasValue && !state.Baserunners.ThirdBaseRunnerId.HasValue)
        {
            state.Baserunners.ThirdBaseRunnerId = state.Baserunners.SecondBaseRunnerId;
            state.Baserunners.SecondBaseRunnerId = null;
            notes.Add($"{GetRunnerName(state, state.Baserunners.ThirdBaseRunnerId)} moves up to third.");
        }

        if (state.Baserunners.FirstBaseRunnerId.HasValue && !state.Baserunners.SecondBaseRunnerId.HasValue)
        {
            state.Baserunners.SecondBaseRunnerId = state.Baserunners.FirstBaseRunnerId;
            state.Baserunners.FirstBaseRunnerId = null;
            notes.Add($"{GetRunnerName(state, state.Baserunners.SecondBaseRunnerId)} advances to second.");
        }

        return new BaserunningResult(0, string.Join(" ", notes));
    }

    private static BaserunningResult ProcessFlyout(MatchState state, PlateAppearanceOutcome outcome, Random random)
    {
        if (outcome.OutsBeforePlay < 2 && state.Baserunners.ThirdBaseRunnerId.HasValue && random.NextDouble() < 0.35d)
        {
            var scoringRunner = state.Baserunners.ThirdBaseRunnerId;
            state.Baserunners.ThirdBaseRunnerId = null;
            return new BaserunningResult(1, $"{GetRunnerName(state, scoringRunner)} tags from third and scores.");
        }

        return new BaserunningResult(0, string.Empty);
    }

    private static string GetRunnerName(MatchState state, Guid? runnerId)
    {
        return string.IsNullOrWhiteSpace(state.GetRunnerName(runnerId)) ? "A runner" : state.GetRunnerName(runnerId);
    }
}
