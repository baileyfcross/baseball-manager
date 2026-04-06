namespace BaseballManager.Core.Contracts;

public sealed class Contract
{
    public Guid PlayerId { get; init; }
    public decimal AnnualSalary { get; set; }
    public int YearsRemaining { get; set; }
}
