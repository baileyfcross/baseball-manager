using BaseballManager.Core.Economy;

namespace BaseballManager.Application.Transactions;

public sealed class ProcessGameRevenueUseCase
{
    public FinancialSnapshot Execute(TeamEconomy economy, double winningPercentage, DateTime gameDate, string notes)
    {
        var attendanceRate = CalculateAttendanceRate(economy, winningPercentage);
        var attendance = Math.Clamp((int)Math.Round(economy.StadiumCapacity * attendanceRate), 8_000, economy.StadiumCapacity);
        var ticketRevenue = decimal.Round(attendance * economy.TicketPrice, 2);
        var merchRevenue = decimal.Round(attendance * (4.25m + (economy.MerchStrength / 18m)), 2);
        var concessionRevenue = decimal.Round(attendance * (6.50m + (economy.FanInterest / 18m)), 2);
        var totalRevenue = ticketRevenue + merchRevenue + concessionRevenue;

        economy.CashOnHand += totalRevenue;
        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);

        var snapshot = new FinancialSnapshot
        {
            EffectiveDate = gameDate.Date,
            Category = "Game Revenue",
            Revenue = totalRevenue,
            Expenses = 0m,
            Attendance = attendance,
            FanInterest = economy.FanInterest,
            CashAfter = economy.CashOnHand,
            Notes = notes
        };

        FinanceMath.AddSnapshot(economy, snapshot);
        return snapshot;
    }

    private static double CalculateAttendanceRate(TeamEconomy economy, double winningPercentage)
    {
        var marketFactor = economy.MarketSize switch
        {
            MarketSize.Small => -0.05d,
            MarketSize.Large => 0.12d,
            _ => 0.04d
        };

        var fanFactor = (economy.FanInterest - 50) / 100d;
        var performanceFactor = (winningPercentage - 0.5d) * 0.35d;
        return Math.Clamp(0.45d + marketFactor + fanFactor + performanceFactor, 0.28d, 0.98d);
    }
}
