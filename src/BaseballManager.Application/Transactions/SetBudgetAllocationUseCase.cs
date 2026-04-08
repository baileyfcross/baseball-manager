using BaseballManager.Core.Economy;

namespace BaseballManager.Application.Transactions;

public sealed class SetBudgetAllocationUseCase
{
    public void Execute(TeamEconomy economy, BudgetAllocation allocation)
    {
        economy.BudgetAllocation = new BudgetAllocation
        {
            ScoutingBudget = Math.Clamp(allocation.ScoutingBudget, 75_000m, 1_250_000m),
            PlayerDevelopmentBudget = Math.Clamp(allocation.PlayerDevelopmentBudget, 75_000m, 1_500_000m),
            MedicalBudget = Math.Clamp(allocation.MedicalBudget, 75_000m, 1_250_000m),
            FacilitiesBudget = Math.Clamp(allocation.FacilitiesBudget, 50_000m, 900_000m)
        };

        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
    }
}
