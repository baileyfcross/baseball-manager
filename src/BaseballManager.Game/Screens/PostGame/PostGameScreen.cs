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

    private readonly Rectangle _boxScoreButtonBounds = new(160, 484, 220, 48);
    private readonly Rectangle _continueButtonBounds = new(392, 484, 260, 44);

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

        if (IsNewKeyPress(currentKeyboardState, Keys.Enter) || IsNewKeyPress(currentKeyboardState, Keys.Escape))
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
        var panelBounds = new Rectangle(140, 110, Math.Max(820, viewport.Width - 280), Math.Max(400, viewport.Height - 220));
        var isBoxScoreHovered = _boxScoreButtonBounds.Contains(Mouse.GetState().Position);
        var isContinueHovered = _continueButtonBounds.Contains(Mouse.GetState().Position);
        var continueLabel = summary?.WasFranchiseMatch == true ? "Return To Franchise Hub" : "Return To Main Menu";

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
            var venueLabel = string.IsNullOrWhiteSpace(summary.Venue) ? "Venue TBD" : summary.Venue;

            uiRenderer.DrawTextInBounds(resultHeader, new Rectangle(160, 146, panelBounds.Width - 40, 28), Color.Gold, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"{summary.AwayAbbreviation} {summary.AwayRuns} - {summary.HomeAbbreviation} {summary.HomeRuns}", new Rectangle(160, 180, panelBounds.Width - 40, 30), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"Completed: {summary.CompletedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}   Venue: {venueLabel}", new Rectangle(160, 216, panelBounds.Width - 40, 22), Color.White, uiRenderer.UiSmallFont);

            if (!string.IsNullOrWhiteSpace(summary.SelectedTeamResultLabel))
            {
                uiRenderer.DrawTextInBounds(summary.SelectedTeamResultLabel, new Rectangle(160, 248, panelBounds.Width - 40, 22), Color.LightGreen, uiRenderer.UiSmallFont);
            }

            if (summary.WasFranchiseMatch)
            {
                if (summary.FranchiseDateAfterGame != default)
                {
                    uiRenderer.DrawTextInBounds($"Franchise Date: {summary.FranchiseDateAfterGame:yyyy-MM-dd}", new Rectangle(160, 278, panelBounds.Width - 40, 22), Color.White, uiRenderer.UiSmallFont);
                }

                var nextMatchupLabel = string.IsNullOrWhiteSpace(summary.NextGameLabel)
                    ? "Next matchup: return to the Franchise Hub to continue the season."
                    : $"Next matchup: {summary.NextGameLabel}";
                uiRenderer.DrawWrappedTextInBounds(nextMatchupLabel, new Rectangle(160, 306, panelBounds.Width - 40, 42), Color.White, uiRenderer.UiSmallFont, 2);
            }

            uiRenderer.DrawWrappedTextInBounds(summary.FinalPlayDescription, new Rectangle(160, 360, panelBounds.Width - 40, 64), Color.White, uiRenderer.UiSmallFont, 3);
        }

        uiRenderer.DrawButton("View Box Score", _boxScoreButtonBounds, isBoxScoreHovered ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(continueLabel, _continueButtonBounds, isContinueHovered ? Color.DarkSlateGray : Color.SlateGray, Color.White);
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
