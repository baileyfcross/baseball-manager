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
    public ContactQuality ResolveContact(MatchState state, Random random)
    {
        var batter = state.CurrentBatter;
        var pitcher = state.CurrentPitcher;
        var batterSkill = batter.ContactRating + (batter.PowerRating / 2) + (batter.OverallRating / 3);
        var pitcherSkill = pitcher.PitchingRating + (pitcher.OverallRating / 4);
        var adjustedRoll = random.Next(100) + ((batterSkill - pitcherSkill) / 3) + ((batter.DisciplineRating - 50) / 6);

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
