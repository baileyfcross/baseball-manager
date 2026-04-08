using BaseballManager.Core.Economy;

namespace BaseballManager.Application.Transactions;

public sealed class HireCoachUseCase
{
    public Contract Execute(TeamEconomy economy, Guid coachId, string coachName, string role, decimal annualSalary, DateTime signedDate)
    {
        var contract = economy.CoachContracts.FirstOrDefault(existing => existing.SubjectId == coachId || string.Equals(existing.Role, role, StringComparison.OrdinalIgnoreCase));
        if (contract == null)
        {
            contract = new Contract
            {
                SubjectId = coachId,
                SubjectName = coachName,
                Role = role,
                IsCoach = true
            };
            economy.CoachContracts.Add(contract);
        }

        contract.SubjectName = coachName;
        contract.Role = role;
        contract.AnnualSalary = Math.Clamp(annualSalary, 150_000m, 4_000_000m);
        contract.YearsRemaining = Math.Clamp(contract.YearsRemaining <= 0 ? 2 : contract.YearsRemaining, 1, 4);
        contract.SignedDate = signedDate;
        contract.IsCoach = true;
        economy.ProjectedBudget = FinanceMath.CalculateProjectedBudget(economy);
        return contract;
    }
}
