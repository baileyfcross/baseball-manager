using BaseballManager.Core.Economy;

namespace BaseballManager.Application.Transactions;

public static class FinanceMath
{
    public static decimal GetMonthlySponsorRevenue(TeamEconomy economy)
    {
        var marketBase = economy.MarketSize switch
        {
            MarketSize.Small => 250_000m,
            MarketSize.Large => 725_000m,
            _ => 450_000m
        };

        var sponsorFactor = 0.75m + (economy.SponsorStrength / 100m);
        var fanFactor = 0.70m + (economy.FanInterest / 100m);
        return decimal.Round(marketBase * sponsorFactor * fanFactor, 2);
    }

    public static decimal CalculateProjectedBudget(TeamEconomy economy)
    {
        var projectedSponsorRevenue = GetMonthlySponsorRevenue(economy) * 6m;
        var projectedExpenses = ((economy.PlayerPayroll + economy.CoachPayroll) / 2m) + (economy.BudgetAllocation.TotalMonthlyInvestment * 6m) + (40_000m * economy.FacilitiesLevel * 6m);
        return decimal.Round(Math.Max(0m, economy.CashOnHand + projectedSponsorRevenue - projectedExpenses), 2);
    }

    public static void AddSnapshot(TeamEconomy economy, FinancialSnapshot snapshot)
    {
        economy.FinancialHistory.Add(snapshot);
        economy.FinancialHistory = economy.FinancialHistory
            .OrderByDescending(entry => entry.EffectiveDate)
            .Take(24)
            .ToList();
    }
}
