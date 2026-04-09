using BaseballManager.Game.Screens;

using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.LiveMatch;
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

    private readonly Rectangle _playButtonBounds = new(160, 276, 240, 52);
    private readonly Rectangle _backButtonBounds = new(160, 340, 240, 44);

    public GameDayScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
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
            if (_playButtonBounds.Contains(mousePosition))
            {
                StartLiveMatch();
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
        var panelBounds = new Rectangle(140, 110, Math.Max(620, viewport.Width - 280), Math.Max(300, viewport.Height - 220));
        var nextGame = _franchiseSession.GetNextScheduledGame();
        var isPlayHovered = _playButtonBounds.Contains(Mouse.GetState().Position);
        var isBackHovered = _backButtonBounds.Contains(Mouse.GetState().Position);

        uiRenderer.DrawText("Game Day", new Vector2(160, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(26, 38, 46), Color.Transparent);
        uiRenderer.DrawTextInBounds($"Franchise Date: {_franchiseSession.GetCurrentFranchiseDate():yyyy-MM-dd}", new Rectangle(160, 130, panelBounds.Width - 40, 24), Color.Gold, uiRenderer.UiSmallFont);

        if (nextGame == null)
        {
            uiRenderer.DrawWrappedTextInBounds("No remaining scheduled games for this franchise. You can still launch a match from the hub.", new Rectangle(160, 166, panelBounds.Width - 40, 74), Color.White, uiRenderer.UiSmallFont, 3);
        }
        else
        {
            uiRenderer.DrawTextInBounds($"Matchup: {nextGame.AwayTeamName} at {nextGame.HomeTeamName}", new Rectangle(160, 166, panelBounds.Width - 40, 24), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"Scheduled: {nextGame.Date:ddd, MMM dd yyyy}", new Rectangle(160, 198, panelBounds.Width - 40, 22), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds("Press Enter or click Play to start today\'s live game. Esc returns to the Franchise Hub.", new Rectangle(160, 232, panelBounds.Width - 40, 42), Color.White, uiRenderer.UiSmallFont, 2);
        }

        uiRenderer.DrawButton("Play Live Match", _playButtonBounds, isPlayHovered ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
        uiRenderer.DrawButton("Back To Hub", _backButtonBounds, isBackHovered ? Color.DarkSlateGray : Color.SlateGray, Color.White);
    }

    private void StartLiveMatch()
    {
        _franchiseSession.PrepareFranchiseMatch();
        _screenManager.TransitionTo(nameof(LiveMatchScreen));
    }

    private bool IsNewKeyPress(KeyboardState currentKeyboardState, Keys key)
    {
        return currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }
}
