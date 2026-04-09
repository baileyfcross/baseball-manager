using BaseballManager.Game.Screens;

using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.MainMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.PostGame;

public sealed class BoxScoreScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private MouseState _previousMouseState = default;
    private KeyboardState _previousKeyboardState = default;
    private bool _ignoreClicksUntilRelease = true;

    private readonly Rectangle _backButtonBounds = new(160, 450, 220, 44);
    private readonly Rectangle _continueButtonBounds = new(390, 450, 220, 44);

    public BoxScoreScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
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
            if (_backButtonBounds.Contains(mousePosition))
            {
                _screenManager.TransitionTo(nameof(PostGameScreen));
                return;
            }

            if (_continueButtonBounds.Contains(mousePosition))
            {
                ContinueFlow();
                return;
            }
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Escape))
        {
            _screenManager.TransitionTo(nameof(PostGameScreen));
            return;
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Enter))
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
        var panelBounds = new Rectangle(140, 110, Math.Max(860, viewport.Width - 280), Math.Max(390, viewport.Height - 220));
        var isBackHovered = _backButtonBounds.Contains(Mouse.GetState().Position);
        var isContinueHovered = _continueButtonBounds.Contains(Mouse.GetState().Position);

        uiRenderer.DrawText("Box Score", new Vector2(160, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(22, 34, 42), Color.Transparent);

        if (summary == null)
        {
            uiRenderer.DrawWrappedTextInBounds("No completed game data available for this box score.", new Rectangle(160, 156, panelBounds.Width - 40, 50), Color.White, uiRenderer.UiSmallFont, 2);
        }
        else
        {
            uiRenderer.DrawTextInBounds($"Final: {summary.AwayAbbreviation} {summary.AwayRuns} - {summary.HomeAbbreviation} {summary.HomeRuns}", new Rectangle(160, 144, panelBounds.Width - 40, 30), Color.Gold, uiRenderer.UiMediumFont);

            var headerY = 196;
            uiRenderer.DrawTextInBounds("Team", new Rectangle(160, headerY, 280, 22), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds("R", new Rectangle(450, headerY, 90, 22), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds("H", new Rectangle(550, headerY, 90, 22), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds("Pitches", new Rectangle(650, headerY, 120, 22), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds("Starter", new Rectangle(780, headerY, panelBounds.Width - 640, 22), Color.White, uiRenderer.UiSmallFont);

            DrawTeamRow(uiRenderer, y: headerY + 30, summary.AwayTeamName, summary.AwayRuns, summary.AwayHits, summary.AwayPitchCount, summary.AwayStartingPitcherName, new Color(34, 52, 68));
            DrawTeamRow(uiRenderer, y: headerY + 66, summary.HomeTeamName, summary.HomeRuns, summary.HomeHits, summary.HomePitchCount, summary.HomeStartingPitcherName, new Color(30, 48, 58));

            uiRenderer.DrawTextInBounds($"Plays Completed: {summary.CompletedPlays}", new Rectangle(160, headerY + 126, panelBounds.Width - 40, 22), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"Final Frame: {(summary.EndedInTopHalf ? "Top" : "Bottom")} {summary.FinalInningNumber}", new Rectangle(160, headerY + 150, panelBounds.Width - 40, 22), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds(summary.FinalPlayDescription, new Rectangle(160, headerY + 182, panelBounds.Width - 40, 60), Color.White, uiRenderer.UiSmallFont, 3);
        }

        uiRenderer.DrawButton("Back", _backButtonBounds, isBackHovered ? Color.DarkSlateGray : Color.SlateGray, Color.White);
        uiRenderer.DrawButton("Continue", _continueButtonBounds, isContinueHovered ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
    }

    private static void DrawTeamRow(UiRenderer uiRenderer, int y, string teamName, int runs, int hits, int pitches, string starterName, Color background)
    {
        var rowBounds = new Rectangle(160, y, 900, 30);
        uiRenderer.DrawButton(string.Empty, rowBounds, background, Color.Transparent);
        uiRenderer.DrawTextInBounds(teamName, new Rectangle(166, y + 2, 270, 26), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(runs.ToString(), new Rectangle(450, y + 2, 90, 26), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(hits.ToString(), new Rectangle(550, y + 2, 90, 26), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(pitches.ToString(), new Rectangle(650, y + 2, 120, 26), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(starterName, new Rectangle(780, y + 2, 274, 26), Color.White, uiRenderer.UiSmallFont);
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
