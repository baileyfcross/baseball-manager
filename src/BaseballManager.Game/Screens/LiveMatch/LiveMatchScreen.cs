using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Graphics.Rendering.LiveMatch;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Game.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.LiveMatch;

public sealed class LiveMatchScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly LiveMatchPresenter _presenter;
    private readonly Rectangle _backButtonBounds = new(24, 20, 120, 42);
    private readonly LiveMatchHudRenderer _hudRenderer = new();
    private FieldRenderer? _fieldRenderer;
    private KeyboardState _previousKeyboardState = default;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private string? _fatalErrorMessage;

    public LiveMatchScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _presenter = new LiveMatchPresenter(leagueData, franchiseSession);
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _fatalErrorMessage = null;

        try
        {
            _presenter.ResetMatch();
        }
        catch (Exception ex)
        {
            _fatalErrorMessage = ex.Message;
            Console.WriteLine(ex);
        }
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
        else if (_previousMouseState.LeftButton == ButtonState.Released &&
                 currentMouseState.LeftButton == ButtonState.Pressed &&
                 _backButtonBounds.Contains(currentMouseState.Position))
        {
            ReturnToPreviousScreen();
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Escape))
        {
            ReturnToPreviousScreen();
        }

        if (_fatalErrorMessage == null)
        {
            try
            {
                if (IsNewKeyPress(currentKeyboardState, Keys.Space))
                {
                    _presenter.TogglePause();
                }

                if (IsNewKeyPress(currentKeyboardState, Keys.Enter))
                {
                    _presenter.StepPitch();
                }

                UpdatePlayResolution(gameTime);
                UpdateFieldLayer(gameTime);
                UpdateOverlayLayer();
                HandlePauseAndManagerCommands();
            }
            catch (Exception ex)
            {
                _fatalErrorMessage = ex.Message;
                Console.WriteLine(ex);
            }
        }

        _previousKeyboardState = currentKeyboardState;
        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        var isHovered = _backButtonBounds.Contains(Mouse.GetState().Position);

        if (_fatalErrorMessage == null)
        {
            try
            {
                _fieldRenderer ??= new FieldRenderer(uiRenderer.GraphicsDevice);
                _fieldRenderer.Draw(_presenter.ViewModel, uiRenderer.Viewport);
                _hudRenderer.Draw(_presenter.ViewModel, uiRenderer);
            }
            catch (Exception ex)
            {
                _fatalErrorMessage = ex.Message;
                Console.WriteLine(ex);
            }
        }

        if (_fatalErrorMessage != null)
        {
            uiRenderer.DrawText("Live match failed to load.", new Vector2(120, 140), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawText(_fatalErrorMessage, new Vector2(120, 190), Color.Orange);
            uiRenderer.DrawText("Press Esc or click Back to return.", new Vector2(120, 230), Color.White);
        }

        uiRenderer.DrawButton("Back", _backButtonBounds, isHovered ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
    }

    public void UpdateFieldLayer(GameTime gameTime)
    {
        _presenter.UpdateFieldView();
    }

    public void UpdatePlayResolution(GameTime gameTime)
    {
        _presenter.Update(gameTime);
    }

    public void UpdateOverlayLayer()
    {
        _presenter.UpdateOverlays();
    }

    public void HandlePauseAndManagerCommands()
    {
        _presenter.HandleManagerCommands();
    }

    private bool IsNewKeyPress(KeyboardState currentKeyboardState, Keys key)
    {
        return currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private void ReturnToPreviousScreen()
    {
        var screenName = _franchiseSession.SelectedTeam != null
            ? nameof(FranchiseHubScreen)
            : nameof(MainMenuScreen);
        _screenManager.TransitionTo(screenName);
    }
}
