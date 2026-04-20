using BaseballManager.Game.Screens;

using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.LiveMatch;
using BaseballManager.Game.Screens.Schedule;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.GameDay;

public sealed class GameDayScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private MouseState _previousMouseState = default;
    private KeyboardState _previousKeyboardState = default;
    private bool _ignoreClicksUntilRelease = true;
    private string _statusMessage = string.Empty;

    private readonly Rectangle _startButtonBounds = new(160, 560, 220, 46);
    private readonly Rectangle _scheduleButtonBounds = new(392, 560, 220, 46);
    private readonly Rectangle _backButtonBounds = new(624, 560, 220, 46);

    public GameDayScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _statusMessage = _franchiseSession.GetSelectedTeamLineupValidation().Summary;
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;
        var currentKeyboardState = inputManager.KeyboardState;

        if (_ignoreClicksUntilRelease)
        {
            if (currentMouseState.LeftButton == ButtonState.Released)
            {
                _ignoreClicksUntilRelease = false;
            }
        }
        else if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePosition = currentMouseState.Position;
            if (_startButtonBounds.Contains(mousePosition))
            {
                StartLiveMatch();
                return;
            }

            if (_scheduleButtonBounds.Contains(mousePosition))
            {
                _screenManager.TransitionTo(nameof(ScheduleScreen));
                return;
            }

            if (_backButtonBounds.Contains(mousePosition))
            {
                _screenManager.TransitionTo(nameof(FranchiseHubScreen));
                return;
            }
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Enter))
        {
            StartLiveMatch();
            return;
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Escape))
        {
            _screenManager.TransitionTo(nameof(FranchiseHubScreen));
            return;
        }

        _previousMouseState = currentMouseState;
        _previousKeyboardState = currentKeyboardState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        var viewport = uiRenderer.Viewport;
        var nextGame = _franchiseSession.GetNextScheduledGame();
        var lineupValidation = _franchiseSession.GetSelectedTeamLineupValidation();
        var canStartGame = nextGame != null && lineupValidation.IsValid;
        var panelBounds = new Rectangle(140, 110, Math.Max(900, viewport.Width - 280), Math.Max(520, viewport.Height - 220));
        var cardWidth = (panelBounds.Width - 88) / 2;
        var awayCardBounds = new Rectangle(panelBounds.X + 20, panelBounds.Y + 126, cardWidth, 180);
        var homeCardBounds = new Rectangle(awayCardBounds.Right + 24, awayCardBounds.Y, cardWidth, 180);
        var startHovered = canStartGame && _startButtonBounds.Contains(Mouse.GetState().Position);
        var scheduleHovered = _scheduleButtonBounds.Contains(Mouse.GetState().Position);
        var backHovered = _backButtonBounds.Contains(Mouse.GetState().Position);

        uiRenderer.DrawText("Game Day", new Vector2(160, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(26, 38, 46), Color.Transparent);
        uiRenderer.DrawTextInBounds($"Franchise Date: {_franchiseSession.GetCurrentFranchiseDate():yyyy-MM-dd}", new Rectangle(panelBounds.X + 20, panelBounds.Y + 16, panelBounds.Width - 40, 22), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(panelBounds.X + 20, panelBounds.Y + 42, panelBounds.Width - 40, 22), Color.White, uiRenderer.UiSmallFont);

        if (nextGame == null)
        {
            uiRenderer.DrawTextInBounds("No scheduled franchise game is available.", new Rectangle(panelBounds.X + 20, panelBounds.Y + 88, panelBounds.Width - 40, 28), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawWrappedTextInBounds("The current franchise schedule has no remaining unplayed games. Use Schedule / Training to review results or return to the Franchise Hub.", new Rectangle(panelBounds.X + 20, panelBounds.Y + 126, panelBounds.Width - 40, 84), Color.White, uiRenderer.UiSmallFont, 3);
        }
        else
        {
            var awayStarter = _franchiseSession.GetScheduledStartingPitcher(nextGame.AwayTeamName);
            var homeStarter = _franchiseSession.GetScheduledStartingPitcher(nextGame.HomeTeamName);
            var venueLabel = string.IsNullOrWhiteSpace(nextGame.Venue) ? $"{nextGame.HomeTeamName} Ballpark" : nextGame.Venue;
            var gameNumberLabel = nextGame.GameNumber.GetValueOrDefault(1) > 1
                ? $"Doubleheader Game {nextGame.GameNumber}"
                : "Scheduled Game";

            uiRenderer.DrawTextInBounds($"{nextGame.AwayTeamName} at {nextGame.HomeTeamName}", new Rectangle(panelBounds.X + 20, panelBounds.Y + 82, panelBounds.Width - 40, 28), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"{nextGame.Date:dddd, MMMM d yyyy} • {gameNumberLabel} • {venueLabel}", new Rectangle(panelBounds.X + 20, panelBounds.Y + 108, panelBounds.Width - 40, 20), Color.White, uiRenderer.UiSmallFont);

            DrawTeamCard(
                uiRenderer,
                awayCardBounds,
                nextGame.AwayTeamName,
                _franchiseSession.GetTeamRecordLabel(nextGame.AwayTeamName),
                awayStarter?.PlayerName ?? "TBD",
                _franchiseSession.SelectedTeam != null && string.Equals(_franchiseSession.SelectedTeam.Name, nextGame.AwayTeamName, StringComparison.OrdinalIgnoreCase),
                isHomeTeam: false);

            DrawTeamCard(
                uiRenderer,
                homeCardBounds,
                nextGame.HomeTeamName,
                _franchiseSession.GetTeamRecordLabel(nextGame.HomeTeamName),
                homeStarter?.PlayerName ?? "TBD",
                _franchiseSession.SelectedTeam != null && string.Equals(_franchiseSession.SelectedTeam.Name, nextGame.HomeTeamName, StringComparison.OrdinalIgnoreCase),
                isHomeTeam: true);

            var scoutingBounds = new Rectangle(panelBounds.X + 20, awayCardBounds.Bottom + 20, panelBounds.Width - 40, 118);
            uiRenderer.DrawButton(string.Empty, scoutingBounds, new Color(32, 46, 58), Color.Transparent);
            uiRenderer.DrawTextInBounds("Pregame Notes", new Rectangle(scoutingBounds.X + 12, scoutingBounds.Y + 8, scoutingBounds.Width - 24, 20), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds($"{lineupValidation.Summary} Start Game enters the live simulation for the next scheduled franchise matchup once the lineup is valid.", new Rectangle(scoutingBounds.X + 12, scoutingBounds.Y + 34, scoutingBounds.Width - 24, scoutingBounds.Height - 42), lineupValidation.IsValid ? Color.White : Color.Orange, uiRenderer.UiSmallFont, 4);
        }

        var startLabel = nextGame == null ? "No Game Scheduled" : lineupValidation.IsValid ? "Start Game" : "Lineup Invalid";
        uiRenderer.DrawButton(startLabel, _startButtonBounds, canStartGame ? (startHovered ? Color.DarkOliveGreen : Color.OliveDrab) : new Color(76, 76, 76), Color.White);
        uiRenderer.DrawButton("Schedule", _scheduleButtonBounds, scheduleHovered ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton("Back To Hub", _backButtonBounds, backHovered ? Color.DarkSlateGray : Color.SlateGray, Color.White);
    }

    private static void DrawTeamCard(UiRenderer uiRenderer, Rectangle bounds, string teamName, string recordLabel, string probableStarter, bool isControlledTeam, bool isHomeTeam)
    {
        var background = isControlledTeam ? new Color(52, 74, 90) : new Color(34, 52, 68);
        uiRenderer.DrawButton(string.Empty, bounds, background, Color.Transparent);
        uiRenderer.DrawTextInBounds(isHomeTeam ? "Home" : "Away", new Rectangle(bounds.X + 12, bounds.Y + 10, bounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(teamName, new Rectangle(bounds.X + 12, bounds.Y + 34, bounds.Width - 24, 26), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"Record: {recordLabel}", new Rectangle(bounds.X + 12, bounds.Y + 70, bounds.Width - 24, 20), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds($"Probable Starter: {probableStarter}", new Rectangle(bounds.X + 12, bounds.Y + 98, bounds.Width - 24, 40), Color.White, uiRenderer.UiSmallFont, 2);

        if (isControlledTeam)
        {
            uiRenderer.DrawTextInBounds("Your club", new Rectangle(bounds.X + 12, bounds.Bottom - 28, bounds.Width - 24, 18), Color.LightGreen, uiRenderer.UiSmallFont);
        }
    }

    private void StartLiveMatch()
    {
        if (!_franchiseSession.HasPendingScheduledGame())
        {
            _statusMessage = "No scheduled game is available.";
            return;
        }

        var lineupValidation = _franchiseSession.GetSelectedTeamLineupValidation();
        if (!lineupValidation.IsValid)
        {
            _statusMessage = lineupValidation.Summary;
            return;
        }

        _franchiseSession.PrepareFranchiseMatch();
        _screenManager.TransitionTo(nameof(LiveMatchScreen));
    }

    private bool IsNewKeyPress(KeyboardState currentKeyboardState, Keys key)
    {
        return currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }
}
