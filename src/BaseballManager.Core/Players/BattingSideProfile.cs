namespace BaseballManager.Core.Players;

public sealed record BattingSideProfile(
    int ContactRating,
    int PowerRating,
    int EyeRating)
{
    public static BattingSideProfile FromRatings(int contactRating, int powerRating, int eyeRating)
    {
        return new BattingSideProfile(
            Math.Clamp(contactRating, 1, 99),
            Math.Clamp(powerRating, 1, 99),
            Math.Clamp(eyeRating, 1, 99));
    }
}
