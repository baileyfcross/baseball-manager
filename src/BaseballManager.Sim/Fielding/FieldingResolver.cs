using BaseballManager.Sim.AtBat;

namespace BaseballManager.Sim.Fielding;

public readonly record struct FieldingResult(string Fielder, string Description);

public sealed class FieldingResolver
{
    public FieldingResult ResolveFielding(BattedBallResult battedBall, Random random)
    {
        return battedBall.OutcomeType switch
        {
            BattedBallOutcomeType.Groundout => CreateGroundoutResult(battedBall, random),
            BattedBallOutcomeType.Flyout => CreateFlyoutResult(battedBall, random),
            BattedBallOutcomeType.Single => CreateHitResult(battedBall, random, new[] { "LF", "CF", "RF" }, "drops in for a single"),
            BattedBallOutcomeType.Double => CreateHitResult(battedBall, random, new[] { "LF", "CF", "RF" }, "one-hops the wall for extra bases"),
            BattedBallOutcomeType.Triple => CreateHitResult(battedBall, random, new[] { "LF", "CF", "RF" }, "rolls all the way to the fence"),
            _ => new FieldingResult("CF", $"{battedBall.ContactDescription} clears the fence for a home run!")
        };
    }

    private static FieldingResult CreateGroundoutResult(BattedBallResult battedBall, Random random)
    {
        var fielders = new[] { "3B", "SS", "2B", "1B", "P" };
        var fielder = fielders[random.Next(fielders.Length)];
        return new FieldingResult(fielder, $"{battedBall.ContactDescription} is handled by the {ExpandLabel(fielder).ToLowerInvariant()} for the out.");
    }

    private static FieldingResult CreateFlyoutResult(BattedBallResult battedBall, Random random)
    {
        var fielders = new[] { "LF", "CF", "RF" };
        var fielder = fielders[random.Next(fielders.Length)];
        return new FieldingResult(fielder, $"{battedBall.ContactDescription} is tracked down by the {ExpandLabel(fielder).ToLowerInvariant()}.");
    }

    private static FieldingResult CreateHitResult(BattedBallResult battedBall, Random random, string[] fielders, string ending)
    {
        var fielder = fielders[random.Next(fielders.Length)];
        return new FieldingResult(fielder, $"{battedBall.ContactDescription} to {ExpandLabel(fielder).ToLowerInvariant()} and it {ending}.");
    }

    private static string ExpandLabel(string label)
    {
        return label switch
        {
            "1B" => "first baseman",
            "2B" => "second baseman",
            "3B" => "third baseman",
            "SS" => "shortstop",
            "LF" => "left fielder",
            "CF" => "center fielder",
            "RF" => "right fielder",
            "P" => "pitcher",
            _ => "fielder"
        };
    }
}
