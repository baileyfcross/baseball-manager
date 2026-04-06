# Import DTOs

Import DTOs describe the target model shape used by CSV imports.

Guidelines:

- Add one DTO per import shape (players, teams, schedules, etc.)
- Property names should match CSV headers by default
- Use `[CsvHeader("Header Name")]` to map alternate header names

Example:

```csharp
public sealed class PlayerImportDto
{
  public string FullName { get; set; } = string.Empty;

  [CsvHeader("Primary Position")]
  [CsvHeader("Pos")]
  public string PrimaryPosition { get; set; } = string.Empty;
}
```
