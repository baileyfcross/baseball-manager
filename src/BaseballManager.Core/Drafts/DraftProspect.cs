namespace BaseballManager.Core.Drafts;

public sealed record DraftProspect(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    int OverallRating,
    int PotentialRating,
    string Source,
    string Summary,
    string ScoutSummary,
    string PotentialSummary,
    string SourceTeamName,
    string SourceStatsSummary,
    string TalentOutcome);
