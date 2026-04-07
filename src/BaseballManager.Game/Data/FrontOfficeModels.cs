namespace BaseballManager.Game.Data;

public sealed record CoachProfileView(
    string Role,
    string Name,
    string Specialty,
    string Voice);

public sealed record ScoutingPlayerCard(
    Guid PlayerId,
    string PlayerName,
    string TeamName,
    string TeamAbbreviation,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    bool IsOnSelectedTeam);

public sealed record CoachScoutingReport(
    string CoachName,
    string CoachRole,
    string PlayerName,
    string Summary,
    string Strengths,
    string Concern,
    string TransferRecommendation);

public sealed record MedicalPlayerStatus(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string Status,
    string Report,
    int Fatigue,
    int DaysUntilAvailable,
    bool IsInjured);

public sealed record TrainingReportView(
    DateTime ReportDate,
    int SeasonYear,
    string Title,
    string FocusLabel,
    string Summary,
    IReadOnlyList<string> CoachNotes);

public sealed record RecentPlayerStatsView(
    int SampleGames,
    int GamesPlayed,
    int InningsPitchedOuts,
    int EarnedRuns,
    int Wins,
    int Losses,
    int AtBats,
    int Hits,
    int Doubles,
    int Triples,
    int HomeRuns,
    int Walks,
    int Strikeouts,
    int PitcherStrikeouts)
{
    public string InningsPitchedDisplay => $"{InningsPitchedOuts / 3}.{InningsPitchedOuts % 3}";

    public string EraDisplay => InningsPitchedOuts == 0
        ? "--.--"
        : ((EarnedRuns * 9d) / (InningsPitchedOuts / 3d)).ToString("0.00");

    public string BattingAverageDisplay => FormatRate(AtBats == 0 ? 0d : Hits / (double)AtBats);

    public string OpsDisplay
    {
        get
        {
            var singles = Math.Max(0, Hits - Doubles - Triples - HomeRuns);
            var totalBases = singles + (Doubles * 2) + (Triples * 3) + (HomeRuns * 4);
            var onBasePercentage = (AtBats + Walks) == 0 ? 0d : (Hits + Walks) / (double)(AtBats + Walks);
            var sluggingPercentage = AtBats == 0 ? 0d : totalBases / (double)AtBats;
            return FormatRate(onBasePercentage + sluggingPercentage);
        }
    }

    private static string FormatRate(double value)
    {
        var formatted = value.ToString("0.000");
        return value is >= 0 and < 1 ? formatted[1..] : formatted;
    }
};
