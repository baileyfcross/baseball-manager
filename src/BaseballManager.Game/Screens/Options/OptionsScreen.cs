using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Game.UI.Layout;
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
    private readonly ScheduleCompactMode[] _scheduleCompactModeOptions =
    [
        ScheduleCompactMode.Auto,
        ScheduleCompactMode.On,
        ScheduleCompactMode.Off
    ];

    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private int _windowModeIndex;
    private int _resolutionIndex;
    private int _refreshRateIndex;
    private bool _showRealTimeClock;
    private int _scheduleCompactModeIndex;
    private string _statusMessage = "Adjust display settings or manage saves.";

    private const int LayoutHorizontalPadding = 24;
    private const int MaxContentWidth = 760;
    private const int SelectorLabelWidth = 210;
    private const int SelectorArrowWidth = 44;
    private const int SelectorGap = 12;
    private const int SelectorValueMinWidth = 180;
    private const int SelectorValueMaxWidth = 320;
    private const int SelectorRowHeight = 42;
    private const int SelectorRowSpacing = 70;
    private const int DeleteButtonWidth = 300;
    private const int DeleteButtonHeight = 44;
    private const int StatusHeight = 38;
    private const int LayoutBottomPadding = 16;
    private const int BaseWindowModeY = 160;
    private const int BaseResolutionY = 230;
    private const int BaseRefreshY = 300;
    private const int BaseClockY = 370;
    private const int BaseDeleteCurrentY = 500;
    private const int BaseDeleteQuickMatchY = 556;
    private const int BaseDeleteAllY = 612;
    private const int BaseStatusY = 668;

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
        var layout = GetLayoutMetrics();
        var mousePosition = Mouse.GetState().Position;

        uiRenderer.DrawTextInBounds("Options", new Rectangle(layout.ContentLeft, 42, layout.ContentWidth, 30), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"Current Team: {_franchiseSession.SelectedTeamName}", new Rectangle(layout.ContentLeft, 82, layout.ContentWidth, 22), Color.White, uiRenderer.UiSmallFont);

        var windowLabelBounds = GetSelectorLabelBounds(0, layout);
        var windowLeftBounds = GetSelectorLeftBounds(0, layout);
        var windowValueBounds = GetSelectorValueBounds(0, layout);
        var windowRightBounds = GetSelectorRightBounds(0, layout);
        var resolutionLabelBounds = GetSelectorLabelBounds(1, layout);
        var resolutionLeftBounds = GetSelectorLeftBounds(1, layout);
        var resolutionValueBounds = GetSelectorValueBounds(1, layout);
        var resolutionRightBounds = GetSelectorRightBounds(1, layout);
        var refreshLabelBounds = GetSelectorLabelBounds(2, layout);
        var refreshLeftBounds = GetSelectorLeftBounds(2, layout);
        var refreshValueBounds = GetSelectorValueBounds(2, layout);
        var refreshRightBounds = GetSelectorRightBounds(2, layout);
        var clockLabelBounds = GetSelectorLabelBounds(3, layout);
        var clockLeftBounds = GetSelectorLeftBounds(3, layout);
        var clockValueBounds = GetSelectorValueBounds(3, layout);
        var clockRightBounds = GetSelectorRightBounds(3, layout);
        var compactLabelBounds = GetSelectorLabelBounds(4, layout);
        var compactLeftBounds = GetSelectorLeftBounds(4, layout);
        var compactValueBounds = GetSelectorValueBounds(4, layout);
        var compactRightBounds = GetSelectorRightBounds(4, layout);

        DrawSelectorRow(
            uiRenderer,
            "Window Mode",
            GetWindowModeLabel(),
            windowLabelBounds,
            windowLeftBounds,
            windowValueBounds,
            windowRightBounds,
            mousePosition);

        DrawSelectorRow(
            uiRenderer,
            "Screen Size",
            GetResolutionLabel(),
            resolutionLabelBounds,
            resolutionLeftBounds,
            resolutionValueBounds,
            resolutionRightBounds,
            mousePosition);

        DrawSelectorRow(
            uiRenderer,
            "Refresh Rate",
            GetRefreshRateLabel(),
            refreshLabelBounds,
            refreshLeftBounds,
            refreshValueBounds,
            refreshRightBounds,
            mousePosition);

        DrawSelectorRow(
            uiRenderer,
            "Real-Time Clock",
            GetClockLabel(),
            clockLabelBounds,
            clockLeftBounds,
            clockValueBounds,
            clockRightBounds,
            mousePosition);

        DrawSelectorRow(
            uiRenderer,
            "Schedule Compact Mode",
            GetScheduleCompactModeLabel(),
            compactLabelBounds,
            compactLeftBounds,
            compactValueBounds,
            compactRightBounds,
            mousePosition);

        var hasTeamSave = _franchiseSession.HasFranchiseSaveData;
        var hasQuickMatchSave = _franchiseSession.HasQuickMatchSaveData;
        var hasAnySaveData = _franchiseSession.HasAnySaveData;
        var deleteCurrentTeamBounds = GetDeleteCurrentTeamBounds(layout);
        var deleteQuickMatchBounds = GetDeleteQuickMatchBounds(layout);
        var deleteAllBounds = GetDeleteAllBounds(layout);
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

        var statusBounds = GetStatusBounds(layout);
        uiRenderer.DrawButton(string.Empty, statusBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(statusBounds.X + 10, statusBounds.Y + 5, statusBounds.Width - 20, statusBounds.Height - 10), Color.White, uiRenderer.UiSmallFont, 2);
        uiRenderer.DrawButton("Back", backBounds, backBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private void HandleClick(Point mousePosition)
    {
        var layout = GetLayoutMetrics();
        var windowLeftBounds = GetSelectorLeftBounds(0, layout);
        var windowValueBounds = GetSelectorValueBounds(0, layout);
        var windowRightBounds = GetSelectorRightBounds(0, layout);
        var resolutionLeftBounds = GetSelectorLeftBounds(1, layout);
        var resolutionRightBounds = GetSelectorRightBounds(1, layout);
        var refreshLeftBounds = GetSelectorLeftBounds(2, layout);
        var refreshRightBounds = GetSelectorRightBounds(2, layout);
        var clockLeftBounds = GetSelectorLeftBounds(3, layout);
        var clockValueBounds = GetSelectorValueBounds(3, layout);
        var clockRightBounds = GetSelectorRightBounds(3, layout);
        var compactLeftBounds = GetSelectorLeftBounds(4, layout);
        var compactValueBounds = GetSelectorValueBounds(4, layout);
        var compactRightBounds = GetSelectorRightBounds(4, layout);
        var deleteCurrentTeamBounds = GetDeleteCurrentTeamBounds(layout);
        var deleteQuickMatchBounds = GetDeleteQuickMatchBounds(layout);
        var deleteAllBounds = GetDeleteAllBounds(layout);

        if (GetBackButtonBounds().Contains(mousePosition))
        {
            _screenManager.TransitionTo(nameof(MainMenuScreen));
            return;
        }

        if (windowLeftBounds.Contains(mousePosition))
        {
            ChangeWindowMode(-1);
            return;
        }

        if (windowRightBounds.Contains(mousePosition) || windowValueBounds.Contains(mousePosition))
        {
            ChangeWindowMode(1);
            return;
        }

        if (resolutionLeftBounds.Contains(mousePosition))
        {
            ChangeResolution(-1);
            return;
        }

        if (resolutionRightBounds.Contains(mousePosition))
        {
            ChangeResolution(1);
            return;
        }

        if (refreshLeftBounds.Contains(mousePosition))
        {
            ChangeRefreshRate(-1);
            return;
        }

        if (refreshRightBounds.Contains(mousePosition))
        {
            ChangeRefreshRate(1);
            return;
        }

        if (clockLeftBounds.Contains(mousePosition) ||
            clockRightBounds.Contains(mousePosition) ||
            clockValueBounds.Contains(mousePosition))
        {
            ToggleClockVisibility();
            return;
        }

        if (compactLeftBounds.Contains(mousePosition))
        {
            ChangeScheduleCompactMode(-1);
            return;
        }

        if (compactRightBounds.Contains(mousePosition) ||
            compactValueBounds.Contains(mousePosition))
        {
            ChangeScheduleCompactMode(1);
            return;
        }

        if (deleteCurrentTeamBounds.Contains(mousePosition))
        {
            var deleted = _franchiseSession.DeleteCurrentTeamSave();
            _statusMessage = deleted
                ? "Deleted the current team save."
                : "No active franchise team save was found.";
            return;
        }

        if (deleteQuickMatchBounds.Contains(mousePosition))
        {
            var deleted = _franchiseSession.DeleteQuickMatchSave();
            _statusMessage = deleted
                ? "Deleted the quick match save."
                : "No quick match save was found.";
            return;
        }

        if (deleteAllBounds.Contains(mousePosition))
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

    private void ChangeScheduleCompactMode(int direction)
    {
        _scheduleCompactModeIndex = WrapIndex(_scheduleCompactModeIndex + direction, _scheduleCompactModeOptions.Length);
        var compactMode = _scheduleCompactModeOptions[_scheduleCompactModeIndex];
        _franchiseSession.UpdateScheduleCompactMode(compactMode);
        _statusMessage = $"Schedule compact mode set to {GetScheduleCompactModeLabel()}.";
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
        _scheduleCompactModeIndex = FindScheduleCompactModeIndex(displaySettings.ScheduleCompactMode);
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

    private int FindScheduleCompactModeIndex(ScheduleCompactMode compactMode)
    {
        for (var i = 0; i < _scheduleCompactModeOptions.Length; i++)
        {
            if (_scheduleCompactModeOptions[i] == compactMode)
            {
                return i;
            }
        }

        return 0;
    }

    private void DrawSelectorRow(
        UiRenderer uiRenderer,
        string label,
        string value,
        Rectangle labelBounds,
        Rectangle leftBounds,
        Rectangle valueBounds,
        Rectangle rightBounds,
        Point mousePosition)
    {
        uiRenderer.DrawTextInBounds(label, labelBounds, Color.White, uiRenderer.UiSmallFont);
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

    private string GetScheduleCompactModeLabel()
    {
        return _scheduleCompactModeOptions[_scheduleCompactModeIndex] switch
        {
            ScheduleCompactMode.On => "On",
            ScheduleCompactMode.Off => "Off",
            _ => "Auto"
        };
    }

    private LayoutMetrics GetLayoutMetrics()
    {
        var availableWidth = Math.Max(420, _viewport.X - (LayoutHorizontalPadding * 2));
        var contentWidth = Math.Min(MaxContentWidth, availableWidth);
        var contentLeft = (_viewport.X - contentWidth) / 2;
        var verticalOffset = GetLayoutVerticalOffset();

        var valueWidthBudget = contentWidth - SelectorLabelWidth - (SelectorArrowWidth * 2) - (SelectorGap * 3);
        var valueWidth = Math.Clamp(valueWidthBudget, SelectorValueMinWidth, SelectorValueMaxWidth);
        var rowClusterWidth = SelectorLabelWidth + (SelectorGap * 3) + (SelectorArrowWidth * 2) + valueWidth;
        var clusterLeft = (_viewport.X - rowClusterWidth) / 2;

        return new LayoutMetrics(contentLeft, contentWidth, verticalOffset, clusterLeft, valueWidth);
    }

    private int GetLayoutVerticalOffset()
    {
        // Center the content block vertically in the space below the header.
        // At the reference height (720 px) this keeps items near their original
        // positions; on taller displays it pushes everything down so there is
        // equal whitespace above and below the group.
        const int contentBlockHeight = BaseStatusY + StatusHeight - BaseWindowModeY; // ~546 px at reference
        var headerEnd = 120;
        var available = _viewport.Y - LayoutBottomPadding - headerEnd;
        var topMargin = Math.Max(24, (available - contentBlockHeight) / 2);
        var contentBlockY = headerEnd + topMargin;
        return contentBlockY - BaseWindowModeY;
    }

    private static int GetSelectorRowY(int rowIndex, LayoutMetrics layout)
    {
        return BaseWindowModeY + (rowIndex * SelectorRowSpacing) + layout.VerticalOffset;
    }

    private static Rectangle GetSelectorLabelBounds(int rowIndex, LayoutMetrics layout)
    {
        return new Rectangle(layout.ClusterLeft, GetSelectorRowY(rowIndex, layout), SelectorLabelWidth, SelectorRowHeight);
    }

    private static Rectangle GetSelectorLeftBounds(int rowIndex, LayoutMetrics layout)
    {
        var labelBounds = GetSelectorLabelBounds(rowIndex, layout);
        return new Rectangle(labelBounds.Right + SelectorGap, labelBounds.Y, SelectorArrowWidth, SelectorRowHeight);
    }

    private static Rectangle GetSelectorValueBounds(int rowIndex, LayoutMetrics layout)
    {
        var leftBounds = GetSelectorLeftBounds(rowIndex, layout);
        return new Rectangle(leftBounds.Right + SelectorGap, leftBounds.Y, layout.SelectorValueWidth, SelectorRowHeight);
    }

    private static Rectangle GetSelectorRightBounds(int rowIndex, LayoutMetrics layout)
    {
        var valueBounds = GetSelectorValueBounds(rowIndex, layout);
        return new Rectangle(valueBounds.Right + SelectorGap, valueBounds.Y, SelectorArrowWidth, SelectorRowHeight);
    }

    private static Rectangle GetDeleteCurrentTeamBounds(LayoutMetrics layout)
    {
        return new Rectangle(layout.ContentLeft + ((layout.ContentWidth - DeleteButtonWidth) / 2), BaseDeleteCurrentY + layout.VerticalOffset, DeleteButtonWidth, DeleteButtonHeight);
    }

    private static Rectangle GetDeleteQuickMatchBounds(LayoutMetrics layout)
    {
        return new Rectangle(layout.ContentLeft + ((layout.ContentWidth - DeleteButtonWidth) / 2), BaseDeleteQuickMatchY + layout.VerticalOffset, DeleteButtonWidth, DeleteButtonHeight);
    }

    private static Rectangle GetDeleteAllBounds(LayoutMetrics layout)
    {
        return new Rectangle(layout.ContentLeft + ((layout.ContentWidth - DeleteButtonWidth) / 2), BaseDeleteAllY + layout.VerticalOffset, DeleteButtonWidth, DeleteButtonHeight);
    }

    private static Rectangle GetStatusBounds(LayoutMetrics layout)
    {
        return new Rectangle(layout.ContentLeft, BaseStatusY + layout.VerticalOffset, layout.ContentWidth, StatusHeight);
    }

    private Rectangle GetBackButtonBounds() => ScreenLayout.BackButtonBounds(_viewport);

    private readonly record struct LayoutMetrics(int ContentLeft, int ContentWidth, int VerticalOffset, int ClusterLeft, int SelectorValueWidth);
}
