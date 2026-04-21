using BaseballManager.Core.Players;
using BaseballManager.Sim.Engine;

namespace BaseballManager.Sim.AtBat;

public sealed class BattingMatchupResolver
{
    public Handedness ResolveActiveBattingSide(BattingStyle battingStyle, Handedness pitcherThrows)
    {
        return battingStyle switch
        {
            BattingStyle.LeftOnly => Handedness.Left,
            BattingStyle.RightOnly => Handedness.Right,
            BattingStyle.Switch => pitcherThrows == Handedness.Left ? Handedness.Right : Handedness.Left,
            _ => Handedness.Right
        };
    }

    public BattingSideProfile ResolveActiveBattingProfile(MatchPlayerSnapshot batter, MatchPlayerSnapshot pitcher)
    {
        var battingProfile = batter.Batting ?? BattingProfile.FromSingleSide(BattingStyle.RightOnly, batter.ContactRating, batter.PowerRating, batter.DisciplineRating);
        var activeSide = ResolveActiveBattingSide(battingProfile.Style, pitcher.Throws);

        if (activeSide == Handedness.Left)
        {
            return battingProfile.LeftHanded ?? battingProfile.RightHanded ?? BattingSideProfile.FromRatings(batter.ContactRating, batter.PowerRating, batter.DisciplineRating);
        }

        return battingProfile.RightHanded ?? battingProfile.LeftHanded ?? BattingSideProfile.FromRatings(batter.ContactRating, batter.PowerRating, batter.DisciplineRating);
    }
}
