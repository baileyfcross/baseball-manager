namespace BaseballManager.Core.Economy;

public sealed class Contract
{
    public Guid SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public decimal AnnualSalary { get; set; }

    public int YearsRemaining { get; set; } = 1;

    public DateTime SignedDate { get; set; } = DateTime.UtcNow;

    public bool IsCoach { get; set; }
}
