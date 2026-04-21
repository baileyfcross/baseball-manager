namespace BaseballManager.Game.Data;

public enum PlayerContextAction
{
    OpenProfile,
    OpenRosterAssignments,
    AssignToFortyMan,
    AssignToTripleA,
    AssignToDoubleA,
    AssignToSingleA,
    ReturnToAutomaticAffiliate,
    RemoveFromFortyMan,
    ReleasePlayer,
    TradeForPlayer,
    ScoutPlayer
}

public enum MinorLeagueAffiliateLevel
{
    TripleA,
    DoubleA,
    SingleA
}

public sealed record PlayerContextActionView(
    PlayerContextAction Action,
    string Label,
    bool IsEnabled = true);

public sealed record PlayerProfileView(
    string Title,
    string Subtitle,
    IReadOnlyList<string> DetailLines,
    IReadOnlyList<string> SummaryLines);

public sealed record AsyncOperationProgressView(
    string Title,
    string Status,
    double ProgressValue)
{
    public static AsyncOperationProgressView Idle { get; } = new(string.Empty, string.Empty, 0d);

    public double ClampedProgressValue => Math.Clamp(ProgressValue, 0d, 1d);

    public int PercentComplete => (int)Math.Round(ClampedProgressValue * 100d, MidpointRounding.AwayFromZero);
}

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

public sealed record LineupPositionAssignmentView(
    string Position,
    Guid? PlayerId,
    string PlayerName,
    int? LineupSlot);

public sealed record LineupValidationView(
    bool IsValid,
    string Summary,
    IReadOnlyList<string> MissingPositions,
    IReadOnlyList<LineupPositionAssignmentView> PositionAssignments);

public sealed record OrganizationRosterPlayerView(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    string AssignmentLabel,
    string TeamStatusLabel,
    MinorLeagueAffiliateLevel? AffiliateLevel,
    bool IsMinorLeagueAssignmentLocked,
    bool IsOnFortyMan,
    bool IsOnFirstTeam,
    bool IsDraftedPlayer,
    int MinorLeagueOptionsRemaining,
    bool CanAssignToFortyMan,
    bool CanReturnToAutomaticAffiliate,
    bool CanRelease,
    int? LineupSlot,
    int? RotationSlot);

public enum OrganizationRosterCompositionMode
{
    FirstTeam,
    Depth,
    Affiliate
}

public sealed record OrganizationRosterCompositionBucketView(
    string Label,
    int Count,
    int? TargetCount,
    IReadOnlyList<OrganizationRosterCompositionBucketView>? Details = null);

public sealed record OrganizationRosterCompositionView(
    string Title,
    string Summary,
    int TotalCount,
    int? TargetCount,
    IReadOnlyList<OrganizationRosterCompositionBucketView> Buckets);

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

public sealed record DraftClassHistoryPlayerView(
    Guid PlayerId,
    string PlayerName,
    string PrimaryPosition,
    string SecondaryPosition,
    int Age,
    int OverallRating,
    int PotentialRating,
    string Source,
    string AssignmentLabel,
    bool IsOnFortyMan);

public sealed record DraftClassHistoryView(
    int SeasonYear,
    string Title,
    string Summary,
    int TotalPlayers,
    int FortyManCount,
    int AffiliateCount,
    int OrganizationCount,
    IReadOnlyList<DraftClassHistoryPlayerView> Players);

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
