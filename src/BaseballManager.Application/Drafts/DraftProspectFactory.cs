using BaseballManager.Core.Drafts;
using System.Security.Cryptography;
using System.Text;

namespace BaseballManager.Application.Drafts;

public sealed class DraftProspectFactory
{
    private static readonly string[] FirstNames = ["Mason", "Jace", "Noah", "Liam", "Elijah", "Carter", "Diego", "Luis", "Mateo", "Yuki", "Hiro", "Seong", "Min", "Adrian", "Roman", "Trey", "Evan", "Hudson", "Dante", "Kai"];
    private static readonly string[] LastNames = ["Johnson", "Miller", "Clark", "Ramirez", "De La Cruz", "Santos", "Kim", "Park", "Tanaka", "Sato", "Rivera", "Torres", "Gonzalez", "Lee", "Martinez", "Flores", "Young", "Brooks", "Diaz", "Morales"];
    private static readonly string[] Positions = ["C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "SP", "RP"];
    private static readonly string[] Sources = ["High School", "College", "International"];
    private static readonly string[] CollegePrograms = ["Coastal State", "North Valley", "Western Tech", "Lakefront U", "Pioneer State", "Metro A&M", "Redwood College", "Southeastern U"];
    private static readonly string[] HighSchools = ["Briarwood Prep", "East Harbor HS", "Mesa Ridge HS", "Southport Academy", "North Creek HS", "Canyon View Prep", "Kingsley HS", "St. Brendan's"];
    private static readonly string[] InternationalClubs = ["Santo Domingo Selects", "Tokyo Blue Stars", "Monterrey Industriales", "Seoul Phoenix", "Havana Capital", "Caracas Metropolitan", "San Juan Mariners", "Taipei Storm"];
    private static readonly string[] Summaries =
    [
        "Polished bat with room to grow.",
        "Athletic defender with intriguing upside.",
        "Projectable arm with starter traits.",
        "Late bloomer whose tools have started to click.",
        "Competitive profile with a chance to move quickly.",
        "Raw but toolsy prospect scouts keep circling back to."
    ];

    public IReadOnlyList<DraftProspect> CreateProspects(int seasonYear, int count)
    {
        var safeCount = Math.Max(1, count);
        var prospects = new List<DraftProspect>(safeCount);

        for (var index = 0; index < safeCount; index++)
        {
            prospects.Add(CreateProspect(seasonYear, index));
        }

        return prospects;
    }

    private static DraftProspect CreateProspect(int seasonYear, int index)
    {
        var random = new Random(CreateSeed(seasonYear, index));
        var source = Pick(random, Sources);
        var primaryPosition = Pick(random, Positions);
        var secondaryPosition = BuildSecondaryPosition(random, primaryPosition);
        var talentOutcome = BuildTalentOutcome(random);
        var age = source switch
        {
            "High School" => 17 + random.Next(0, 2),
            "College" => 20 + random.Next(0, 3),
            _ => 18 + random.Next(0, 4)
        };

        var overall = BuildOverall(random, primaryPosition, source, talentOutcome);
        var potential = BuildPotential(random, overall, source, talentOutcome);
        var playerName = $"{Pick(random, FirstNames)} {Pick(random, LastNames)}";
        var sourceTeamName = BuildSourceTeamName(random, source);

        return new DraftProspect(
            CreateDeterministicGuid($"draft-{seasonYear}-{index}-{playerName}-{primaryPosition}"),
            playerName,
            primaryPosition,
            secondaryPosition,
            age,
            overall,
            potential,
            source,
            Pick(random, Summaries),
            BuildScoutSummary(primaryPosition, overall, talentOutcome),
            BuildPotentialSummary(potential, talentOutcome),
            sourceTeamName,
            BuildSourceStatsSummary(random, source, sourceTeamName, primaryPosition, overall, talentOutcome),
            talentOutcome);
    }

    private static string BuildSecondaryPosition(Random random, string primaryPosition)
    {
        return primaryPosition switch
        {
            "C" => Pick(random, new[] { string.Empty, string.Empty, string.Empty, "1B" }),
            "1B" => Pick(random, new[] { string.Empty, string.Empty, "3B", "LF", "RF" }),
            "2B" => Pick(random, new[] { string.Empty, string.Empty, "SS", "3B", "CF" }),
            "3B" => Pick(random, new[] { string.Empty, string.Empty, "1B", "SS", "LF" }),
            "SS" => Pick(random, new[] { string.Empty, string.Empty, "2B", "3B", "CF" }),
            "LF" => Pick(random, new[] { string.Empty, string.Empty, "CF", "RF", "3B" }),
            "CF" => Pick(random, new[] { string.Empty, string.Empty, "LF", "RF", "2B", "SS" }),
            "RF" => Pick(random, new[] { string.Empty, string.Empty, "LF", "CF", "1B" }),
            "SP" => Pick(random, new[] { string.Empty, string.Empty, "RP" }),
            "RP" => Pick(random, new[] { string.Empty, string.Empty, "SP" }),
            _ => string.Empty
        };
    }

    private static int BuildOverall(Random random, string primaryPosition, string source, string talentOutcome)
    {
        var baseOverall = source switch
        {
            "College" => 46 + random.Next(0, 26),
            "International" => 42 + random.Next(0, 28),
            _ => 38 + random.Next(0, 28)
        };

        var positionalBonus = primaryPosition switch
        {
            "SP" => 3,
            "SS" or "CF" or "C" => 2,
            _ => 0
        };

        var outcomeModifier = talentOutcome switch
        {
            "Bust" => -8 + random.Next(0, 7),
            "Gem" => 6 + random.Next(0, 7),
            _ => -1 + random.Next(0, 6)
        };

        return Math.Clamp(baseOverall + positionalBonus + outcomeModifier, 28, 90);
    }

    private static int BuildPotential(Random random, int overall, string source, string talentOutcome)
    {
        var sourceModifier = source switch
        {
            "High School" => 5,
            "International" => 3,
            _ => 0
        };

        var growthWindow = talentOutcome switch
        {
            "Bust" => 2 + random.Next(0, 9),
            "Gem" => 15 + random.Next(0, 14),
            _ => 8 + random.Next(0, 13)
        };

        return Math.Clamp(overall + sourceModifier + growthWindow, overall + 1, 99);
    }

    private static string BuildTalentOutcome(Random random)
    {
        var roll = random.Next(100);
        return roll switch
        {
            < 22 => "Bust",
            < 82 => "Normal",
            _ => "Gem"
        };
    }

    private static string BuildScoutSummary(string primaryPosition, int overall, string talentOutcome)
    {
        var role = primaryPosition switch
        {
            "SP" or "RP" => "arm",
            "C" => "catch-and-throw skill set",
            "SS" or "2B" or "3B" => "infield package",
            "CF" or "LF" or "RF" => "outfield tool set",
            _ => "bat"
        };

        return (overall, talentOutcome) switch
        {
            ( >= 78, "Gem") => $"Scout sees impact {role} with first-division upside.",
            ( >= 72, _) => $"Scout projects a strong everyday profile if the {role} keeps trending up.",
            ( >= 64, _) => $"Scout likes the foundation, but the {role} still needs refinement.",
            ( >= 55, _) => $"Scout report is split: there is one carrying tool, but the floor is shaky.",
            (_, "Bust") => $"Scout warns this is more athlete than baseball player right now.",
            _ => $"Scout sees a developmental flier who needs time in the affiliate pipeline."
        };
    }

    private static string BuildPotentialSummary(int potential, string talentOutcome)
    {
        if (talentOutcome == "Gem" && potential >= 85)
        {
            return "Star ceiling";
        }

        if (potential >= 78)
        {
            return "Impact regular upside";
        }

        if (potential >= 70)
        {
            return "Solid big-league ceiling";
        }

        if (potential >= 60)
        {
            return "Bench or backend ceiling";
        }

        return "Long-shot upside";
    }

    private static string BuildSourceTeamName(Random random, string source)
    {
        return source switch
        {
            "College" => Pick(random, CollegePrograms),
            "High School" => Pick(random, HighSchools),
            _ => Pick(random, InternationalClubs)
        };
    }

    private static string BuildSourceStatsSummary(Random random, string source, string sourceTeamName, string primaryPosition, int overall, string talentOutcome)
    {
        if (primaryPosition is "SP" or "RP")
        {
            var innings = source switch
            {
                "College" => 58 + random.Next(0, 56),
                "High School" => 34 + random.Next(0, 48),
                _ => 40 + random.Next(0, 52)
            };
            var eraBase = talentOutcome switch
            {
                "Gem" => 180,
                "Bust" => 390,
                _ => 275
            };
            var era = Math.Max(1.45m, (eraBase + random.Next(-35, 61)) / 100m);
            var strikeouts = Math.Max(28, (int)Math.Round(innings * (overall / 11m))) + random.Next(0, 18);
            var wins = Math.Max(1, innings / 12 + random.Next(0, 4));
            var losses = Math.Max(0, wins - 1 + random.Next(0, 4));
            return $"{sourceTeamName}: {wins}-{losses}, {era:0.00} ERA, {strikeouts} K in {innings:0.0} IP";
        }

        var games = source switch
        {
            "College" => 42 + random.Next(0, 18),
            "High School" => 24 + random.Next(0, 16),
            _ => 36 + random.Next(0, 22)
        };
        var averageBase = talentOutcome switch
        {
            "Gem" => 290,
            "Bust" => 214,
            _ => 252
        };
        var average = Math.Clamp((averageBase + random.Next(-24, 43)) / 1000m, 0.180m, 0.382m);
        var homeRuns = Math.Max(0, (overall - 35) / 6 + random.Next(0, 6));
        var steals = primaryPosition is "CF" or "SS" or "2B"
            ? Math.Max(1, (overall - 35) / 5 + random.Next(0, 10))
            : random.Next(0, 6);
        return $"{sourceTeamName}: {average:0.000} AVG, {homeRuns} HR, {steals} SB in {games} G";
    }

    private static T Pick<T>(Random random, IReadOnlyList<T> options)
    {
        return options[random.Next(options.Count)];
    }

    private static int CreateSeed(int seasonYear, int index)
    {
        return HashCode.Combine(seasonYear, index, 1903);
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = MD5.HashData(bytes);
        return new Guid(hash);
    }
}
