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

        var disciplineEdge = batter.DisciplineRating - pitcher.PitchingRating;
        var contactEdge = batter.ContactRating - pitcher.PitchingRating;
        var overallEdge = batter.OverallRating - pitcher.OverallRating;

        var ballChance = Math.Clamp(18 + (disciplineEdge / 5) + (overallEdge / 12), 8, 30);
        var calledStrikeChance = Math.Clamp(19 + ((pitcher.PitchingRating - batter.DisciplineRating) / 6) - (overallEdge / 20), 10, 28);
        var swingingStrikeChance = Math.Clamp(17 + ((pitcher.PitchingRating - batter.ContactRating) / 6) - (overallEdge / 24), 8, 28);
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
