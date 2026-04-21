namespace BaseballManager.Contracts.ImportDtos;

public sealed class PlayerImportDto
{
    [CsvHeader("Player ID")]
    public Guid PlayerId { get; set; }

    public string FullName { get; set; } = string.Empty;

    [CsvHeader("Primary Position")]
    [CsvHeader("PriPos")]
    public string PrimaryPosition { get; set; } = string.Empty;


    [CsvHeader("Secondary Position")]
    [CsvHeader("SecPos")]
    public string SecondaryPosition { get; set; } = string.Empty;

    [CsvHeader("Team")]
    public string TeamName { get; set; } = string.Empty;

    [CsvHeader("Age")]
    public int Age { get; set; }

    [CsvHeader("Throws")]
    public string Throws { get; set; } = "R";

    [CsvHeader("Batting Style")]
    [CsvHeader("Bats")]
    public string BattingStyle { get; set; } = "R";
}
