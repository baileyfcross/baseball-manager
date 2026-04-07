namespace BaseballManager.Sim.Engine;

public sealed record MatchPlayerSnapshot(
    Guid Id,
    string FullName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    int ContactRating,
    int PowerRating,
    int DisciplineRating,
    int SpeedRating,
    int PitchingRating);
