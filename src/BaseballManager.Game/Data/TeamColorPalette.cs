using BaseballManager.Contracts.ImportDtos;
using Microsoft.Xna.Framework;

namespace BaseballManager.Game.Data;

public static class TeamColorPalette
{
    private static readonly string[] FallbackPalette =
    [
        "#2F4F6B",
        "#5B394F",
        "#2E5D5A",
        "#6B4A2F",
        "#4A3F73",
        "#2D5A7A",
        "#685028",
        "#3D5C73",
        "#6A3D4A",
        "#3D6A4E"
    ];

    private static readonly Dictionary<string, string> ColorsByAbbreviation = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NAF"] = "#2F4F6B",
        ["STO"] = "#5B394F",
        ["POC"] = "#2E5D5A",
        ["BAP"] = "#6B4A2F",
        ["AUS"] = "#4A3F73",
        ["TOL"] = "#2D5A7A",
        ["PIB"] = "#685028",
        ["CHS"] = "#3D5C73",
        ["DEC"] = "#6A3D4A",
        ["HOK"] = "#3D6A4E",
        ["INA"] = "#3F4F6A",
        ["MIG"] = "#5E3D67",
        ["CHD"] = "#2F5C63",
        ["LOS"] = "#7A4A31",
        ["MIA"] = "#3D6174",
        ["PHH"] = "#6D374B",
        ["KAW"] = "#3A5D40",
        ["DAR"] = "#74483A",
        ["MI1"] = "#4B5675",
        ["BOF"] = "#5C3B3B",
        ["SES"] = "#2E5666",
        ["PHK"] = "#654F2E",
        ["ATC"] = "#4A3466",
        ["SAF"] = "#346057",
        ["TAP"] = "#7A5A2D",
        ["CLS"] = "#3F4C5E",
        ["DEB"] = "#70403A",
        ["NEH"] = "#5A3E73",
        ["SAO"] = "#355B70",
        ["WAA"] = "#6A2F41"
    };

    public static void ApplyTo(IEnumerable<TeamImportDto> teams)
    {
        foreach (var team in teams)
        {
            if (string.IsNullOrWhiteSpace(team.HexColor))
            {
                team.HexColor = ResolveHexColor(team);
            }
        }
    }

    public static string ResolveHexColor(TeamImportDto team)
    {
        if (ColorsByAbbreviation.TryGetValue(team.Abbreviation, out var hexColor))
        {
            return hexColor;
        }

        var key = string.IsNullOrWhiteSpace(team.Name) ? team.Abbreviation : team.Name;
        var index = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(key)) % FallbackPalette.Length;
        return FallbackPalette[index];
    }

    public static Color GetBackgroundColor(string? hexColor, Color fallback)
    {
        return TryParseHexColor(hexColor, out var parsed)
            ? Blend(parsed, new Color(20, 26, 34), 0.35f)
            : fallback;
    }

    public static Color GetSelectionColor(string? hexColor, bool isHovered)
    {
        if (!TryParseHexColor(hexColor, out var parsed))
        {
            return isHovered ? Color.DarkSlateBlue : Color.SlateGray;
        }

        var baseColor = Blend(parsed, new Color(42, 48, 58), 0.22f);
        return isHovered ? Blend(baseColor, Color.White, 0.10f) : baseColor;
    }

    private static bool TryParseHexColor(string? hexColor, out Color color)
    {
        color = Color.Transparent;

        if (string.IsNullOrWhiteSpace(hexColor))
        {
            return false;
        }

        var normalized = hexColor.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(normalized[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !byte.TryParse(normalized.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !byte.TryParse(normalized.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return false;
        }

        color = new Color(r, g, b);
        return true;
    }

    private static Color Blend(Color source, Color target, float amount)
    {
        return new Color(
            (byte)MathHelper.Lerp(source.R, target.R, amount),
            (byte)MathHelper.Lerp(source.G, target.G, amount),
            (byte)MathHelper.Lerp(source.B, target.B, amount));
    }
}
