using BaseballManager.Core.Economy;
using BaseballManager.Core.Players;
using BaseballManager.Core.Scheduling;
using BaseballManager.Core.Teams;
using Xunit;

namespace BaseballManager.Core.Tests;

public sealed class CoreDomainTests
{
    [Fact]
    public void FromDateTime_And_ToDateTime_RoundTrip_TheCalendarDay()
    {
        var sourceDate = new DateTime(2026, 4, 8, 14, 30, 0, DateTimeKind.Utc);

        var gameDate = GameDate.FromDateTime(sourceDate);

        Assert.Equal(new GameDate(2026, 4, 8), gameDate);
        Assert.Equal(new DateTime(2026, 4, 8), gameDate.ToDateTime());
    }

    [Fact]
    public void TotalMonthlyInvestment_Sums_All_Budget_Buckets()
    {
        var allocation = new BudgetAllocation
        {
            ScoutingBudget = 120_000m,
            PlayerDevelopmentBudget = 185_000m,
            MedicalBudget = 95_000m,
            FacilitiesBudget = 140_000m
        };

        Assert.Equal(540_000m, allocation.TotalMonthlyInvestment);
    }

    [Fact]
    public void PayrollCommitments_Add_Player_And_Coach_Salaries()
    {
        var economy = new TeamEconomy
        {
            PlayerContracts =
            [
                new Contract { SubjectName = "Starter", AnnualSalary = 4_500_000m },
                new Contract { SubjectName = "Utility Bat", AnnualSalary = 1_250_000m }
            ],
            CoachContracts =
            [
                new Contract { SubjectName = "Bench Coach", AnnualSalary = 750_000m, IsCoach = true }
            ]
        };

        Assert.Equal(5_750_000m, economy.PlayerPayroll);
        Assert.Equal(750_000m, economy.CoachPayroll);
        Assert.Equal(6_500_000m, economy.PayrollCommitments);
    }

    [Fact]
    public void IsValidForRoster_ReturnsTrue_ForNineUniquePlayersFromTheTeamRoster()
    {
        var roster = Enumerable.Range(1, 9)
            .Select(index => new Player { FullName = $"Player {index}", PrimaryPosition = "OF" })
            .ToList();
        var lineup = new Lineup { TeamId = Guid.NewGuid() };
        lineup.BattingOrder.AddRange(roster.Select(player => player.Id));

        var isValid = lineup.IsValidForRoster(roster);

        Assert.True(isValid);
    }

    [Fact]
    public void IsValidForRoster_ReturnsFalse_WhenTheSamePlayerAppearsTwice()
    {
        var roster = Enumerable.Range(1, 9)
            .Select(index => new Player { FullName = $"Player {index}", PrimaryPosition = "IF" })
            .ToList();
        var lineup = new Lineup { TeamId = Guid.NewGuid() };
        lineup.BattingOrder.AddRange(roster.Take(8).Select(player => player.Id));
        lineup.BattingOrder.Add(roster[0].Id);

        var isValid = lineup.IsValidForRoster(roster);

        Assert.False(isValid);
    }

    [Fact]
    public void IsValidForRoster_ReturnsFalse_WhenABatterIsNotOnTheRoster()
    {
        var roster = Enumerable.Range(1, 9)
            .Select(index => new Player { FullName = $"Player {index}", PrimaryPosition = "C" })
            .ToList();
        var outsider = new Player { FullName = "Call-Up", PrimaryPosition = "DH" };
        var lineup = new Lineup { TeamId = Guid.NewGuid() };
        lineup.BattingOrder.AddRange(roster.Take(8).Select(player => player.Id));
        lineup.BattingOrder.Add(outsider.Id);

        var isValid = lineup.IsValidForRoster(roster);

        Assert.False(isValid);
    }
}
