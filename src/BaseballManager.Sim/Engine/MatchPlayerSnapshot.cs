using BaseballManager.Core.Players;

namespace BaseballManager.Sim.Engine;

public sealed record MatchPlayerSnapshot(
    Guid Id,
    string FullName,
    string PrimaryPosition,
    string SecondaryPosition,
    string DefensivePosition,
    int Age,
    int ContactRating,
    int PowerRating,
    int DisciplineRating,
    int SpeedRating,
    int PitchingRating,
    int FieldingRating,
    int ArmRating,
    int StaminaRating,
    int DurabilityRating,
    int OverallRating,
    Handedness Throws = Handedness.Right,
    BattingProfile? Batting = null)
{
    public BattingStyle BattingStyle => (Batting ?? BattingProfile.FromSingleSide(BattingStyle.RightOnly, ContactRating, PowerRating, DisciplineRating)).Style;
}
