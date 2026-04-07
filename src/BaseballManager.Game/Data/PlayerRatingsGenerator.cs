using BaseballManager.Contracts.ImportDtos;

namespace BaseballManager.Game.Data;

public static class PlayerRatingsGenerator
{
    public static PlayerHiddenRatingsState Generate(PlayerImportDto player)
    {
        return Generate(player.PlayerId, player.FullName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
    }

    public static PlayerHiddenRatingsState Generate(Guid playerId, string fullName, string primaryPosition, string secondaryPosition, int age)
    {
        var random = new Random(CreateStableSeed(playerId, age));
        var normalizedPosition = (primaryPosition ?? string.Empty).Trim().ToUpperInvariant();
        var ageAdjustment = age switch
        {
            <= 22 => 2,
            <= 27 => 5,
            <= 31 => 3,
            <= 35 => 0,
            _ => -3
        };

        var contact = RollBellCurve(random, 50 + ageAdjustment + PositionBonus(normalizedPosition, "SS", "2B", "CF", "C", "3B"), 12);
        var power = RollBellCurve(random, 48 + PositionBonus(normalizedPosition, "1B", "3B", "LF", "RF", "DH"), 14);
        var discipline = RollBellCurve(random, 50 + ageAdjustment, 11);
        var speed = RollBellCurve(random, 49 + PositionBonus(normalizedPosition, "SS", "2B", "CF", "LF", "RF"), 13);
        var fielding = RollBellCurve(random, 50 + PositionBonus(normalizedPosition, "SS", "2B", "3B", "CF", "C"), 11);
        var arm = RollBellCurve(random, 49 + PositionBonus(normalizedPosition, "C", "3B", "SS", "CF", "RF"), 12);
        var durability = RollBellCurve(random, 56 + ageAdjustment, 10);

        var pitching = normalizedPosition switch
        {
            "SP" => RollBellCurve(random, 68 + ageAdjustment, 10),
            "RP" => RollBellCurve(random, 63 + ageAdjustment, 10),
            _ => RollBellCurve(random, 22, 8, 5, 45)
        };

        if (normalizedPosition is "SP" or "RP")
        {
            contact = Math.Clamp(contact - 8, 20, 99);
            power = Math.Clamp(power - 10, 20, 99);
            speed = Math.Clamp(speed - 4, 20, 99);
        }

        var ratings = new PlayerHiddenRatingsState
        {
            ContactRating = contact,
            PowerRating = power,
            DisciplineRating = discipline,
            SpeedRating = speed,
            FieldingRating = fielding,
            ArmRating = arm,
            PitchingRating = pitching,
            DurabilityRating = durability
        };

        ratings.RecalculateDerivedRatings();
        return ratings;
    }

    private static int PositionBonus(string primaryPosition, params string[] favoredPositions)
    {
        return favoredPositions.Contains(primaryPosition, StringComparer.OrdinalIgnoreCase) ? 6 : 0;
    }

    private static int RollBellCurve(Random random, double mean, double standardDeviation, int min = 20, int max = 99)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        var standardNormal = Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
        var value = mean + (standardDeviation * standardNormal);
        return Math.Clamp((int)Math.Round(value), min, max);
    }

    private static int CreateStableSeed(Guid playerId, int age)
    {
        var bytes = playerId.ToByteArray();
        return BitConverter.ToInt32(bytes, 0)
               ^ BitConverter.ToInt32(bytes, 4)
               ^ BitConverter.ToInt32(bytes, 8)
               ^ BitConverter.ToInt32(bytes, 12)
               ^ age;
    }
}
