namespace BaseballManager.Contracts.ImportDtos;

public sealed class PlayerImportDto
{
    public string FullName { get; set; } = string.Empty;

    [CsvHeader("Primary Position")]
    [CsvHeader("Pos")]
    public string PrimaryPosition { get; set; } = string.Empty;

    [CsvHeader("Team")]
    public string TeamName { get; set; } = string.Empty;
}
