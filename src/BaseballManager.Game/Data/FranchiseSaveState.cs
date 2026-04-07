namespace BaseballManager.Game.Data;

public sealed class FranchiseSaveState
{
    public string? SelectedTeamName { get; set; }

    public DisplaySettingsState DisplaySettings { get; set; } = new();

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

public enum DisplayWindowMode
{
    Windowed,
    BorderlessWindow,
    Fullscreen
}
