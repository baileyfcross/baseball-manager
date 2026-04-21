namespace BaseballManager.Contracts.ImportDtos;

public sealed class RosterImportDto
{
    [CsvHeader("Player ID")]
    public Guid PlayerId { get; set; }

    [CsvHeader("Team")]
    [CsvHeader("Team Name")]
    public string TeamName { get; set; } = string.Empty;

    [CsvHeader("Player")]
    [CsvHeader("Player Name")]
    [CsvHeader("Full Name")]
    public string PlayerName { get; set; } = string.Empty;

    [CsvHeader("Primary Position")]
    [CsvHeader("PriPos")]
    [CsvHeader("Pos")]
    public string PrimaryPosition { get; set; } = string.Empty;

    [CsvHeader("Secondary Position")]
    [CsvHeader("SecPos")]
    public string SecondaryPosition { get; set; } = string.Empty;

    [CsvHeader("Lineup Slot")]
    [CsvHeader("Lineup")]
    [CsvHeader("Batting Order")]
    public int? LineupSlot { get; set; }

    [CsvHeader("Defensive Position")]
    [CsvHeader("Lineup Position")]
    [CsvHeader("Field Position")]
    public string DefensivePosition { get; set; } = string.Empty;

    [CsvHeader("Rotation Slot")]
    [CsvHeader("Rotation")]
    [CsvHeader("Starter Slot")]
    public int? RotationSlot { get; set; }
}
