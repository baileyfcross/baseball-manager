using BaseballManager.Core.Scheduling;

namespace BaseballManager.Core.League;

public sealed class Season
{
    public int Year { get; init; }
    public GameDate OpeningDay { get; init; } = GameDate.FromDateTime(DateTime.UtcNow.Date);
}
