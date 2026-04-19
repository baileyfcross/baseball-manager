using BaseballManager.Application.Transactions;
using Xunit;

namespace BaseballManager.Core.Tests;

public sealed class OffseasonContractEvaluatorTests
{
    private readonly OffseasonContractEvaluator _evaluator = new();

    [Fact]
    public void EliteHitterSeasonRaisesPlayerExpectationAboveBaseline()
    {
        var average = _evaluator.Evaluate(new OffseasonContractContext(
            IsPitcher: false,
            Age: 27,
            OverallRating: 84,
            TeamNeedScore: 3,
            TeamBudgetRoom: 40_000_000m,
            CurrentAnnualSalary: 9_000_000m,
            RegularSeason: new OffseasonPerformanceSnapshot(
                PlateAppearances: 620,
                AtBats: 550,
                Hits: 148,
                Doubles: 26,
                Triples: 2,
                HomeRuns: 18,
                Walks: 54,
                RunsScored: 74,
                RunsBattedIn: 76,
                InningsPitchedOuts: 0,
                EarnedRuns: 0,
                StrikeoutsPitched: 0,
                Wins: 0,
                Losses: 0)));

        var result = _evaluator.Evaluate(new OffseasonContractContext(
            IsPitcher: false,
            Age: 27,
            OverallRating: 84,
            TeamNeedScore: 3,
            TeamBudgetRoom: 40_000_000m,
            CurrentAnnualSalary: 9_000_000m,
            RegularSeason: new OffseasonPerformanceSnapshot(
                PlateAppearances: 680,
                AtBats: 590,
                Hits: 190,
                Doubles: 38,
                Triples: 4,
                HomeRuns: 34,
                Walks: 72,
                RunsScored: 108,
                RunsBattedIn: 112,
                InningsPitchedOuts: 0,
                EarnedRuns: 0,
                StrikeoutsPitched: 0,
                Wins: 0,
                Losses: 0)));

        Assert.True(result.PlayerExpectation.AnnualSalary > average.PlayerExpectation.AnnualSalary);
        Assert.True(result.TeamExpectation.AnnualSalary > average.TeamExpectation.AnnualSalary);
    }

    [Fact]
    public void BudgetConstrainedTeamCanMissPlayerExpectation()
    {
        var result = _evaluator.Evaluate(new OffseasonContractContext(
            IsPitcher: false,
            Age: 29,
            OverallRating: 81,
            TeamNeedScore: 1,
            TeamBudgetRoom: 6_000_000m,
            CurrentAnnualSalary: 11_500_000m,
            RegularSeason: new OffseasonPerformanceSnapshot(
                PlateAppearances: 640,
                AtBats: 560,
                Hits: 165,
                Doubles: 30,
                Triples: 2,
                HomeRuns: 29,
                Walks: 58,
                RunsScored: 92,
                RunsBattedIn: 94,
                InningsPitchedOuts: 0,
                EarnedRuns: 0,
                StrikeoutsPitched: 0,
                Wins: 0,
                Losses: 0)));

        Assert.False(result.Accepted);
        Assert.True(result.TeamExpectation.Years <= result.PlayerExpectation.Years);
    }

    [Fact]
    public void StrongPitcherPerformanceImprovesPitcherMarket()
    {
        var average = _evaluator.Evaluate(new OffseasonContractContext(
            IsPitcher: true,
            Age: 30,
            OverallRating: 76,
            TeamNeedScore: 2,
            TeamBudgetRoom: 28_000_000m,
            CurrentAnnualSalary: 8_500_000m,
            RegularSeason: new OffseasonPerformanceSnapshot(
                PlateAppearances: 0,
                AtBats: 0,
                Hits: 0,
                Doubles: 0,
                Triples: 0,
                HomeRuns: 0,
                Walks: 0,
                RunsScored: 0,
                RunsBattedIn: 0,
                InningsPitchedOuts: 480,
                EarnedRuns: 78,
                StrikeoutsPitched: 122,
                Wins: 10,
                Losses: 11)));
        var strong = _evaluator.Evaluate(new OffseasonContractContext(
            IsPitcher: true,
            Age: 30,
            OverallRating: 76,
            TeamNeedScore: 2,
            TeamBudgetRoom: 28_000_000m,
            CurrentAnnualSalary: 8_500_000m,
            RegularSeason: new OffseasonPerformanceSnapshot(
                PlateAppearances: 0,
                AtBats: 0,
                Hits: 0,
                Doubles: 0,
                Triples: 0,
                HomeRuns: 0,
                Walks: 0,
                RunsScored: 0,
                RunsBattedIn: 0,
                InningsPitchedOuts: 585,
                EarnedRuns: 58,
                StrikeoutsPitched: 201,
                Wins: 16,
                Losses: 7)));

        Assert.True(strong.PlayerExpectation.AnnualSalary > average.PlayerExpectation.AnnualSalary);
        Assert.True(strong.TeamExpectation.AnnualSalary > average.TeamExpectation.AnnualSalary);
    }
}
