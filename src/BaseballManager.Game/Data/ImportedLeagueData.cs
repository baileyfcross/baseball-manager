using BaseballManager.Contracts.ImportDtos;

namespace BaseballManager.Game.Data;

public sealed class ImportedLeagueData
{
    public ImportedLeagueData(
        IReadOnlyList<TeamImportDto> teams,
        IReadOnlyList<PlayerImportDto> players,
        IReadOnlyList<RosterImportDto> rosters,
        IReadOnlyList<ScheduleImportDto> schedule)
    {
        Teams = teams;
        Players = players;
        Rosters = rosters;
        Schedule = schedule;
    }

    public IReadOnlyList<TeamImportDto> Teams { get; }

    public IReadOnlyList<PlayerImportDto> Players { get; }

    public IReadOnlyList<RosterImportDto> Rosters { get; }

    public IReadOnlyList<ScheduleImportDto> Schedule { get; }

    public bool HasData => Teams.Count > 0;
}