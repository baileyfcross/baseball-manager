using BaseballManager.Core.Economy;

namespace BaseballManager.Application.Transactions;

public sealed class ReleasePlayerUseCase
{
    public decimal Execute(TeamEconomy economy, Guid playerId, decimal buyoutRate = 0.25m)
    {
        var contract = economy.PlayerContracts.FirstOrDefault(existing => existing.SubjectId == playerId);
        if (contract == null)
        {
            return 0m;
        }

        var releaseCost = decimal.Round(contract.AnnualSalary * Math.Clamp(buyoutRate, 0m, 1m), 2);
        economy.PlayerContracts.Remove(contract);
        economy.CashOnHand -= releaseCost;
        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
        return releaseCost;
    }
}
