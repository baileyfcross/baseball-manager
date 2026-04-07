using System.Text.Json.Serialization;

namespace BaseballManager.Game.Data;

public sealed class FranchiseSaveState
{
    public string? SelectedTeamName { get; set; }

    public DateTime CurrentFranchiseDate { get; set; }

    public HashSet<string> CompletedScheduleGameKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, CompletedScheduleGameResult> CompletedScheduleGameResults { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DisplaySettingsState DisplaySettings { get; set; } = new();

    public Dictionary<Guid, PlayerHiddenRatingsState> PlayerRatings { get; set; } = new();

    public Dictionary<Guid, PlayerSeasonStatsState> PlayerSeasonStats { get; set; } = new();

    public Dictionary<Guid, List<PlayerRecentGameStatState>> PlayerRecentGameStats { get; set; } = new();

    public Dictionary<Guid, PlayerRecentTotalsState> PlayerRecentTrackingTotals { get; set; } = new();

    public Dictionary<Guid, PlayerHealthState> PlayerHealth { get; set; } = new();

    public Dictionary<Guid, string> PlayerAssignments { get; set; } = new();

    public LiveMatchSaveState? CurrentLiveMatch { get; set; }

    public LiveMatchSaveState? QuickMatchLiveMatch { get; set; }

    public Dictionary<string, TeamFranchiseState> Teams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TeamFranchiseState
{
    public List<Guid?> LineupSlots { get; set; } = new();

    public List<Guid?> RotationSlots { get; set; } = new();

    public TeamPracticeFocus PracticeFocus { get; set; } = TeamPracticeFocus.Balanced;

    public Dictionary<string, TeamPracticeFocus> PracticeFocusOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<CoachAssignmentState> CoachingStaff { get; set; } = new();

    public List<TransferRecordState> TransferHistory { get; set; } = new();

    public List<TrainingReportState> TrainingReports { get; set; } = new();

    public LiveMatchSaveState? CurrentLiveMatch { get; set; }
}

public sealed class CoachAssignmentState
{
    public string Role { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Specialty { get; set; } = string.Empty;

    public string Voice { get; set; } = string.Empty;
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

public enum TeamPracticeFocus
{
    Balanced,
    Hitting,
    Pitching,
    Defense,
    Baserunning,
    Recovery
}
