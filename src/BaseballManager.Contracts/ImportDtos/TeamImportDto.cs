namespace BaseballManager.Contracts.ImportDtos;

public sealed class TeamImportDto
{
    [CsvHeader("Team")]
    [CsvHeader("Team Name")]
    [CsvHeader("Club")]
    public string Name { get; set; } = string.Empty;

    [CsvHeader("Team Code")]
    [CsvHeader("Abbrev")]
    [CsvHeader("Abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [CsvHeader("League")]
    public string League { get; set; } = string.Empty;

    [CsvHeader("Division")]
    public string Division { get; set; } = string.Empty;

    [CsvHeader("City")]
    public string City { get; set; } = string.Empty;
}
