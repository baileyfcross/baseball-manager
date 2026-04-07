using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.MainMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Options;

public sealed class OptionsScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly GameRoot _gameRoot;
    private readonly DisplayWindowMode[] _windowModeOptions =
    [
        DisplayWindowMode.Windowed,
        DisplayWindowMode.BorderlessWindow,
        DisplayWindowMode.Fullscreen
    ];
    private readonly (int Width, int Height)[] _resolutionOptions =
    [
        (1024, 576),
        (1280, 720),
        (1600, 900),
        (1920, 1080)
    ];
    private readonly int[] _refreshRateOptions = [30, 60, 75, 120, 144];

    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private int _windowModeIndex;
    private int _resolutionIndex;
    private int _refreshRateIndex;
    private bool _showRealTimeClock;
    private string _statusMessage = "Adjust display settings or manage saves.";

    public OptionsScreen(ScreenManager screenManager, FranchiseSession franchiseSession, GameRoot gameRoot)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _gameRoot = gameRoot;
        LoadSelections();
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        LoadSelections();
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;

        if (_ignoreClicksUntilRelease)
        {
            if (currentMouseState.LeftButton == ButtonState.Released)
            {
                _ignoreClicksUntilRelease = false;
            }

            _previousMouseState = currentMouseState;
            return;
        }

        if (_previousMouseState.LeftButton == ButtonState.Released &&
            currentMouseState.LeftButton == ButtonState.Pressed)
        {
            HandleClick(currentMouseState.Position);
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        uiRenderer.DrawText("Options", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"Current Team: {_franchiseSession.SelectedTeamName}", new Rectangle(168, 82, 380, 22), Color.White, uiRenderer.UiSmallFont);

        var contentLeft = Math.Max(80, (_viewport.X - 680) / 2);
        var mousePosition = Mouse.GetState().Position;

        DrawSelectorRow(
            uiRenderer,
            "Window Mode",
            GetWindowModeLabel(),
            GetWindowModeLeftBounds(contentLeft),
            GetWindowModeValueBounds(contentLeft),
            GetWindowModeRightBounds(contentLeft),
            mousePosition);

        DrawSelectorRow(
            uiRenderer,
            "Screen Size",
            GetResolutionLabel(),
            GetResolutionLeftBounds(contentLeft),
            GetResolutionValueBounds(contentLeft),
            GetResolutionRightBounds(contentLeft),
            mousePosition);

        DrawSelectorRow(
            uiRenderer,
            "Refresh Rate",
            GetRefreshRateLabel(),
            GetRefreshLeftBounds(contentLeft),
            GetRefreshValueBounds(contentLeft),
            GetRefreshRightBounds(contentLeft),
            mousePosition);

        DrawSelectorRow(
            uiRenderer,
            "Real-Time Clock",
            GetClockLabel(),
            GetClockLeftBounds(contentLeft),
            GetClockValueBounds(contentLeft),
            GetClockRightBounds(contentLeft),
            mousePosition);

        var hasTeamSave = _franchiseSession.HasFranchiseSaveData;
        var hasQuickMatchSave = _franchiseSession.HasQuickMatchSaveData;
        var hasAnySaveData = _franchiseSession.HasAnySaveData;
        var deleteCurrentTeamBounds = GetDeleteCurrentTeamBounds(contentLeft);
        var deleteQuickMatchBounds = GetDeleteQuickMatchBounds(contentLeft);
        var deleteAllBounds = GetDeleteAllBounds(contentLeft);
        var backBounds = GetBackButtonBounds();

        uiRenderer.DrawButton(
            "Delete Current Team Save",
            deleteCurrentTeamBounds,
            hasTeamSave
                ? (deleteCurrentTeamBounds.Contains(mousePosition) ? Color.Red : Color.DarkRed)
                : Color.DimGray,
            Color.White);

        uiRenderer.DrawButton(
            "Delete Quick Match Save",
            deleteQuickMatchBounds,
            hasQuickMatchSave
                ? (deleteQuickMatchBounds.Contains(mousePosition) ? Color.OrangeRed : Color.IndianRed)
                : Color.DimGray,
            Color.White);

        uiRenderer.DrawButton(
            "Delete All Save Data",
            deleteAllBounds,
            hasAnySaveData
                ? (deleteAllBounds.Contains(mousePosition) ? Color.Red : Color.Maroon)
                : Color.DimGray,
            Color.White);

        var statusBounds = new Rectangle(contentLeft, _viewport.Y - 112, 540, 38);
        uiRenderer.DrawButton(string.Empty, statusBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(statusBounds.X + 10, statusBounds.Y + 5, statusBounds.Width - 20, statusBounds.Height - 10), Color.White, uiRenderer.ScoreboardFont, 2);
        uiRenderer.DrawButton("Back", backBounds, backBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private void HandleClick(Point mousePosition)
    {
        var contentLeft = Math.Max(80, (_viewport.X - 680) / 2);

        if (GetBackButtonBounds().Contains(mousePosition))
        {
            _screenManager.TransitionTo(nameof(MainMenuScreen));
            return;
        }

        if (GetWindowModeLeftBounds(contentLeft).Contains(mousePosition))
        {
            ChangeWindowMode(-1);
            return;
        }

        if (GetWindowModeRightBounds(contentLeft).Contains(mousePosition))
        {
            ChangeWindowMode(1);
            return;
        }

        if (GetResolutionLeftBounds(contentLeft).Contains(mousePosition))
        {
            ChangeResolution(-1);
            return;
        }

        if (GetResolutionRightBounds(contentLeft).Contains(mousePosition))
        {
            ChangeResolution(1);
            return;
        }

        if (GetRefreshLeftBounds(contentLeft).Contains(mousePosition))
        {
            ChangeRefreshRate(-1);
            return;
        }

        if (GetRefreshRightBounds(contentLeft).Contains(mousePosition))
        {
            ChangeRefreshRate(1);
            return;
        }

        if (GetClockLeftBounds(contentLeft).Contains(mousePosition) ||
            GetClockRightBounds(contentLeft).Contains(mousePosition) ||
            GetClockValueBounds(contentLeft).Contains(mousePosition))
        {
            ToggleClockVisibility();
            return;
        }

        if (GetDeleteCurrentTeamBounds(contentLeft).Contains(mousePosition))
        {
            var deleted = _franchiseSession.DeleteCurrentTeamSave();
            _statusMessage = deleted
                ? "Deleted the current team save."
                : "No active franchise team save was found.";
            return;
        }

        if (GetDeleteQuickMatchBounds(contentLeft).Contains(mousePosition))
        {
            var deleted = _franchiseSession.DeleteQuickMatchSave();
            _statusMessage = deleted
                ? "Deleted the quick match save."
                : "No quick match save was found.";
            return;
        }

        if (GetDeleteAllBounds(contentLeft).Contains(mousePosition))
        {
            var deleted = _franchiseSession.DeleteAllSaveData();
            _statusMessage = deleted
                ? "All save data deleted."
                : "No save data was found to delete.";
        }
    }

    private void ChangeWindowMode(int direction)
    {
        _windowModeIndex = WrapIndex(_windowModeIndex + direction, _windowModeOptions.Length);
        ApplyDisplaySettings();
    }

    private void ChangeResolution(int direction)
    {
        _resolutionIndex = WrapIndex(_resolutionIndex + direction, _resolutionOptions.Length);
        ApplyDisplaySettings();
    }

    private void ChangeRefreshRate(int direction)
    {
        _refreshRateIndex = WrapIndex(_refreshRateIndex + direction, _refreshRateOptions.Length);
        ApplyDisplaySettings();
    }

    private void ToggleClockVisibility()
    {
        _showRealTimeClock = !_showRealTimeClock;
        _franchiseSession.UpdateClockVisibility(_showRealTimeClock);
        _statusMessage = $"Real-time clock {(_showRealTimeClock ? "enabled" : "disabled")}.";
    }

    private void ApplyDisplaySettings()
    {
        var windowMode = _windowModeOptions[_windowModeIndex];
        var resolution = _resolutionOptions[_resolutionIndex];
        var refreshRate = _refreshRateOptions[_refreshRateIndex];

        _franchiseSession.UpdateDisplaySettings(resolution.Width, resolution.Height, refreshRate, windowMode);
        _gameRoot.ApplyDisplaySettings(resolution.Width, resolution.Height, refreshRate, windowMode);
        _statusMessage = $"Display updated: {GetWindowModeLabel()} / {resolution.Width}x{resolution.Height} / {refreshRate} Hz.";
    }

    private void LoadSelections()
    {
        var displaySettings = _franchiseSession.GetDisplaySettings();
        _windowModeIndex = FindWindowModeIndex(displaySettings.WindowMode);
        _resolutionIndex = FindResolutionIndex(displaySettings.ScreenWidth, displaySettings.ScreenHeight);
        _refreshRateIndex = FindRefreshRateIndex(displaySettings.RefreshRate);
        _showRealTimeClock = displaySettings.ShowRealTimeClock;
    }

    private int FindWindowModeIndex(DisplayWindowMode windowMode)
    {
        for (var i = 0; i < _windowModeOptions.Length; i++)
        {
            if (_windowModeOptions[i] == windowMode)
            {
                return i;
            }
        }

        return 0;
    }

    private int FindResolutionIndex(int screenWidth, int screenHeight)
    {
        for (var i = 0; i < _resolutionOptions.Length; i++)
        {
            if (_resolutionOptions[i].Width == screenWidth && _resolutionOptions[i].Height == screenHeight)
            {
                return i;
            }
        }

        return 1;
    }

    private int FindRefreshRateIndex(int refreshRate)
    {
        for (var i = 0; i < _refreshRateOptions.Length; i++)
        {
            if (_refreshRateOptions[i] == refreshRate)
            {
                return i;
            }
        }

        return 1;
    }

    private void DrawSelectorRow(
        UiRenderer uiRenderer,
        string label,
        string value,
        Rectangle leftBounds,
        Rectangle valueBounds,
        Rectangle rightBounds,
        Point mousePosition)
    {
        uiRenderer.DrawText(label, new Vector2(valueBounds.X - 190, valueBounds.Y + 8), Color.White);
        uiRenderer.DrawButton("<", leftBounds, leftBounds.Contains(mousePosition) ? Color.SteelBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(value, valueBounds, valueBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.DarkSlateGray, Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawButton(">", rightBounds, rightBounds.Contains(mousePosition) ? Color.SteelBlue : Color.SlateGray, Color.White);
    }

    private static int WrapIndex(int value, int count)
    {
        return (value % count + count) % count;
    }

    private string GetWindowModeLabel()
    {
        return _windowModeOptions[_windowModeIndex] switch
        {
            DisplayWindowMode.BorderlessWindow => "Borderless Window",
            DisplayWindowMode.Fullscreen => "Fullscreen",
            _ => "Windowed"
        };
    }

    private string GetResolutionLabel()
    {
        var resolution = _resolutionOptions[_resolutionIndex];
        return $"{resolution.Width} x {resolution.Height}";
    }

    private string GetRefreshRateLabel()
    {
        return $"{_refreshRateOptions[_refreshRateIndex]} Hz";
    }

    private string GetClockLabel()
    {
        return _showRealTimeClock ? "On" : "Off";
    }

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private static Rectangle GetWindowModeLeftBounds(int contentLeft) => new(contentLeft + 180, 160, 44, 42);

    private static Rectangle GetWindowModeValueBounds(int contentLeft) => new(contentLeft + 236, 160, 240, 42);

    private static Rectangle GetWindowModeRightBounds(int contentLeft) => new(contentLeft + 488, 160, 44, 42);

    private static Rectangle GetResolutionLeftBounds(int contentLeft) => new(contentLeft + 180, 230, 44, 42);

    private static Rectangle GetResolutionValueBounds(int contentLeft) => new(contentLeft + 236, 230, 240, 42);

    private static Rectangle GetResolutionRightBounds(int contentLeft) => new(contentLeft + 488, 230, 44, 42);

    private static Rectangle GetRefreshLeftBounds(int contentLeft) => new(contentLeft + 180, 300, 44, 42);

    private static Rectangle GetRefreshValueBounds(int contentLeft) => new(contentLeft + 236, 300, 240, 42);

    private static Rectangle GetRefreshRightBounds(int contentLeft) => new(contentLeft + 488, 300, 44, 42);

    private static Rectangle GetClockLeftBounds(int contentLeft) => new(contentLeft + 180, 370, 44, 42);

    private static Rectangle GetClockValueBounds(int contentLeft) => new(contentLeft + 236, 370, 240, 42);

    private static Rectangle GetClockRightBounds(int contentLeft) => new(contentLeft + 488, 370, 44, 42);

    private static Rectangle GetDeleteCurrentTeamBounds(int contentLeft) => new(contentLeft + 180, 430, 260, 44);

    private static Rectangle GetDeleteQuickMatchBounds(int contentLeft) => new(contentLeft + 180, 486, 260, 44);

    private static Rectangle GetDeleteAllBounds(int contentLeft) => new(contentLeft + 180, 542, 260, 44);
}
