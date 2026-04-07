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
        var adjustedRoll = random.Next(100) + ((batter.ContactRating + batter.PowerRating) / 4) - (pitcher.PitchingRating / 5);

        if (adjustedRoll < 42)
        {
            return ContactQuality.Weak;
        }

        if (adjustedRoll < 82)
        {
            return ContactQuality.Average;
        }

        return ContactQuality.Solid;
    }
}
