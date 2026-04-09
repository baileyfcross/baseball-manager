using BaseballManager.Game.Screens;

using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.MainMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.PostGame;

public sealed class PostGameScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private MouseState _previousMouseState = default;
    private KeyboardState _previousKeyboardState = default;
    private bool _ignoreClicksUntilRelease = true;

    private readonly Rectangle _boxScoreButtonBounds = new(160, 314, 220, 48);
    private readonly Rectangle _continueButtonBounds = new(160, 372, 220, 44);

    public PostGameScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
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
            if (_boxScoreButtonBounds.Contains(mousePosition))
            {
                _screenManager.TransitionTo(nameof(BoxScoreScreen));
                return;
            }

            if (_continueButtonBounds.Contains(mousePosition))
            {
                ContinueFlow();
                return;
            }
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Enter))
        {
            _screenManager.TransitionTo(nameof(BoxScoreScreen));
            return;
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Escape))
        {
            ContinueFlow();
            return;
        }

        _previousMouseState = currentMouseState;
        _previousKeyboardState = currentKeyboardState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        var summary = _franchiseSession.GetLastCompletedLiveMatchSummary();
        var viewport = uiRenderer.Viewport;
        var panelBounds = new Rectangle(140, 110, Math.Max(700, viewport.Width - 280), Math.Max(340, viewport.Height - 220));
        var isBoxScoreHovered = _boxScoreButtonBounds.Contains(Mouse.GetState().Position);
        var isContinueHovered = _continueButtonBounds.Contains(Mouse.GetState().Position);

        uiRenderer.DrawText("Post Game", new Vector2(160, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(35, 28, 24), Color.Transparent);

        if (summary == null)
        {
            uiRenderer.DrawWrappedTextInBounds("No completed game summary is available. Return to the hub and launch another game.", new Rectangle(160, 154, panelBounds.Width - 40, 52), Color.White, uiRenderer.UiSmallFont, 2);
        }
        else
        {
            var resultHeader = summary.WinningTeamName == "Tie"
                ? "Final: Tie Game"
                : $"Final: {summary.WinningTeamName} Win";

            uiRenderer.DrawTextInBounds(resultHeader, new Rectangle(160, 146, panelBounds.Width - 40, 28), Color.Gold, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"{summary.AwayAbbreviation} {summary.AwayRuns} - {summary.HomeAbbreviation} {summary.HomeRuns}", new Rectangle(160, 180, panelBounds.Width - 40, 30), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"Completed: {summary.CompletedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}   Inning: {(summary.EndedInTopHalf ? "Top" : "Bottom")} {summary.FinalInningNumber}", new Rectangle(160, 216, panelBounds.Width - 40, 22), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds(summary.FinalPlayDescription, new Rectangle(160, 252, panelBounds.Width - 40, 48), Color.White, uiRenderer.UiSmallFont, 2);
        }

        uiRenderer.DrawButton("View Box Score", _boxScoreButtonBounds, isBoxScoreHovered ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton("Continue", _continueButtonBounds, isContinueHovered ? Color.DarkSlateGray : Color.SlateGray, Color.White);
    }

    private void ContinueFlow()
    {
        var summary = _franchiseSession.GetLastCompletedLiveMatchSummary();
        var destination = summary?.WasFranchiseMatch == true
            ? nameof(FranchiseHubScreen)
            : nameof(MainMenuScreen);
        _screenManager.TransitionTo(destination);
    }

    private bool IsNewKeyPress(KeyboardState currentKeyboardState, Keys key)
    {
        return currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }
}
