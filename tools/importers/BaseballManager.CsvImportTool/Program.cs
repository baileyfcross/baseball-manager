using System.Reflection;
using System.Text.Json;

namespace BaseballManager.CsvImportTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);

            if (!File.Exists(options.CsvPath))
            {
                Console.Error.WriteLine($"CSV file not found: {options.CsvPath}");
                return 1;
            }

            var modelType = ResolveModelType(options.ModelTypeName, options.AssemblyPath);
            var csvText = File.ReadAllText(options.CsvPath);
            var rows = CsvParser.Parse(csvText);

            if (rows.Count < 2)
            {
                Console.Error.WriteLine("CSV must contain a header row and at least one data row.");
                return 1;
            }

            var headers = rows[0];
            var items = new List<object>();

            for (var i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                try
                {
                    items.Add(RowMapper.MapRow(modelType, headers, row, options.Strict));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Row {i + 1} failed: {ex.Message}");
                    return 1;
                }
            }

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(items, jsonOptions);

            var outputDirectory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(options.OutputPath, json);

            Console.WriteLine($"Imported {items.Count} rows as {modelType.Name} and wrote JSON to:");
            Console.WriteLine(options.OutputPath);
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Import failed: {ex.Message}");
            return 1;
        }
    }

    private static ImportOptions ParseOptions(string[] args)
    {
        string? csv = null;
        string? output = null;
        string? model = null;
        string? assemblyPath = null;
        var strict = true;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            var value = i + 1 < args.Length ? args[i + 1] : null;

            switch (key)
            {
                case "csv":
                    csv = RequireValue(key, value);
                    i++;
                    break;
                case "out":
                    output = RequireValue(key, value);
                    i++;
                    break;
                case "model":
                    model = RequireValue(key, value);
                    i++;
                    break;
                case "assembly":
                    assemblyPath = RequireValue(key, value);
                    i++;
                    break;
                case "strict":
                    strict = bool.Parse(RequireValue(key, value));
                    i++;
                    break;
                case "help":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown option '--{key}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(csv) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Missing required options: --csv, --out, --model");
        }

        return new ImportOptions(csv!, output!, model!, assemblyPath, strict);
    }

    private static string RequireValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '--{key}' requires a value.");
        }

        return value;
    }

    private static Type ResolveModelType(string modelTypeName, string? assemblyPath)
    {
        Assembly? assembly = null;

        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            if (!File.Exists(assemblyPath))
            {
                throw new ArgumentException($"Assembly path does not exist: {assemblyPath}");
            }

            assembly = Assembly.LoadFrom(assemblyPath);
        }

        Type? modelType = null;

        if (assembly != null)
        {
            modelType = assembly.GetType(modelTypeName, throwOnError: false, ignoreCase: false);
        }

        modelType ??= Type.GetType(modelTypeName, throwOnError: false, ignoreCase: false);

        modelType ??= AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(modelTypeName, throwOnError: false, ignoreCase: false))
            .FirstOrDefault(t => t != null);

        if (modelType == null)
        {
            // Try explicitly loading the contracts assembly when needed.
            var contractsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "BaseballManager.Contracts")
                ?? Assembly.Load("BaseballManager.Contracts");

            modelType = contractsAssembly?.GetType(modelTypeName, throwOnError: false, ignoreCase: false);
        }

        if (modelType == null)
        {
            throw new ArgumentException(
                $"Could not resolve model type '{modelTypeName}'. Provide --assembly when the model is outside referenced assemblies.");
        }

        return modelType;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("CSV Import Tool");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/importers/BaseballManager.CsvImportTool/BaseballManager.CsvImportTool.csproj -- \\");
        Console.WriteLine("    --csv data/imports/players.csv \\");
        Console.WriteLine("    --out data/imports/players.json \\");
        Console.WriteLine("    --model BaseballManager.Contracts.ImportDtos.PlayerImportDto \\");
        Console.WriteLine("    --strict true");
        Console.WriteLine();
        Console.WriteLine("Optional:");
        Console.WriteLine("  --assembly <path/to/assembly.dll>");
        Console.WriteLine("  --help");
    }

    private sealed record ImportOptions(
        string CsvPath,
        string OutputPath,
        string ModelTypeName,
        string? AssemblyPath,
        bool Strict);
}
