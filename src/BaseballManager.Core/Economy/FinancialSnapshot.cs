namespace BaseballManager.Core.Economy;

public sealed class FinancialSnapshot
{
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    public string Category { get; set; } = string.Empty;

    public decimal Revenue { get; set; }

    public decimal Expenses { get; set; }

    public int Attendance { get; set; }

    public int FanInterest { get; set; }

    public decimal CashAfter { get; set; }

    public string Notes { get; set; } = string.Empty;

    public decimal NetIncome => Revenue - Expenses;
}
