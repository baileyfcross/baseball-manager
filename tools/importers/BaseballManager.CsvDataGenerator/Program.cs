using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace BaseballManager.CsvDataGenerator;

internal static class Program
{
    private static readonly string[] FirstNames =
    [
        "James", "John", "Robert", "Michael", "William", "David", "Joseph", "Thomas", "Daniel", "Matthew",
        "Anthony", "Mark", "Andrew", "Joshua", "Ryan", "Nicholas", "Tyler", "Brandon", "Jordan", "Samuel"
    ];

    private static readonly string[] LastNames =
    [
        "Adams", "Baker", "Carter", "Diaz", "Edwards", "Foster", "Garcia", "Hayes", "Irving", "Johnson",
        "King", "Lopez", "Morris", "Nelson", "Owens", "Parker", "Reed", "Sanchez", "Turner", "Walker"
    ];

    private static readonly string[] Cities =
    [
        "Atlanta", "Austin", "Baltimore", "Boston", "Charlotte", "Chicago", "Cleveland", "Dallas", "Denver", "Detroit",
        "Houston", "Indianapolis", "Kansas City", "Los Angeles", "Miami", "Milwaukee", "Minneapolis", "Nashville", "New York", "Philadelphia",
        "Phoenix", "Pittsburgh", "Portland", "San Diego", "San Francisco", "Seattle", "St. Louis", "Tampa", "Toronto", "Washington"
    ];

    private static readonly string[] Mascots =
    [
        "Aces", "Anchors", "Bears", "Captains", "Comets", "Dragons", "Falcons", "Foxes", "Giants", "Guardians",
        "Hawks", "Knights", "Lions", "Owls", "Pilots", "Rockets", "Sharks", "Stars", "Storm", "Wolves"
    ];

    private static readonly string[] Positions = ["C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH", "SP", "RP"];

    private static int Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            Directory.CreateDirectory(options.OutputDirectory);

            var random = new Random(options.Seed);
            var teams = GenerateTeams(options.TeamCount, random);
            var players = GeneratePlayers(teams, options.PlayersPerTeam, random);
            var rosters = GenerateRosterEntries(players);
            var schedule = GenerateSchedule(teams, options.StartDate);

            var teamsCsvPath = Path.Combine(options.OutputDirectory, "teams.csv");
            var playersCsvPath = Path.Combine(options.OutputDirectory, "players.csv");
            var rostersCsvPath = Path.Combine(options.OutputDirectory, "rosters.csv");
            var scheduleCsvPath = Path.Combine(options.OutputDirectory, "schedule.csv");

            WriteTeamsCsv(teamsCsvPath, teams);
            WritePlayersCsv(playersCsvPath, players);
            WriteRostersCsv(rostersCsvPath, rosters);
            WriteScheduleCsv(scheduleCsvPath, schedule);

            Console.WriteLine($"Generated CSV files in {options.OutputDirectory}");
            Console.WriteLine($"Teams: {teams.Count}, Players: {players.Count}, Roster rows: {rosters.Count}, Games: {schedule.Count}");

            if (options.ImportAfterGenerate)
            {
                RunImportWorkflow(options.OutputDirectory, teamsCsvPath, playersCsvPath, rostersCsvPath, scheduleCsvPath);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Generation failed: {ex.Message}");
            return 1;
        }
    }

    private static GeneratorOptions ParseOptions(string[] args)
    {
        var teamCount = 30;
        var playersPerTeam = 40;
        var outputDirectory = Path.Combine("data", "imports", "generated");
        var seed = 42;
        var startDate = new DateOnly(2026, 3, 26);
        var importAfterGenerate = true;

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            var value = i + 1 < args.Length ? args[i + 1] : null;

            switch (key)
            {
                case "teams":
                    teamCount = int.Parse(RequireValue(key, value), CultureInfo.InvariantCulture);
                    i++;
                    break;
                case "players-per-team":
                    playersPerTeam = int.Parse(RequireValue(key, value), CultureInfo.InvariantCulture);
                    i++;
                    break;
                case "out-dir":
                    outputDirectory = RequireValue(key, value);
                    i++;
                    break;
                case "seed":
                    seed = int.Parse(RequireValue(key, value), CultureInfo.InvariantCulture);
                    i++;
                    break;
                case "start-date":
                    startDate = DateOnly.ParseExact(RequireValue(key, value), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    i++;
                    break;
                case "import":
                    importAfterGenerate = bool.Parse(RequireValue(key, value));
                    i++;
                    break;
                case "help":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '--{key}'.");
            }
        }

        if (teamCount < 2 || teamCount % 2 != 0)
        {
            throw new ArgumentException("--teams must be an even number greater than or equal to 2.");
        }

        if (playersPerTeam != 40)
        {
            throw new ArgumentException("--players-per-team must be 40 to match the requested roster size.");
        }

        return new GeneratorOptions(teamCount, playersPerTeam, outputDirectory, seed, startDate, importAfterGenerate);
    }

    private static string RequireValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '--{key}' requires a value.");
        }

        return value;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("CSV Data Generator");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/importers/BaseballManager.CsvDataGenerator/BaseballManager.CsvDataGenerator.csproj -- \\");
        Console.WriteLine("    --teams 30 \\");
        Console.WriteLine("    --players-per-team 40 \\");
        Console.WriteLine("    --out-dir data/imports/generated \\");
        Console.WriteLine("    --start-date 2026-03-26 \\");
        Console.WriteLine("    --seed 42 \\");
        Console.WriteLine("    --import true");
    }

    private static List<TeamRow> GenerateTeams(int teamCount, Random random)
    {
        var teams = new List<TeamRow>(teamCount);
        var cityPool = BuildValuePool(Cities, teamCount, random);
        var mascotPool = BuildValuePool(Mascots, teamCount, random);
        var leagueNames = new[] { "American", "National" };
        var divisionNames = new[] { "East", "Central", "West" };
        var teamsPerLeague = teamCount / 2;

        for (var i = 0; i < teamCount; i++)
        {
            var leagueIndex = i < teamsPerLeague ? 0 : 1;
            var divisionIndex = (i % teamsPerLeague) * divisionNames.Length / teamsPerLeague;
            var city = cityPool[i];
            var mascot = mascotPool[i];
            var name = $"{city} {mascot}";
            var abbreviation = BuildAbbreviation(city, mascot, teams.Select(t => t.Abbreviation).ToHashSet(StringComparer.OrdinalIgnoreCase));

            teams.Add(new TeamRow(
                name,
                abbreviation,
                leagueNames[leagueIndex],
                divisionNames[Math.Min(divisionIndex, divisionNames.Length - 1)],
                city,
                $"{city} Park"));
        }

        return teams;
    }

    private static string[] BuildValuePool(string[] source, int count, Random random)
    {
        var values = new List<string>(count);
        var cycle = 0;

        while (values.Count < count)
        {
            var shuffled = source.OrderBy(_ => random.Next()).ToArray();
            foreach (var value in shuffled)
            {
                if (values.Count >= count)
                {
                    break;
                }

                values.Add(cycle == 0 ? value : $"{value} {cycle + 1}");
            }

            cycle++;
        }

        return values.ToArray();
    }

    private static List<PlayerRow> GeneratePlayers(List<TeamRow> teams, int playersPerTeam, Random random)
    {
        var players = new List<PlayerRow>(teams.Count * playersPerTeam);
        var ageMin = 19;
        var ageMax = 38;

        foreach (var team in teams)
        {
            for (var i = 0; i < playersPerTeam; i++)
            {
                var playerId = Guid.NewGuid();
                var fullName = $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}";
                var primaryPosition = GetPrimaryPosition(i);
                var secondaryPosition = GetSecondaryPosition(primaryPosition, random);
                var age = random.Next(ageMin, ageMax + 1);

                players.Add(new PlayerRow(playerId, fullName, primaryPosition, secondaryPosition, team.Name, age));
            }
        }

        return players;
    }

    private static string GetPrimaryPosition(int index)
    {
        if (index < 5) return "SP";
        if (index < 13) return "RP";
        if (index == 13) return "C";
        if (index == 14) return "1B";
        if (index == 15) return "2B";
        if (index == 16) return "3B";
        if (index == 17) return "SS";
        if (index == 18) return "LF";
        if (index == 19) return "CF";
        if (index == 20) return "RF";
        if (index == 21) return "DH";
        return Positions[index % Positions.Length];
    }

    private static string GetSecondaryPosition(string primaryPosition, Random random)
    {
        var options = Positions.Where(p => p != primaryPosition).ToArray();
        return options[random.Next(options.Length)];
    }

    private static List<RosterRow> GenerateRosterEntries(List<PlayerRow> players)
    {
        var rosters = new List<RosterRow>(players.Count);

        foreach (var teamGroup in players.GroupBy(p => p.TeamName))
        {
            var teamPlayers = teamGroup.ToList();
            var lineupCandidates = teamPlayers.Where(p => p.PrimaryPosition is not "SP" and not "RP").Take(9).ToList();
            var rotationCandidates = teamPlayers.Where(p => p.PrimaryPosition == "SP").Take(5).ToList();

            foreach (var player in teamPlayers)
            {
                int? lineupSlot = null;
                int? rotationSlot = null;

                var lineupIndex = lineupCandidates.FindIndex(p => p.PlayerId == player.PlayerId);
                if (lineupIndex >= 0)
                {
                    lineupSlot = lineupIndex + 1;
                }

                var rotationIndex = rotationCandidates.FindIndex(p => p.PlayerId == player.PlayerId);
                if (rotationIndex >= 0)
                {
                    rotationSlot = rotationIndex + 1;
                }

                rosters.Add(new RosterRow(
                    player.PlayerId,
                    player.TeamName,
                    player.FullName,
                    player.PrimaryPosition,
                    player.SecondaryPosition,
                    lineupSlot,
                    rotationSlot));
            }
        }

        return rosters;
    }

    private static List<ScheduleRow> GenerateSchedule(List<TeamRow> teams, DateOnly startDate)
    {
        var seriesByRound = BuildSeriesMatrix(teams);
        var schedule = new List<ScheduleRow>();
        var gameCounters = teams.ToDictionary(t => t.Name, _ => 0);

        for (var round = 0; round < seriesByRound.Count; round++)
        {
            var seriesDate = startDate.AddDays(round * 4);
            foreach (var series in seriesByRound[round])
            {
                for (var gameOffset = 0; gameOffset < 3; gameOffset++)
                {
                    gameCounters[series.HomeTeam.Name]++;
                    gameCounters[series.AwayTeam.Name]++;
                    schedule.Add(new ScheduleRow(
                        seriesDate.AddDays(gameOffset),
                        series.HomeTeam.Name,
                        series.AwayTeam.Name,
                        gameCounters[series.HomeTeam.Name],
                        series.HomeTeam.Ballpark));
                }
            }
        }

        return schedule;
    }

    private static List<List<SeriesMatchup>> BuildSeriesMatrix(List<TeamRow> teams)
    {
        var rounds = new List<List<SeriesMatchup>>();
        var rotation = teams.ToList();
        var roundTemplate = new List<List<(TeamRow Home, TeamRow Away)>>();
        const int targetSeriesPerTeam = 54;

        for (var round = 0; round < teams.Count - 1; round++)
        {
            var pairings = new List<(TeamRow Home, TeamRow Away)>();
            for (var i = 0; i < teams.Count / 2; i++)
            {
                var home = rotation[i];
                var away = rotation[^(i + 1)];
                if (round % 2 == 1)
                {
                    (home, away) = (away, home);
                }

                pairings.Add((home, away));
            }

            roundTemplate.Add(pairings);

            var fixedTeam = rotation[0];
            var moved = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, moved);
            rotation[0] = fixedTeam;
        }

        for (var cycle = 0; rounds.Count < targetSeriesPerTeam; cycle++)
        {
            foreach (var templateRound in roundTemplate)
            {
                if (rounds.Count >= targetSeriesPerTeam)
                {
                    break;
                }

                var roundSeries = new List<SeriesMatchup>();
                foreach (var pairing in templateRound)
                {
                    var home = pairing.Home;
                    var away = pairing.Away;
                    if (cycle % 2 == 1)
                    {
                        (home, away) = (away, home);
                    }

                    roundSeries.Add(new SeriesMatchup(home, away));
                }

                rounds.Add(roundSeries);
            }
        }

        return rounds;
    }

    private static void WriteTeamsCsv(string path, List<TeamRow> teams)
    {
        var lines = new List<string> { "Team,Team Code,League,Division,City" };
        lines.AddRange(teams.Select(team => string.Join(',', Escape(team.Name), Escape(team.Abbreviation), Escape(team.League), Escape(team.Division), Escape(team.City))));
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    private static void WritePlayersCsv(string path, List<PlayerRow> players)
    {
        var lines = new List<string> { "Player ID,FullName,Primary Position,Secondary Position,Team,Age" };
        lines.AddRange(players.Select(player => string.Join(',', Escape(player.PlayerId.ToString()), Escape(player.FullName), Escape(player.PrimaryPosition), Escape(player.SecondaryPosition), Escape(player.TeamName), player.Age.ToString(CultureInfo.InvariantCulture))));
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    private static void WriteRostersCsv(string path, List<RosterRow> rosters)
    {
        var lines = new List<string> { "Player ID,Team,Player,Primary Position,Secondary Position,Lineup Slot,Rotation Slot" };
        lines.AddRange(rosters.Select(roster => string.Join(',', Escape(roster.PlayerId.ToString()), Escape(roster.TeamName), Escape(roster.PlayerName), Escape(roster.PrimaryPosition), Escape(roster.SecondaryPosition), Escape(roster.LineupSlot?.ToString(CultureInfo.InvariantCulture) ?? string.Empty), Escape(roster.RotationSlot?.ToString(CultureInfo.InvariantCulture) ?? string.Empty))));
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    private static void WriteScheduleCsv(string path, List<ScheduleRow> schedule)
    {
        var lines = new List<string> { "Date,Home,Away,Game Number,Ballpark" };
        lines.AddRange(schedule.Select(game => string.Join(',', Escape(game.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Escape(game.HomeTeamName), Escape(game.AwayTeamName), Escape(game.GameNumber.ToString(CultureInfo.InvariantCulture)), Escape(game.Venue))));
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }

    private static void RunImportWorkflow(string outputDirectory, string teamsCsvPath, string playersCsvPath, string rostersCsvPath, string scheduleCsvPath)
    {
        var imports = new[]
        {
            new ImportDefinition(teamsCsvPath, Path.Combine(outputDirectory, "teams.json"), "BaseballManager.Contracts.ImportDtos.TeamImportDto"),
            new ImportDefinition(playersCsvPath, Path.Combine(outputDirectory, "players.json"), "BaseballManager.Contracts.ImportDtos.PlayerImportDto"),
            new ImportDefinition(rostersCsvPath, Path.Combine(outputDirectory, "rosters.json"), "BaseballManager.Contracts.ImportDtos.RosterImportDto"),
            new ImportDefinition(scheduleCsvPath, Path.Combine(outputDirectory, "schedule.json"), "BaseballManager.Contracts.ImportDtos.ScheduleImportDto")
        };

        foreach (var import in imports)
        {
            var arguments = $"run --project tools/importers/BaseballManager.CsvImportTool/BaseballManager.CsvImportTool.csproj -- --csv \"{import.CsvPath}\" --out \"{import.JsonPath}\" --model {import.ModelType} --strict true";
            var startInfo = new ProcessStartInfo("dotnet", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start CSV import workflow.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Import failed for {import.CsvPath}: {standardError}{standardOutput}");
            }

            Console.WriteLine(standardOutput.Trim());
        }
    }

    private static string BuildAbbreviation(string city, string mascot, HashSet<string> existing)
    {
        var baseCode = (new string(city.Where(char.IsLetter).Take(2).ToArray()) + new string(mascot.Where(char.IsLetter).Take(1).ToArray())).ToUpperInvariant();
        if (baseCode.Length < 3)
        {
            baseCode = (baseCode + "XXX")[..3];
        }

        var abbreviation = baseCode;
        var suffix = 1;
        while (!existing.Add(abbreviation))
        {
            abbreviation = baseCode[..2] + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return abbreviation;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private sealed record GeneratorOptions(
        int TeamCount,
        int PlayersPerTeam,
        string OutputDirectory,
        int Seed,
        DateOnly StartDate,
        bool ImportAfterGenerate);

    private sealed record TeamRow(string Name, string Abbreviation, string League, string Division, string City, string Ballpark);
    private sealed record PlayerRow(Guid PlayerId, string FullName, string PrimaryPosition, string SecondaryPosition, string TeamName, int Age);
    private sealed record RosterRow(Guid PlayerId, string TeamName, string PlayerName, string PrimaryPosition, string SecondaryPosition, int? LineupSlot, int? RotationSlot);
    private sealed record ScheduleRow(DateOnly Date, string HomeTeamName, string AwayTeamName, int GameNumber, string Venue);
    private sealed record SeriesMatchup(TeamRow HomeTeam, TeamRow AwayTeam);
    private sealed record ImportDefinition(string CsvPath, string JsonPath, string ModelType);
}
