namespace BaseballManager.Application.Drafts;

public sealed record DraftCpuTeamContext(
    string TeamName,
    IReadOnlyList<string> RosterPositions);
