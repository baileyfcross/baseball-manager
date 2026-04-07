namespace BaseballManager.Game.Data;

public sealed record CoachProfileView(
    string Role,
    string Name,
    string Specialty,
    string Voice);

public sealed record ScoutingPlayerCard(
    Guid PlayerId,
    string PlayerName,
    string TeamName,
    string TeamAbbreviation,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    bool IsOnSelectedTeam);

public sealed record CoachScoutingReport(
    string CoachName,
    string CoachRole,
    string PlayerName,
    string Summary,
    string Strengths,
    string Concern,
    string TransferRecommendation);
