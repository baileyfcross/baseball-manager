using BaseballManager.Core.Economy;

namespace BaseballManager.Application.Transactions;

public sealed class SignPlayerContractUseCase
{
    public Contract Execute(TeamEconomy economy, Guid playerId, string playerName, decimal annualSalary, int yearsRemaining, DateTime signedDate)
    {
        var contract = economy.PlayerContracts.FirstOrDefault(existing => existing.SubjectId == playerId);
        if (contract == null)
        {
            contract = new Contract
            {
                SubjectId = playerId,
                IsCoach = false
            };
            economy.PlayerContracts.Add(contract);
        }

        contract.SubjectName = playerName;
        contract.Role = "Player";
        contract.AnnualSalary = Math.Clamp(annualSalary, 600_000m, 45_000_000m);
        contract.YearsRemaining = Math.Clamp(yearsRemaining, 1, 8);
        contract.SignedDate = signedDate;
        contract.IsCoach = false;
        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
        return contract;
    }
}
