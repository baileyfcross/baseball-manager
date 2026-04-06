namespace BaseballManager.Core.Scheduling;

public readonly record struct GameDate(int Year, int Month, int Day)
{
    public static GameDate FromDateTime(DateTime dateTime) =>
        new(dateTime.Year, dateTime.Month, dateTime.Day);

    public DateTime ToDateTime() => new(Year, Month, Day);
}
