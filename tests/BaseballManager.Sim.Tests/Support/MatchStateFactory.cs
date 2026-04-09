using BaseballManager.Sim.Engine;

namespace BaseballManager.Sim.Tests.Support;

internal static class MatchStateFactory
{
    public static MatchState CreateDefault()
    {
        var away = MatchTeamState.CreatePlaceholder("Visitors", "VIS");
        var home = MatchTeamState.CreatePlaceholder("Home", "HOME");
        return new MatchState(away, home);
    }

    public static MatchTeamState CreateTeam(
        string name,
        string abbreviation,
        IEnumerable<MatchPlayerSnapshot>? lineup = null,
        MatchPlayerSnapshot? startingPitcher = null,
        IEnumerable<MatchPlayerSnapshot>? benchPlayers = null,
        IEnumerable<MatchPlayerSnapshot>? bullpenPlayers = null,
        MatchPlayerSnapshot? currentPitcher = null,
        IDictionary<Guid, int>? pitchCountsByPitcher = null)
    {
        var resolvedLineup = lineup?.ToList() ?? Enumerable.Range(1, 9)
            .Select(index => CreatePlayer($"{name} Batter {index}", index % 2 == 0 ? "IF" : "OF"))
            .ToList();

        var resolvedPitcher = startingPitcher ?? CreatePlayer($"{name} Pitcher", "SP", pitchingRating: 72, armRating: 60, staminaRating: 70);
        return new MatchTeamState(name, abbreviation, resolvedLineup, resolvedPitcher, benchPlayers, bullpenPlayers, currentPitcher, pitchCountsByPitcher);
    }

    public static MatchPlayerSnapshot CreatePlayer(
        string name,
        string primaryPosition,
        string secondaryPosition = "",
        int contactRating = 55,
        int powerRating = 50,
        int disciplineRating = 50,
        int speedRating = 50,
        int pitchingRating = 24,
        int fieldingRating = 55,
        int armRating = 50,
        int staminaRating = 55,
        int durabilityRating = 55,
        int overallRating = 55)
    {
        return new MatchPlayerSnapshot(
            Guid.NewGuid(),
            name,
            primaryPosition,
            secondaryPosition,
            27,
            contactRating,
            powerRating,
            disciplineRating,
            speedRating,
            pitchingRating,
            fieldingRating,
            armRating,
            staminaRating,
            durabilityRating,
            overallRating);
    }
}
