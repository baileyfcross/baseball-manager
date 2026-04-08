using BaseballManager.Application.Franchise;
using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.LiveMatch;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Game.Screens.Roster;
using BaseballManager.Game.Screens.Schedule;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class FranchiseHubScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly StartMatchUseCase _startMatchUseCase = new();
    private readonly List<ButtonControl> _buttons = new();
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private string _statusMessage = "";

    private const int ButtonWidth = 240;

    public FranchiseHubScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        _buttons.Add(new ButtonControl { Label = "Live Match", OnClick = () => StartLiveMatch() });
        _buttons.Add(new ButtonControl { Label = "Sim Day", OnClick = () => SimDay() });
        _buttons.Add(new ButtonControl { Label = "Sim Next Game", OnClick = () => SimNextGame() });
        _buttons.Add(new ButtonControl { Label = "Roster", OnClick = () => _screenManager.TransitionTo(nameof(RosterScreen)) });
        _buttons.Add(new ButtonControl { Label = "Lineup", OnClick = () => _screenManager.TransitionTo(nameof(LineupScreen)) });
        _buttons.Add(new ButtonControl { Label = "Rotation", OnClick = () => _screenManager.TransitionTo(nameof(RotationScreen)) });
        _buttons.Add(new ButtonControl { Label = "Schedule / Training", OnClick = () => _screenManager.TransitionTo(nameof(ScheduleScreen)) });
        _buttons.Add(new ButtonControl { Label = "Training Reports", OnClick = () => _screenManager.TransitionTo(nameof(TrainingReportsScreen)) });
        _buttons.Add(new ButtonControl { Label = "Scouting / Transfers", OnClick = () => _screenManager.TransitionTo(nameof(ScoutingScreen)) });
        _buttons.Add(new ButtonControl { Label = "Coaching Staff", OnClick = () => _screenManager.TransitionTo(nameof(CoachingStaffScreen)) });
        _buttons.Add(new ButtonControl { Label = "Finances", OnClick = () => _screenManager.TransitionTo(nameof(FinancesScreen)) });
        _buttons.Add(new ButtonControl { Label = "Main Menu", OnClick = () => _screenManager.TransitionTo(nameof(MainMenuScreen)) });
    }

    private Rectangle GetButtonBounds(int index, int viewportWidth, int viewportHeight)
    {
        var panelBounds = GetMenuPanelBounds(viewportWidth, viewportHeight);
        var buttonHeight = GetButtonHeight(viewportHeight);
        var buttonSpacing = GetButtonSpacing();
        var width = Math.Min(panelBounds.Width - 24, GetColumnButtonWidth());
        var x = panelBounds.X + ((panelBounds.Width - width) / 2);
        var y = panelBounds.Y + 18 + index * (buttonHeight + buttonSpacing);
        return new Rectangle(x, y, width, buttonHeight);
    }

    private Rectangle GetMenuPanelBounds(int viewportWidth, int viewportHeight)
    {
        var buttonHeight = GetButtonHeight(viewportHeight);
        var buttonSpacing = GetButtonSpacing();
        var totalHeight = (_buttons.Count * buttonHeight) + (Math.Max(0, _buttons.Count - 1) * buttonSpacing) + 36;
        var width = Math.Clamp(viewportWidth / 3, 320, 460);
        var x = (viewportWidth - width) / 2;
        var y = Math.Max(102, (viewportHeight - totalHeight - 132) / 2);
        return new Rectangle(x, y, width, totalHeight);
    }

    private int GetButtonHeight(int viewportHeight)
    {
        var divisor = _buttons.Count >= 10 ? 20 : 16;
        return Math.Clamp(viewportHeight / divisor, 34, 50);
    }

    private int GetButtonSpacing()
    {
        return _buttons.Count >= 10 ? 6 : 10;
    }

    private int GetColumnButtonWidth()
    {
        return _buttons.Count == 0
            ? ButtonWidth
            : _buttons.Max(button => ButtonControl.GetSuggestedWidth(button.Label, ButtonWidth));
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
            var mousePos = currentMouseState.Position;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (GetButtonBounds(i, _viewport.X, _viewport.Y).Contains(mousePos))
                {
                    _buttons[i].Click();
                }
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        if (string.IsNullOrEmpty(_statusMessage))
        {
            _statusMessage = "Use Sim Day to move one calendar day at a time with coach feedback from practices, or Sim Next Game to jump ahead.";
        }
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        uiRenderer.DrawText("Franchise Hub", new Vector2(56, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText(_franchiseSession.SelectedTeamName, new Vector2(56, 82), Color.White);

        var menuPanelBounds = GetMenuPanelBounds(_viewport.X, _viewport.Y);
        uiRenderer.DrawButton(string.Empty, menuPanelBounds, new Color(38, 48, 56), Color.White);

        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            var bounds = GetButtonBounds(i, _viewport.X, _viewport.Y);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var bgColor = isHovered ? Color.DarkGray : Color.Gray;

            uiRenderer.DrawButton(button.Label, bounds, bgColor, Color.White);
        }

        var statusBounds = new Rectangle(68, _viewport.Y - 138, Math.Max(520, _viewport.X - 136), 102);
        uiRenderer.DrawButton(string.Empty, statusBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawTextInBounds("Latest Update", new Rectangle(statusBounds.X + 12, statusBounds.Y + 6, 200, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(statusBounds.X + 12, statusBounds.Y + 26, statusBounds.Width - 24, statusBounds.Height - 32), Color.White, uiRenderer.UiSmallFont, 5);
    }

    private void StartLiveMatch()
    {
        _startMatchUseCase.Execute();
        _franchiseSession.PrepareFranchiseMatch();
        _screenManager.TransitionTo(nameof(LiveMatchScreen));
    }

    private void SimDay()
    {
        _franchiseSession.SimulateCurrentDay(out var message);
        _statusMessage = message;
    }

    private void SimNextGame()
    {
        _franchiseSession.SimulateNextScheduledGame(out var message);
        _statusMessage = message;
    }

    private static IEnumerable<string> WrapText(string text, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (candidate.Length <= maxCharacters)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }

            currentLine = word;
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            yield return currentLine;
        }
    }
}
