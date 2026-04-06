namespace BaseballManager.Contracts.ImportDtos;

public sealed class ScheduleImportDto
{
    [CsvHeader("Date")]
    [CsvHeader("Game Date")]
    public DateTime Date { get; set; }

    [CsvHeader("Home")]
    [CsvHeader("Home Team")]
    public string HomeTeamName { get; set; } = string.Empty;

    [CsvHeader("Away")]
    [CsvHeader("Away Team")]
    public string AwayTeamName { get; set; } = string.Empty;

    [CsvHeader("Game Number")]
    [CsvHeader("Game No")]
    public int? GameNumber { get; set; }

    [CsvHeader("Ballpark")]
    [CsvHeader("Venue")]
    public string Venue { get; set; } = string.Empty;
}
