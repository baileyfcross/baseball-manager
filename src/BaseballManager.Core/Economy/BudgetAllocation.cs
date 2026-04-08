namespace BaseballManager.Core.Economy;

public sealed class BudgetAllocation
{
    public decimal ScoutingBudget { get; set; } = 225_000m;

    public decimal PlayerDevelopmentBudget { get; set; } = 275_000m;

    public decimal MedicalBudget { get; set; } = 200_000m;

    public decimal FacilitiesBudget { get; set; } = 150_000m;

    public decimal TotalMonthlyInvestment => ScoutingBudget + PlayerDevelopmentBudget + MedicalBudget + FacilitiesBudget;
}
