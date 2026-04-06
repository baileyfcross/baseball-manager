# Importers

Tools in this folder convert CSV source data into JSON files the game can use.

## CSV Import Tool

Project:

- `tools/importers/BaseballManager.CsvImportTool/BaseballManager.CsvImportTool.csproj`

Usage:

```bash
dotnet run --project tools/importers/BaseballManager.CsvImportTool/BaseballManager.CsvImportTool.csproj -- \
	--csv data/imports/players.csv \
	--out data/imports/players.json \
	--model BaseballManager.Contracts.ImportDtos.PlayerImportDto \
	--strict true
```

Optional:

- `--assembly <path/to/assembly.dll>` when the model type is not in referenced assemblies
- `--strict false` to allow missing headers

How mapping works:

- Row 1 is treated as CSV headers
- Each data row is mapped to the target model type by header name
- Header matching ignores case, spaces, and punctuation
- A model property can define alternate headers with `[CsvHeader("Header Name")]`
