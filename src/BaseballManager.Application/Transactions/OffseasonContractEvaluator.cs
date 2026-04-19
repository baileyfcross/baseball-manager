namespace BaseballManager.Application.Transactions;

public sealed class OffseasonContractEvaluator
{
    public OffseasonContractEvaluation Evaluate(OffseasonContractContext context)
    {
        var performanceMultiplier = CalculatePerformanceMultiplier(context.RegularSeason, context.PlayoffPerformance, context.IsPitcher);
        var baseSalary = CalculateBaseSalary(context.OverallRating, context.Age, context.IsPitcher);
        var playerExpectedSalary = ClampSalary(baseSalary * performanceMultiplier * GetPlayerLeverageMultiplier(context.Age, context.CurrentAnnualSalary));
        var playerExpectedYears = DeterminePlayerExpectationYears(context.Age, context.OverallRating, performanceMultiplier, context.IsPitcher);

        var needMultiplier = 0.90m + (Math.Clamp(context.TeamNeedScore, 0, 4) * 0.08m);
        var budgetMultiplier = GetBudgetMultiplier(context.TeamBudgetRoom, playerExpectedSalary);
        var teamExpectedSalary = ClampSalary(baseSalary * performanceMultiplier * needMultiplier * budgetMultiplier);
        teamExpectedSalary = Math.Max(teamExpectedSalary, Math.Min(playerExpectedSalary, Math.Max(context.CurrentAnnualSalary * 0.92m, 600_000m)));
        var teamExpectedYears = DetermineTeamExpectationYears(context.Age, context.OverallRating, context.TeamNeedScore, playerExpectedYears, context.TeamBudgetRoom, teamExpectedSalary);

        var minAcceptedSalary = ClampSalary(playerExpectedSalary * 0.93m);
        var accepted = teamExpectedSalary >= minAcceptedSalary && teamExpectedYears >= Math.Max(1, playerExpectedYears - 1);
        var agreedSalary = accepted
            ? ClampSalary(decimal.Round((teamExpectedSalary + playerExpectedSalary) / 2m, 2, MidpointRounding.AwayFromZero))
            : 0m;
        var agreedYears = accepted
            ? Math.Max(1, Math.Min(teamExpectedYears, playerExpectedYears))
            : 0;

        return new OffseasonContractEvaluation(
            new OffseasonContractExpectation(playerExpectedSalary, playerExpectedYears),
            new OffseasonContractExpectation(teamExpectedSalary, teamExpectedYears),
            accepted,
            agreedSalary,
            agreedYears,
            performanceMultiplier);
    }

    private static decimal CalculateBaseSalary(int overallRating, int age, bool isPitcher)
    {
        var salary = 650_000m + (Math.Max(0, overallRating - 45) * 170_000m);
        salary *= age switch
        {
            <= 24 => 0.82m,
            >= 34 => 0.94m,
            _ => 1.00m
        };

        if (isPitcher)
        {
            salary *= 1.08m;
        }

        return salary;
    }

    private static decimal CalculatePerformanceMultiplier(OffseasonPerformanceSnapshot regularSeason, OffseasonPerformanceSnapshot? playoffPerformance, bool isPitcher)
    {
        var regularSeasonScore = ScorePerformance(regularSeason, isPitcher);
        var playoffScore = playoffPerformance == null ? 0m : ScorePerformance(playoffPerformance, isPitcher) * 0.22m;
        return Math.Clamp(0.82m + regularSeasonScore + playoffScore, 0.82m, 1.60m);
    }

    private static decimal ScorePerformance(OffseasonPerformanceSnapshot snapshot, bool isPitcher)
    {
        if (isPitcher)
        {
            if (snapshot.InningsPitchedOuts <= 0)
            {
                return 0m;
            }

            var inningsPitched = snapshot.InningsPitchedOuts / 3m;
            var earnedRunAverage = inningsPitched <= 0m ? 9m : (snapshot.EarnedRuns * 9m) / inningsPitched;
            var strikeoutsPerNine = inningsPitched <= 0m ? 0m : (snapshot.StrikeoutsPitched * 9m) / inningsPitched;
            var volumeFactor = Math.Clamp(inningsPitched / 180m, 0.35m, 1.15m);
            var eraScore = Math.Clamp((4.90m - earnedRunAverage) / 5.5m, -0.10m, 0.24m);
            var strikeoutScore = Math.Clamp((strikeoutsPerNine - 6.8m) / 18m, -0.04m, 0.10m);
            var winScore = Math.Clamp((snapshot.Wins - snapshot.Losses) / 45m, -0.04m, 0.06m);
            return Math.Clamp((eraScore + strikeoutScore + winScore) * volumeFactor, -0.08m, 0.34m);
        }

        if (snapshot.PlateAppearances <= 0)
        {
            return 0m;
        }

        var onBase = (snapshot.Hits + snapshot.Walks) / (decimal)Math.Max(1, snapshot.PlateAppearances);
        var singles = Math.Max(0, snapshot.Hits - snapshot.Doubles - snapshot.Triples - snapshot.HomeRuns);
        var totalBases = singles + (snapshot.Doubles * 2) + (snapshot.Triples * 3) + (snapshot.HomeRuns * 4);
        var slugging = snapshot.AtBats <= 0 ? 0m : totalBases / (decimal)snapshot.AtBats;
        var ops = onBase + slugging;
        var plateAppearanceVolume = Math.Clamp(snapshot.PlateAppearances / 650m, 0.35m, 1.15m);
        var opsScore = Math.Clamp((ops - 0.700m) / 1.20m, -0.08m, 0.26m);
        var powerScore = Math.Clamp(snapshot.HomeRuns / 220m, 0m, 0.12m);
        var runCreationScore = Math.Clamp((snapshot.RunsBattedIn + snapshot.RunsScored) / 420m, 0m, 0.10m);
        return Math.Clamp((opsScore + powerScore + runCreationScore) * plateAppearanceVolume, -0.08m, 0.36m);
    }

    private static decimal GetPlayerLeverageMultiplier(int age, decimal currentAnnualSalary)
    {
        var multiplier = age switch
        {
            <= 25 => 1.06m,
            <= 30 => 1.02m,
            >= 35 => 0.94m,
            _ => 1.00m
        };

        if (currentAnnualSalary >= 18_000_000m)
        {
            multiplier += 0.03m;
        }

        return multiplier;
    }

    private static decimal GetBudgetMultiplier(decimal budgetRoom, decimal playerExpectedSalary)
    {
        if (budgetRoom >= playerExpectedSalary * 3.2m)
        {
            return 1.06m;
        }

        if (budgetRoom >= playerExpectedSalary * 2.1m)
        {
            return 1.00m;
        }

        if (budgetRoom >= playerExpectedSalary * 1.2m)
        {
            return 0.95m;
        }

        return 0.88m;
    }

    private static int DeterminePlayerExpectationYears(int age, int overallRating, decimal performanceMultiplier, bool isPitcher)
    {
        var years = age switch
        {
            <= 24 => 5,
            <= 28 => 4,
            <= 31 => 3,
            <= 34 => 2,
            _ => 1
        };

        if (overallRating >= 78 && performanceMultiplier >= 1.08m)
        {
            years++;
        }

        if (isPitcher && age >= 33)
        {
            years--;
        }

        return Math.Clamp(years, 1, 6);
    }

    private static int DetermineTeamExpectationYears(int age, int overallRating, int teamNeedScore, int playerExpectedYears, decimal budgetRoom, decimal teamExpectedSalary)
    {
        var years = Math.Min(playerExpectedYears, age <= 28 ? 4 : age <= 32 ? 3 : 2);
        if (overallRating >= 80 && teamNeedScore >= 3)
        {
            years++;
        }

        if (budgetRoom < teamExpectedSalary * 2m)
        {
            years--;
        }

        return Math.Clamp(years, 1, 5);
    }

    private static decimal ClampSalary(decimal salary)
    {
        return decimal.Round(Math.Clamp(salary, 600_000m, 45_000_000m), 2, MidpointRounding.AwayFromZero);
    }
}

public sealed record OffseasonContractContext(
    bool IsPitcher,
    int Age,
    int OverallRating,
    int TeamNeedScore,
    decimal TeamBudgetRoom,
    decimal CurrentAnnualSalary,
    OffseasonPerformanceSnapshot RegularSeason,
    OffseasonPerformanceSnapshot? PlayoffPerformance = null);

public sealed record OffseasonPerformanceSnapshot(
    int PlateAppearances,
    int AtBats,
    int Hits,
    int Doubles,
    int Triples,
    int HomeRuns,
    int Walks,
    int RunsScored,
    int RunsBattedIn,
    int InningsPitchedOuts,
    int EarnedRuns,
    int StrikeoutsPitched,
    int Wins,
    int Losses);

public sealed record OffseasonContractExpectation(decimal AnnualSalary, int Years);

public sealed record OffseasonContractEvaluation(
    OffseasonContractExpectation PlayerExpectation,
    OffseasonContractExpectation TeamExpectation,
    bool Accepted,
    decimal AgreedAnnualSalary,
    int AgreedYears,
    decimal PerformanceMultiplier);
