namespace BaseballManager.Core.Players;

public sealed record BattingProfile(
    BattingStyle Style,
    BattingSideProfile? LeftHanded,
    BattingSideProfile? RightHanded)
{
    public static BattingProfile FromSingleSide(BattingStyle style, int contactRating, int powerRating, int eyeRating)
    {
        var sideProfile = BattingSideProfile.FromRatings(contactRating, powerRating, eyeRating);

        return style switch
        {
            BattingStyle.LeftOnly => new BattingProfile(BattingStyle.LeftOnly, sideProfile, null),
            BattingStyle.RightOnly => new BattingProfile(BattingStyle.RightOnly, null, sideProfile),
            BattingStyle.Switch => new BattingProfile(BattingStyle.Switch, sideProfile, sideProfile),
            _ => new BattingProfile(BattingStyle.RightOnly, null, sideProfile)
        };
    }
}
