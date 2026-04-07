using BaseballManager.Sim.Engine;

namespace BaseballManager.Sim.AtBat;

public enum PitchOutcomeType
{
    Ball,
    CalledStrike,
    SwingingStrike,
    Foul,
    BallInPlay
}

public readonly record struct PitchResult(PitchOutcomeType OutcomeType, string Description, bool PutsBallInPlay);

public sealed class PitchResolver
{
    public PitchResult ResolvePitch(MatchState state, Random random)
    {
        var batter = state.CurrentBatter;
        var pitcher = state.CurrentPitcher;
        var comfortLimit = 50 + (pitcher.StaminaRating / 2);
        var overage = Math.Max(0, state.DefensiveTeam.PitchCount - comfortLimit);
        var fatiguePenalty = Math.Max(0, (state.DefensiveTeam.PitchCount - Math.Max(35, comfortLimit - 12)) / 6);
        var effectivePitching = Math.Max(18, pitcher.PitchingRating - fatiguePenalty);
        var effectiveOverall = Math.Max(18, pitcher.OverallRating - (fatiguePenalty / 2));

        var disciplineEdge = batter.DisciplineRating - effectivePitching;
        var contactEdge = batter.ContactRating - effectivePitching;
        var overallEdge = batter.OverallRating - effectiveOverall;

        var ballChance = Math.Clamp(18 + (disciplineEdge / 5) + (overallEdge / 12) + (overage / 8), 8, 34);
        var calledStrikeChance = Math.Clamp(19 + ((effectivePitching - batter.DisciplineRating) / 6) - (overallEdge / 20) - (overage / 10), 8, 28);
        var swingingStrikeChance = Math.Clamp(17 + ((effectivePitching - batter.ContactRating) / 6) - (overallEdge / 24) - (overage / 12), 7, 28);
        var foulChance = Math.Clamp(14 + (Math.Abs(contactEdge) / 20), 10, 18);

        var roll = random.Next(100);
        if (roll < ballChance)
        {
            return new PitchResult(PitchOutcomeType.Ball, $"Ball low to {batter.FullName}.", false);
        }

        roll -= ballChance;
        if (roll < calledStrikeChance)
        {
            return new PitchResult(PitchOutcomeType.CalledStrike, $"Called strike on the outer edge to {batter.FullName}.", false);
        }

        roll -= calledStrikeChance;
        if (roll < swingingStrikeChance)
        {
            return new PitchResult(PitchOutcomeType.SwingingStrike, $"{batter.FullName} swings through the pitch.", false);
        }

        roll -= swingingStrikeChance;
        if (roll < foulChance)
        {
            return new PitchResult(PitchOutcomeType.Foul, $"{batter.FullName} fouls it back.", false);
        }

        return new PitchResult(PitchOutcomeType.BallInPlay, $"{batter.FullName} puts the ball in play.", true);
    }
}
