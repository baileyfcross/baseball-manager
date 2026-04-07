using BaseballManager.Sim.Engine;

namespace BaseballManager.Sim.AtBat;

public enum BattedBallOutcomeType
{
    Groundout,
    Flyout,
    Single,
    Double,
    Triple,
    HomeRun
}

public readonly record struct BattedBallResult(BattedBallOutcomeType OutcomeType, float BallX, float BallY, string ContactDescription);

public sealed class BattedBallResolver
{
    public BattedBallResult ResolveBattedBall(MatchState state, ContactQuality contactQuality, Random random)
    {
        var batter = state.CurrentBatter;
        var powerBoost = Math.Clamp((batter.PowerRating - 50) / 5, -6, 10);
        var speedBoost = Math.Clamp((batter.SpeedRating - 50) / 8, -3, 5);
        var roll = random.Next(100);

        return contactQuality switch
        {
            ContactQuality.Weak => roll < 50
                ? CreateGroundout(random, "A chopped grounder")
                : roll < 82
                    ? CreateFlyout(random, "A lazy pop fly")
                    : CreateSingle(random, "A soft single"),
            ContactQuality.Average => roll < 26 + powerBoost
                ? CreateSingle(random, "A sharp single")
                : roll < 44 + powerBoost
                    ? CreateDouble(random, "A drive into the gap")
                    : roll < 68 + speedBoost
                        ? CreateGroundout(random, "A routine ground ball")
                        : CreateFlyout(random, "A lifted fly ball"),
            _ => roll < 18 + powerBoost
                ? CreateHomeRun(random, "A towering drive")
                : roll < 36 + powerBoost
                    ? CreateDouble(random, "A rope off the wall")
                    : roll < 42 + speedBoost
                        ? CreateTriple(random, "A rocket to the alley")
                        : roll < 76
                            ? CreateSingle(random, "A solid liner")
                            : CreateFlyout(random, "A deep fly ball")
        };
    }

    private static BattedBallResult CreateGroundout(Random random, string contactDescription)
        => new(BattedBallOutcomeType.Groundout, RandomRange(random, 0.28f, 0.72f), RandomRange(random, 0.42f, 0.58f), contactDescription);

    private static BattedBallResult CreateFlyout(Random random, string contactDescription)
        => new(BattedBallOutcomeType.Flyout, RandomRange(random, 0.18f, 0.82f), RandomRange(random, 0.12f, 0.30f), contactDescription);

    private static BattedBallResult CreateSingle(Random random, string contactDescription)
        => new(BattedBallOutcomeType.Single, RandomRange(random, 0.20f, 0.80f), RandomRange(random, 0.24f, 0.42f), contactDescription);

    private static BattedBallResult CreateDouble(Random random, string contactDescription)
        => new(BattedBallOutcomeType.Double, RandomRange(random, 0.12f, 0.88f), RandomRange(random, 0.10f, 0.24f), contactDescription);

    private static BattedBallResult CreateTriple(Random random, string contactDescription)
        => new(BattedBallOutcomeType.Triple, RandomRange(random, 0.08f, 0.92f), RandomRange(random, 0.06f, 0.18f), contactDescription);

    private static BattedBallResult CreateHomeRun(Random random, string contactDescription)
        => new(BattedBallOutcomeType.HomeRun, RandomRange(random, 0.22f, 0.78f), RandomRange(random, 0.04f, 0.10f), contactDescription);

    private static float RandomRange(Random random, float min, float max)
        => min + ((float)random.NextDouble() * (max - min));
}
