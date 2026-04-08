namespace BaseballManager.Core.Economy;

public sealed class TeamEconomy
{
    public decimal CashOnHand { get; set; } = 22_500_000m;

    public decimal ProjectedBudget { get; set; } = 82_000_000m;

    public MarketSize MarketSize { get; set; } = MarketSize.Medium;

    public int FanInterest { get; set; } = 50;

    public decimal TicketPrice { get; set; } = 28m;

    public int StadiumCapacity { get; set; } = 36_000;

    public int MerchStrength { get; set; } = 50;

    public int SponsorStrength { get; set; } = 50;

    public int FacilitiesLevel { get; set; } = 2;

    public BudgetAllocation BudgetAllocation { get; set; } = new();

    public List<Contract> PlayerContracts { get; set; } = [];

    public List<Contract> CoachContracts { get; set; } = [];

    public List<FinancialSnapshot> FinancialHistory { get; set; } = [];

    public decimal PlayerPayroll => PlayerContracts.Sum(contract => contract.AnnualSalary);

    public decimal CoachPayroll => CoachContracts.Sum(contract => contract.AnnualSalary);

    public decimal PayrollCommitments => PlayerPayroll + CoachPayroll;
}

public enum MarketSize
{
    Small,
    Medium,
    Large
}
