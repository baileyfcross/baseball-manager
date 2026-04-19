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
    bool IsOnSelectedTeam,
    decimal TransferFee = 0m);

public sealed record CoachScoutingReport(
    string CoachName,
    string CoachRole,
    string PlayerName,
    string Summary,
    string Strengths,
    string Concern,
    string TransferRecommendation);

public sealed record ScoutAssignmentView(
    int SlotIndex,
    string Role,
    string Name,
    string Specialty,
    string Voice,
    string Country,
    string PositionFocus,
    string TraitFocus,
    string AssignmentMode,
    string AssignmentTarget,
    int DaysUntilNextDiscovery,
    bool IsHeadScout,
    bool IsVacant);

public sealed record AmateurProspectView(
    string ProspectKey,
    string PlayerName,
    string Country,
    string Source,
    string PrimaryPosition,
    int Age,
    string TraitFocus,
    string ScoutName,
    string Projection,
    string Summary,
    string EstimatedBonus,
    int ScoutingProgress,
    bool IsOnTargetList,
    string AssignedScoutName);

public sealed record DraftProspectView(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    string Source,
    string Summary,
    string ScoutSummary,
    string PotentialSummary,
    string SourceTeamName,
    string SourceStatsSummary,
    int ScoutRank,
    int PotentialRank);

public sealed record DraftPickView(
    int RoundNumber,
    int PickNumberInRound,
    int OverallPickNumber,
    string TeamName,
    string PlayerName,
    string PrimaryPosition,
    bool IsUserPick);

public sealed record DraftOrganizationPlayerView(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    string ScoutSummary,
    string PotentialSummary,
    string Source,
    string SourceTeamName,
    string SourceStatsSummary,
    string AssignmentLabel,
    bool RequiresRosterDecision,
    int MinorLeagueOptionsRemaining,
    int ScoutRank,
    int PotentialRank);

public sealed record DraftFortyManPlayerView(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    string StatusLabel,
    bool IsDraftedPlayer,
    int MinorLeagueOptionsRemaining);

public sealed record OrganizationRosterPlayerView(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    string AssignmentLabel,
    bool IsOnFortyMan,
    bool IsDraftedPlayer,
    int MinorLeagueOptionsRemaining,
    bool CanAssignToFortyMan,
    bool CanAssignToAffiliate,
    bool CanRelease,
    int? LineupSlot,
    int? RotationSlot);

public sealed record DraftBoardView(
    bool HasActiveDraft,
    bool IsComplete,
    int TotalRounds,
    int CurrentRound,
    int CurrentPickNumber,
    string CurrentTeamName,
    IReadOnlyList<string> CurrentRoundOrder,
    IReadOnlyList<DraftProspectView> AvailableProspects,
    IReadOnlyList<DraftPickView> RecentPicks);

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

public sealed record TeamStandingView(
    string TeamName,
    string TeamAbbreviation,
    string League,
    string Division,
    int Wins,
    int Losses,
    string Streak)
{
    public int GamesPlayed => Wins + Losses;

    public string Record => $"{Wins}-{Losses}";

    public string WinningPercentage
    {
        get
        {
            var value = GamesPlayed == 0 ? 0d : Wins / (double)GamesPlayed;
            var formatted = value.ToString("0.000");
            return value is >= 0 and < 1 ? formatted[1..] : formatted;
        }
    }
};
