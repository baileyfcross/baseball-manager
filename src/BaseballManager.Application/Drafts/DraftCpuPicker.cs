using BaseballManager.Core.Drafts;

namespace BaseballManager.Application.Drafts;

public sealed class DraftCpuPicker
{
    public DraftProspect SelectProspect(DraftState state, DraftCpuTeamContext teamContext)
    {
        if (state.AvailableProspects.Count == 0)
        {
            throw new InvalidOperationException("No draft prospects are available.");
        }

        return state.AvailableProspects
            .OrderByDescending(prospect => ScoreProspect(prospect, teamContext))
            .ThenByDescending(prospect => prospect.OverallRating)
            .ThenByDescending(prospect => prospect.PotentialRating)
            .ThenBy(prospect => prospect.Age)
            .ThenBy(prospect => prospect.PlayerName)
            .First();
    }

    private static int ScoreProspect(DraftProspect prospect, DraftCpuTeamContext teamContext)
    {
        var baseScore = (prospect.OverallRating * 100) + (prospect.PotentialRating * 4) - (prospect.Age * 3);
        var positionNeedScore = GetPositionNeedScore(teamContext.RosterPositions, prospect.PrimaryPosition);
        return baseScore + positionNeedScore;
    }

    private static int GetPositionNeedScore(IReadOnlyList<string> rosterPositions, string position)
    {
        var normalizedPosition = NormalizePosition(position);
        var countAtPosition = rosterPositions.Count(existing => string.Equals(NormalizePosition(existing), normalizedPosition, StringComparison.OrdinalIgnoreCase));

        return normalizedPosition switch
        {
            "SP" => countAtPosition switch { 0 => 36, 1 => 26, 2 => 18, 3 => 10, 4 => 4, _ => 0 },
            "RP" => countAtPosition switch { 0 => 30, 1 => 22, 2 => 16, 3 => 10, 4 => 6, _ => 0 },
            _ => countAtPosition switch { 0 => 28, 1 => 18, 2 => 8, _ => 0 }
        };
    }

    private static string NormalizePosition(string position)
    {
        var normalizedPosition = (position ?? string.Empty).Trim().ToUpperInvariant();
        return normalizedPosition switch
        {
            "LF" or "CF" or "RF" => "OF",
            _ => normalizedPosition
        };
    }
}
