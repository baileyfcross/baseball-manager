using BaseballManager.Core.Economy;
using System.Text.Json.Serialization;

namespace BaseballManager.Game.Data;

public sealed class FranchiseSaveState
{
    public string? SelectedTeamName { get; set; }

    public int CurrentSeasonYear { get; set; }

    public DateTime CurrentFranchiseDate { get; set; }

    public HashSet<string> CompletedScheduleGameKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, CompletedScheduleGameResult> CompletedScheduleGameResults { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, CompletedGameBoxScoreState> CompletedGameBoxScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? CurrentViewedBoxScoreGameKey { get; set; }

    public DisplaySettingsState DisplaySettings { get; set; } = new();

    public Dictionary<Guid, PlayerHiddenRatingsState> PlayerRatings { get; set; } = new();

    public Dictionary<Guid, PlayerSeasonStatsState> PlayerSeasonStats { get; set; } = new();

    public Dictionary<Guid, PlayerSeasonStatsState> PreviousSeasonStats { get; set; } = new();

    public Dictionary<Guid, List<PlayerRecentGameStatState>> PlayerRecentGameStats { get; set; } = new();

    public Dictionary<Guid, PlayerRecentTotalsState> PlayerRecentTrackingTotals { get; set; } = new();

    public Dictionary<Guid, PlayerHealthState> PlayerHealth { get; set; } = new();

    public Dictionary<Guid, int> PlayerAges { get; set; } = new();

    public Dictionary<Guid, string> PlayerAssignments { get; set; } = new();

    public Dictionary<Guid, string> PlayerRosterAssignments { get; set; } = new();

    public Dictionary<Guid, FranchiseCreatedPlayerState> CreatedPlayers { get; set; } = new();

    public LiveMatchSaveState? CurrentLiveMatch { get; set; }

    public LiveMatchSaveState? QuickMatchLiveMatch { get; set; }

    public CompletedLiveMatchSummaryState? LastCompletedLiveMatch { get; set; }

    public OffseasonSummaryState? LastOffseasonSummary { get; set; }

    public DraftSessionState? ActiveDraft { get; set; }

    public int LastCompletedDraftSeasonYear { get; set; }

    public Dictionary<string, TeamFranchiseState> Teams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class FranchiseCreatedPlayerState
{
    public Guid PlayerId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string PrimaryPosition { get; set; } = string.Empty;

    public string SecondaryPosition { get; set; } = string.Empty;

    public int Age { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public int DraftOverallRating { get; set; }

    public int PotentialRating { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string ScoutSummary { get; set; } = string.Empty;

    public string PotentialSummary { get; set; } = string.Empty;

    public string SourceTeamName { get; set; } = string.Empty;

    public string SourceStatsSummary { get; set; } = string.Empty;

    public string TalentOutcome { get; set; } = string.Empty;

    public int DraftSeasonYear { get; set; }

    public string RosterAssignment { get; set; } = "Pending";

    public bool RequiresRosterDecision { get; set; }

    public int MinorLeagueOptionsRemaining { get; set; } = 3;

    public int LastOptionedSeasonYear { get; set; }
}

public sealed class DraftSessionState
{
    public int TotalRounds { get; set; }

    public bool IsSnakeDraft { get; set; }

    public int CurrentRound { get; set; }

    public int CurrentPickNumber { get; set; }

    public List<string> DraftOrder { get; set; } = new();

    public List<DraftProspectState> AvailableProspects { get; set; } = new();

    public List<DraftPickState> DraftedPicks { get; set; } = new();
}

public sealed class DraftProspectState
{
    public Guid PlayerId { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public string PrimaryPosition { get; set; } = string.Empty;

    public string SecondaryPosition { get; set; } = string.Empty;

    public int Age { get; set; }

    public int OverallRating { get; set; }

    public int PotentialRating { get; set; }

    public string Source { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ScoutSummary { get; set; } = string.Empty;

    public string PotentialSummary { get; set; } = string.Empty;

    public string SourceTeamName { get; set; } = string.Empty;

    public string SourceStatsSummary { get; set; } = string.Empty;

    public string TalentOutcome { get; set; } = string.Empty;
}

public sealed class DraftPickState
{
    public int RoundNumber { get; set; }

    public int PickNumberInRound { get; set; }

    public int OverallPickNumber { get; set; }

    public string TeamName { get; set; } = string.Empty;

    public Guid PlayerId { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public string PrimaryPosition { get; set; } = string.Empty;

    public int OverallRating { get; set; }

    public bool IsUserPick { get; set; }
}

public sealed class OffseasonSummaryState
{
    public int CompletedSeasonYear { get; set; }

    public int NewSeasonYear { get; set; }

    public int ExpiringContracts { get; set; }

    public int ExtensionOffers { get; set; }

    public int ExtensionsCompleted { get; set; }

    public int FreeAgentsSigned { get; set; }

    public int TradesCompleted { get; set; }

    public int LeagueOfferCount { get; set; }

    public string Overview { get; set; } = string.Empty;

    public List<string> LeagueNotes { get; set; } = new();

    public List<OffseasonContractDecisionState> SelectedTeamContractDecisions { get; set; } = new();

    public List<OffseasonTradeDecisionState> SelectedTeamTradeDecisions { get; set; } = new();
}

public sealed class OffseasonContractDecisionState
{
    public string PlayerName { get; set; } = string.Empty;

    public string TeamName { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public decimal PlayerExpectedSalary { get; set; }

    public int PlayerExpectedYears { get; set; }

    public decimal TeamExpectedSalary { get; set; }

    public int TeamExpectedYears { get; set; }

    public decimal AgreedSalary { get; set; }

    public int AgreedYears { get; set; }
}

public sealed class OffseasonTradeDecisionState
{
    public string TeamName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class CompletedLiveMatchSummaryState
{
    public string AwayTeamName { get; set; } = string.Empty;

    public string HomeTeamName { get; set; } = string.Empty;

    public string AwayAbbreviation { get; set; } = string.Empty;

    public string HomeAbbreviation { get; set; } = string.Empty;

    public int AwayRuns { get; set; }

    public int HomeRuns { get; set; }

    public int AwayHits { get; set; }

    public int HomeHits { get; set; }

    public int AwayPitchCount { get; set; }

    public int HomePitchCount { get; set; }

    public string AwayStartingPitcherName { get; set; } = string.Empty;

    public string HomeStartingPitcherName { get; set; } = string.Empty;

    public DateTime ScheduledDate { get; set; }

    public int GameNumber { get; set; } = 1;

    public string Venue { get; set; } = string.Empty;

    public string AwayRecord { get; set; } = string.Empty;

    public string HomeRecord { get; set; } = string.Empty;

    public int FinalInningNumber { get; set; }

    public bool EndedInTopHalf { get; set; }

    public int CompletedPlays { get; set; }

    public bool WasFranchiseMatch { get; set; }

    public string WinningTeamName { get; set; } = string.Empty;

    public string SelectedTeamName { get; set; } = string.Empty;

    public string SelectedTeamResultLabel { get; set; } = string.Empty;

    public string NextGameLabel { get; set; } = string.Empty;

    public DateTime FranchiseDateAfterGame { get; set; }

    public string FinalPlayDescription { get; set; } = string.Empty;

    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CompletedGameBoxScoreState
{
    public string GameKey { get; set; } = string.Empty;

    public CompletedLiveMatchSummaryState Summary { get; set; } = new();

    public List<int> AwayRunsByInning { get; set; } = new();

    public List<int> HomeRunsByInning { get; set; } = new();

    public int AwayErrors { get; set; }

    public int HomeErrors { get; set; }

    public List<CompletedPitchingLineState> AwayPitchingLines { get; set; } = new();

    public List<CompletedPitchingLineState> HomePitchingLines { get; set; } = new();

    public List<CompletedPlayerHighlightState> NotablePlayers { get; set; } = new();
}

public sealed class CompletedPitchingLineState
{
    public string TeamAbbreviation { get; set; } = string.Empty;

    public string PitcherName { get; set; } = string.Empty;

    public bool IsStartingPitcher { get; set; }

    public int PitchCount { get; set; }

    public int InningsPitchedOuts { get; set; }

    public int EarnedRuns { get; set; }

    public int Strikeouts { get; set; }
}

public sealed class CompletedPlayerHighlightState
{
    public string TeamAbbreviation { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public string PrimaryPosition { get; set; } = string.Empty;

    public int RunsScored { get; set; }

    public int Hits { get; set; }

    public int HomeRuns { get; set; }

    public int Walks { get; set; }

    public int Strikeouts { get; set; }

    public string SummaryLine { get; set; } = string.Empty;
}

public sealed class TeamFranchiseState
{
    public List<Guid?> LineupSlots { get; set; } = new();

    public List<Guid?> RotationSlots { get; set; } = new();

    public TeamPracticeFocus PracticeFocus { get; set; } = TeamPracticeFocus.Balanced;

    public Dictionary<string, TeamPracticeFocus> PracticeFocusOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<CoachAssignmentState> CoachingStaff { get; set; } = new();

    public List<AssistantScoutState> AssistantScouts { get; set; } = new();

    public List<ScoutedPlayerState> ScoutedPlayers { get; set; } = new();

    public List<TransferRecordState> TransferHistory { get; set; } = new();

    public List<TrainingReportState> TrainingReports { get; set; } = new();

    public TeamEconomy Economy { get; set; } = new();

    public LiveMatchSaveState? CurrentLiveMatch { get; set; }
}

public sealed class CoachAssignmentState
{
    public string Role { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Specialty { get; set; } = string.Empty;

    public string Voice { get; set; } = string.Empty;
}

public sealed class AssistantScoutState
{
    public int SlotIndex { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Specialty { get; set; } = string.Empty;

    public string Voice { get; set; } = string.Empty;

    public string Country { get; set; } = "U.S. High School";

    public string PositionFocus { get; set; } = "Any";

    public string TraitFocus { get; set; } = "Best Athlete";

    public string AssignmentMode { get; set; } = "Unassigned";

    public string AssignmentTarget { get; set; } = string.Empty;

    public int DaysUntilNextDiscovery { get; set; }
}

public sealed class ScoutedPlayerState
{
    public string ProspectKey { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string PrimaryPosition { get; set; } = string.Empty;

    public int Age { get; set; }

    public string TraitFocus { get; set; } = string.Empty;

    public string FoundByScoutName { get; set; } = string.Empty;

    public int FoundByScoutSlotIndex { get; set; } = -1;

    public string Projection { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string EstimatedBonus { get; set; } = string.Empty;

    public DateTime FoundDate { get; set; } = DateTime.UtcNow;

    public int ScoutingProgress { get; set; } = 20;

    public bool IsOnTargetList { get; set; }

    public int? AssignedScoutSlotIndex { get; set; }

    public string AssignedScoutName { get; set; } = string.Empty;
}

public sealed class TransferRecordState
{
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    public string Description { get; set; } = string.Empty;
}

public sealed class TrainingReportState
{
    public DateTime ReportDate { get; set; }

    public int SeasonYear { get; set; }

    public string Title { get; set; } = string.Empty;

    public string FocusLabel { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> CoachNotes { get; set; } = new();
}

public sealed class CompletedScheduleGameResult
{
    public int AwayRuns { get; set; }

    public int HomeRuns { get; set; }
}

public sealed class DisplaySettingsState
{
    public int ScreenWidth { get; set; } = 1280;

    public int ScreenHeight { get; set; } = 720;

    public int RefreshRate { get; set; } = 60;

    public DisplayWindowMode WindowMode { get; set; } = DisplayWindowMode.Windowed;

    public bool ShowRealTimeClock { get; set; } = true;

    public ScheduleCompactMode ScheduleCompactMode { get; set; } = ScheduleCompactMode.Auto;
}

public sealed class PlayerHiddenRatingsState
{
    public int ContactRating { get; set; }

    public double ContactProgress { get; set; }

    public int PowerRating { get; set; }

    public double PowerProgress { get; set; }

    public int DisciplineRating { get; set; }

    public double DisciplineProgress { get; set; }

    public int SpeedRating { get; set; }

    public double SpeedProgress { get; set; }

    public int FieldingRating { get; set; }

    public double FieldingProgress { get; set; }

    public int ArmRating { get; set; }

    public double ArmProgress { get; set; }

    public int PitchingRating { get; set; }

    public double PitchingProgress { get; set; }

    public int StaminaRating { get; set; }

    public double StaminaProgress { get; set; }

    public int DurabilityRating { get; set; }

    public double DurabilityProgress { get; set; }

    public int AttributeTotal { get; set; }

    public int OverallRating { get; set; }

    [JsonIgnore]
    public double ContactExactRating => GetExactRating(ContactRating, ContactProgress);

    [JsonIgnore]
    public double PowerExactRating => GetExactRating(PowerRating, PowerProgress);

    [JsonIgnore]
    public double DisciplineExactRating => GetExactRating(DisciplineRating, DisciplineProgress);

    [JsonIgnore]
    public double SpeedExactRating => GetExactRating(SpeedRating, SpeedProgress);

    [JsonIgnore]
    public double FieldingExactRating => GetExactRating(FieldingRating, FieldingProgress);

    [JsonIgnore]
    public double ArmExactRating => GetExactRating(ArmRating, ArmProgress);

    [JsonIgnore]
    public double PitchingExactRating => GetExactRating(PitchingRating, PitchingProgress);

    [JsonIgnore]
    public double StaminaExactRating => GetExactRating(StaminaRating, StaminaProgress);

    [JsonIgnore]
    public double DurabilityExactRating => GetExactRating(DurabilityRating, DurabilityProgress);

    [JsonIgnore]
    public int EffectiveContactRating => GetEffectiveRating(ContactExactRating);

    [JsonIgnore]
    public int EffectivePowerRating => GetEffectiveRating(PowerExactRating);

    [JsonIgnore]
    public int EffectiveDisciplineRating => GetEffectiveRating(DisciplineExactRating);

    [JsonIgnore]
    public int EffectiveSpeedRating => GetEffectiveRating(SpeedExactRating);

    [JsonIgnore]
    public int EffectiveFieldingRating => GetEffectiveRating(FieldingExactRating);

    [JsonIgnore]
    public int EffectiveArmRating => GetEffectiveRating(ArmExactRating);

    [JsonIgnore]
    public int EffectivePitchingRating => GetEffectiveRating(PitchingExactRating);

    [JsonIgnore]
    public int EffectiveStaminaRating => GetEffectiveRating(StaminaExactRating);

    [JsonIgnore]
    public int EffectiveDurabilityRating => GetEffectiveRating(DurabilityExactRating);

    public void RecalculateDerivedRatings()
    {
        ContactProgress = NormalizeProgress(ContactRating, ContactProgress);
        PowerProgress = NormalizeProgress(PowerRating, PowerProgress);
        DisciplineProgress = NormalizeProgress(DisciplineRating, DisciplineProgress);
        SpeedProgress = NormalizeProgress(SpeedRating, SpeedProgress);
        FieldingProgress = NormalizeProgress(FieldingRating, FieldingProgress);
        ArmProgress = NormalizeProgress(ArmRating, ArmProgress);
        PitchingProgress = NormalizeProgress(PitchingRating, PitchingProgress);
        StaminaProgress = NormalizeProgress(StaminaRating, StaminaProgress);
        DurabilityProgress = NormalizeProgress(DurabilityRating, DurabilityProgress);

        var exactTotal = ContactExactRating + PowerExactRating + DisciplineExactRating + SpeedExactRating + FieldingExactRating + ArmExactRating + PitchingExactRating + StaminaExactRating + DurabilityExactRating;
        AttributeTotal = Math.Clamp((int)Math.Round(exactTotal, MidpointRounding.AwayFromZero), 9, 891);
        OverallRating = Math.Clamp((int)Math.Round(exactTotal / 9d, MidpointRounding.AwayFromZero), 1, 99);
    }

    private static double GetExactRating(int baseRating, double progress)
    {
        return Math.Clamp(baseRating + NormalizeProgress(baseRating, progress), 1d, 99d);
    }

    private static int GetEffectiveRating(double exactRating)
    {
        return Math.Clamp((int)Math.Round(exactRating, MidpointRounding.AwayFromZero), 1, 99);
    }

    private static double NormalizeProgress(int baseRating, double progress)
    {
        if (baseRating <= 1 || baseRating >= 99)
        {
            return 0d;
        }

        var clamped = Math.Clamp(progress, 0d, 0.5d);
        return Math.Round(clamped * 2d, MidpointRounding.AwayFromZero) / 2d;
    }
}

public sealed class PlayerHealthState
{
    public int Fatigue { get; set; }

    public int PitchCountToday { get; set; }

    public int LastPitchCount { get; set; }

    public int DaysUntilAvailable { get; set; }

    public int InjuryDaysRemaining { get; set; }

    public string InjuryDescription { get; set; } = string.Empty;
}

public sealed class PlayerRecentGameStatState
{
    public DateTime GameDate { get; set; }

    public int GamesPlayed { get; set; }

    public int InningsPitchedOuts { get; set; }

    public int EarnedRuns { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int RunsScored { get; set; }

    public int AtBats { get; set; }

    public int Hits { get; set; }

    public int Doubles { get; set; }

    public int Triples { get; set; }

    public int HomeRuns { get; set; }

    public int Walks { get; set; }

    public int Strikeouts { get; set; }

    public int PitcherStrikeouts { get; set; }
}

public sealed class PlayerRecentTotalsState
{
    public int GamesPlayed { get; set; }

    public int GamesPitched { get; set; }

    public int InningsPitchedOuts { get; set; }

    public int EarnedRuns { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int RunsScored { get; set; }

    public int AtBats { get; set; }

    public int Hits { get; set; }

    public int Doubles { get; set; }

    public int Triples { get; set; }

    public int HomeRuns { get; set; }

    public int Walks { get; set; }

    public int Strikeouts { get; set; }

    public int PitcherStrikeouts { get; set; }
}

public sealed class PlayerSeasonStatsState
{
    public int GamesPlayed { get; set; }

    public int PlateAppearances { get; set; }

    public int RunsScored { get; set; }

    public int AtBats { get; set; }

    public int Hits { get; set; }

    public int Doubles { get; set; }

    public int Triples { get; set; }

    public int HomeRuns { get; set; }

    public int RunsBattedIn { get; set; }

    public int Walks { get; set; }

    public int Strikeouts { get; set; }

    public int GamesPitched { get; set; }

    public int InningsPitchedOuts { get; set; }

    public int HitsAllowed { get; set; }

    public int RunsAllowed { get; set; }

    public int EarnedRuns { get; set; }

    public int WalksAllowed { get; set; }

    public int StrikeoutsPitched { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public string BattingAverageDisplay => FormatRate(AtBats == 0 ? 0d : Hits / (double)AtBats);

    public string OnBasePercentageDisplay => FormatRate(PlateAppearances == 0 ? 0d : (Hits + Walks) / (double)PlateAppearances);

    public string SluggingPercentageDisplay => FormatRate(AtBats == 0 ? 0d : GetTotalBases() / (double)AtBats);

    public string OpsDisplay => FormatRate((PlateAppearances == 0 ? 0d : (Hits + Walks) / (double)PlateAppearances) + (AtBats == 0 ? 0d : GetTotalBases() / (double)AtBats));

    public string EarnedRunAverageDisplay => InningsPitchedOuts == 0 ? "--.--" : ((EarnedRuns * 9d) / (InningsPitchedOuts / 3d)).ToString("0.00");

    public string WinLossDisplay => $"{Wins}-{Losses}";

    private int GetTotalBases()
    {
        var singles = Math.Max(0, Hits - Doubles - Triples - HomeRuns);
        return singles + (Doubles * 2) + (Triples * 3) + (HomeRuns * 4);
    }

    private static string FormatRate(double value)
    {
        var formatted = value.ToString("0.000");
        return value is >= 0 and < 1 ? formatted[1..] : formatted;
    }
}

public enum DisplayWindowMode
{
    Windowed,
    BorderlessWindow,
    Fullscreen
}

public enum ScheduleCompactMode
{
    Auto,
    On,
    Off
}

public enum TeamPracticeFocus
{
    Balanced,
    Hitting,
    Pitching,
    Defense,
    Baserunning,
    Recovery
}
