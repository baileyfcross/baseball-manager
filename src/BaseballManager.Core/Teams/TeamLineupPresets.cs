using BaseballManager.Core.Players;

namespace BaseballManager.Core.Teams;

public enum LineupPresetType
{
    VsLeftHandedPitcher,
    VsRightHandedPitcher
}

public sealed class TeamLineupPresets
{
    public Lineup VsLeftHandedPitcher { get; set; } = new();
    public Lineup VsRightHandedPitcher { get; set; } = new();

    public Lineup GetForPitcherHand(Handedness pitcherThrows)
    {
        return pitcherThrows == Handedness.Left ? VsLeftHandedPitcher : VsRightHandedPitcher;
    }

    public void InitializeFromDefault(Lineup defaultLineup)
    {
        VsLeftHandedPitcher = defaultLineup.Clone();
        VsRightHandedPitcher = defaultLineup.Clone();
    }
}
