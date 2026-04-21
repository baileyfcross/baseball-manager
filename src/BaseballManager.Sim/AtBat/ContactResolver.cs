using BaseballManager.Sim.Engine;

namespace BaseballManager.Sim.AtBat;

public enum ContactQuality
{
    Weak,
    Average,
    Solid
}

public sealed class ContactResolver
{
    private readonly BattingMatchupResolver _battingMatchupResolver = new();

    public ContactQuality ResolveContact(MatchState state, Random random)
    {
        var batter = state.CurrentBatter;
        var pitcher = state.CurrentPitcher;
        var activeBattingProfile = _battingMatchupResolver.ResolveActiveBattingProfile(batter, pitcher);
        var comfortLimit = 50 + (pitcher.StaminaRating / 2);
        var overage = Math.Max(0, state.DefensiveTeam.PitchCount - comfortLimit);
        var fatiguePenalty = Math.Max(0, (state.DefensiveTeam.PitchCount - Math.Max(35, comfortLimit - 12)) / 5);
        var batterSkill = activeBattingProfile.ContactRating + (activeBattingProfile.PowerRating / 2) + (batter.OverallRating / 3);
        var pitcherSkill = Math.Max(18, pitcher.PitchingRating - fatiguePenalty) + (Math.Max(18, pitcher.OverallRating - (fatiguePenalty / 2)) / 4);
        var adjustedRoll = random.Next(100) + ((batterSkill - pitcherSkill) / 3) + ((activeBattingProfile.EyeRating - 50) / 6) + (overage / 4);

        if (adjustedRoll < 38)
        {
            return ContactQuality.Weak;
        }

        if (adjustedRoll < 78)
        {
            return ContactQuality.Average;
        }

        return ContactQuality.Solid;
    }
}
