using System.Text.Json;
using BaseballManager.Contracts.ImportDtos;

namespace BaseballManager.Game.Data;

public sealed class LeagueDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ImportedLeagueData Load()
    {
        var root = RepositoryPathResolver.GetRepositoryRoot(AppContext.BaseDirectory);
        var generatedDir = Path.Combine(root, "data", "imports", "generated");
        var importDir = Path.Combine(root, "data", "imports");

        var teams = LoadList<TeamImportDto>(Path.Combine(generatedDir, "teams.json"), Path.Combine(importDir, "teams.json"));
        TeamColorPalette.ApplyTo(teams);

        return new ImportedLeagueData(
            teams,
            LoadList<PlayerImportDto>(Path.Combine(generatedDir, "players.json"), Path.Combine(importDir, "players.json")),
            LoadList<RosterImportDto>(Path.Combine(generatedDir, "rosters.json"), Path.Combine(importDir, "rosters.json")),
            LoadList<ScheduleImportDto>(Path.Combine(generatedDir, "schedule.json"), Path.Combine(importDir, "schedule.json")));
    }

    private static List<T> LoadList<T>(params string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }

        return new List<T>();
    }
}
