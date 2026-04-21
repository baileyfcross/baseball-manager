using BaseballManager.Core.Players;

namespace BaseballManager.Game.Data;

public static class BattingProfileFactory
{
    public static Handedness ParseThrows(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Handedness.Right;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "L" or "LEFT" => Handedness.Left,
            _ => Handedness.Right
        };
    }

    public static BattingStyle ParseBattingStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return BattingStyle.RightOnly;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "L" or "LEFT" or "LEFTONLY" => BattingStyle.LeftOnly,
            "S" or "SW" or "SWITCH" => BattingStyle.Switch,
            _ => BattingStyle.RightOnly
        };
    }

    public static BattingProfile Create(Guid playerId, BattingStyle style, int contactRating, int powerRating, int eyeRating)
    {
        var baseProfile = BattingSideProfile.FromRatings(contactRating, powerRating, eyeRating);

        if (style == BattingStyle.LeftOnly)
        {
            return new BattingProfile(BattingStyle.LeftOnly, baseProfile, null);
        }

        if (style == BattingStyle.RightOnly)
        {
            return new BattingProfile(BattingStyle.RightOnly, null, baseProfile);
        }

        var seed = CreateStableSeed(playerId);
        var random = new Random(seed);
        var left = CreateVariant(baseProfile, random.Next(-3, 4), random.Next(-3, 4), random.Next(-3, 4));
        var right = CreateVariant(baseProfile, random.Next(-3, 4), random.Next(-3, 4), random.Next(-3, 4));

        if (left == right)
        {
            right = right with { ContactRating = Math.Clamp(right.ContactRating + 1, 1, 99) };
        }

        return new BattingProfile(BattingStyle.Switch, left, right);
    }

    private static BattingSideProfile CreateVariant(BattingSideProfile profile, int contactDelta, int powerDelta, int eyeDelta)
    {
        return BattingSideProfile.FromRatings(
            profile.ContactRating + contactDelta,
            profile.PowerRating + powerDelta,
            profile.EyeRating + eyeDelta);
    }

    private static int CreateStableSeed(Guid playerId)
    {
        var bytes = playerId.ToByteArray();
        return BitConverter.ToInt32(bytes, 0)
               ^ BitConverter.ToInt32(bytes, 4)
               ^ BitConverter.ToInt32(bytes, 8)
               ^ BitConverter.ToInt32(bytes, 12)
               ^ 0x4A2D19;
    }
}
