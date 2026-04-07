namespace BaseballManager.Game.Data;

public sealed class FranchiseSaveState
{
    public string? SelectedTeamName { get; set; }

    public DisplaySettingsState DisplaySettings { get; set; } = new();

    public Dictionary<Guid, PlayerHiddenRatingsState> PlayerRatings { get; set; } = new();

    public Dictionary<Guid, PlayerSeasonStatsState> PlayerSeasonStats { get; set; } = new();

    public LiveMatchSaveState? CurrentLiveMatch { get; set; }

    public LiveMatchSaveState? QuickMatchLiveMatch { get; set; }

    public Dictionary<string, TeamFranchiseState> Teams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TeamFranchiseState
{
    public List<Guid?> LineupSlots { get; set; } = new();

    public List<Guid?> RotationSlots { get; set; } = new();

    public LiveMatchSaveState? CurrentLiveMatch { get; set; }
}

public sealed class DisplaySettingsState
{
    public int ScreenWidth { get; set; } = 1280;

    public int ScreenHeight { get; set; } = 720;

    public int RefreshRate { get; set; } = 60;

    public DisplayWindowMode WindowMode { get; set; } = DisplayWindowMode.Windowed;
}

public sealed class PlayerHiddenRatingsState
{
    public int ContactRating { get; set; }

    public int PowerRating { get; set; }

    public int DisciplineRating { get; set; }

    public int SpeedRating { get; set; }

    public int FieldingRating { get; set; }

    public int ArmRating { get; set; }

    public int PitchingRating { get; set; }

    public int DurabilityRating { get; set; }

    public int AttributeTotal { get; set; }

    public int OverallRating { get; set; }

    public void RecalculateDerivedRatings()
    {
        AttributeTotal = ContactRating + PowerRating + DisciplineRating + SpeedRating + FieldingRating + ArmRating + PitchingRating + DurabilityRating;
        OverallRating = Math.Clamp((int)Math.Round(AttributeTotal / 8d), 1, 99);
    }
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
