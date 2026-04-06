namespace BaseballManager.Core.Injuries;

public sealed class Injury
{
    public Guid PlayerId { get; init; }
    public string Description { get; set; } = string.Empty;
    public int DaysRemaining { get; set; }
}
