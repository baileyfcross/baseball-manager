using System.Globalization;
using System.Reflection;
using BaseballManager.Contracts.ImportDtos;

namespace BaseballManager.CsvImportTool;

internal static class RowMapper
{
    public static object MapRow(Type modelType, string[] headers, string[] row, bool strict)
    {
        var instance = Activator.CreateInstance(modelType)
            ?? throw new InvalidOperationException($"Could not create instance of {modelType.FullName}.");

        var normalizedHeaders = headers
            .Select((header, index) => new { Key = Normalize(header), Index = index })
            .ToDictionary(x => x.Key, x => x.Index, StringComparer.OrdinalIgnoreCase);

        foreach (var property in modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
            {
                continue;
            }

            var possibleNames = GetPossibleHeaderNames(property)
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int headerIndex = -1;
            foreach (var name in possibleNames)
            {
                if (normalizedHeaders.TryGetValue(name, out var idx))
                {
                    headerIndex = idx;
                    break;
                }
            }

            if (headerIndex < 0)
            {
                if (strict)
                {
                    throw new InvalidOperationException(
                        $"No CSV header found for property '{property.Name}' on model '{modelType.Name}'.");
                }

                continue;
            }

            var rawValue = headerIndex < row.Length ? row[headerIndex] : string.Empty;
            var converted = ConvertValue(rawValue, property.PropertyType);
            property.SetValue(instance, converted);
        }

        return instance;
    }

    private static IEnumerable<string> GetPossibleHeaderNames(PropertyInfo property)
    {
        yield return property.Name;

        var attributes = property.GetCustomAttributes<CsvHeaderAttribute>();
        foreach (var attribute in attributes)
        {
            yield return attribute.HeaderName;
        }
    }

    private static object? ConvertValue(string rawValue, Type targetType)
    {
        var trimmed = rawValue.Trim();
        var nullableType = Nullable.GetUnderlyingType(targetType);

        if (nullableType != null)
        {
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            targetType = nullableType;
        }

        if (targetType == typeof(string))
        {
            return trimmed;
        }

        if (targetType == typeof(int))
        {
            return int.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(long))
        {
            return long.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(decimal))
        {
            return decimal.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(float))
        {
            return float.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(trimmed);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(trimmed);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, trimmed, ignoreCase: true);
        }

        throw new NotSupportedException(
            $"Property type '{targetType.FullName}' is not supported by the CSV importer.");
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();
    }
}
