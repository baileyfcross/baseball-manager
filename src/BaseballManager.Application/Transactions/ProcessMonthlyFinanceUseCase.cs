using BaseballManager.Core.Economy;

namespace BaseballManager.Application.Transactions;

public sealed class ProcessMonthlyFinanceUseCase
{
    public FinancialSnapshot Execute(TeamEconomy economy, DateTime processDate, string notes)
    {
        var sponsorRevenue = FinanceMath.GetMonthlySponsorRevenue(economy);
        var playerPayrollExpense = decimal.Round(economy.PlayerPayroll / 12m, 2);
        var coachPayrollExpense = decimal.Round(economy.CoachPayroll / 12m, 2);
        var facilitiesUpkeep = 35_000m * Math.Max(1, economy.FacilitiesLevel);
        var travelExpense = economy.MarketSize switch
        {
            MarketSize.Small => 45_000m,
            MarketSize.Large => 80_000m,
            _ => 60_000m
        };

        var totalExpenses = playerPayrollExpense + coachPayrollExpense + economy.BudgetAllocation.TotalMonthlyInvestment + facilitiesUpkeep + travelExpense;
        economy.CashOnHand += sponsorRevenue - totalExpenses;
        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);

        var snapshot = new FinancialSnapshot
        {
            EffectiveDate = processDate.Date,
            Category = "Monthly Finance",
            Revenue = sponsorRevenue,
            Expenses = totalExpenses,
            Attendance = 0,
            FanInterest = economy.FanInterest,
            CashAfter = economy.CashOnHand,
            Notes = notes
        };

        FinanceMath.AddSnapshot(economy, snapshot);
        return snapshot;
    }
}
