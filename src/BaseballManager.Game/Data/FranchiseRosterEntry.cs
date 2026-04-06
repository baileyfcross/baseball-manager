namespace BaseballManager.Game.Data;

public sealed record FranchiseRosterEntry(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    int? LineupSlot,
    int? RotationSlot);
