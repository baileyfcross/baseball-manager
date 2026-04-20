using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Graphics.Rendering.LiveMatch;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.PostGame;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Game.Screens;
using BaseballManager.Game.UI.Layout;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.LiveMatch;

public sealed class LiveMatchScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly LiveMatchPresenter _presenter;
    private readonly LiveMatchHudRenderer _hudRenderer = new();
    private FieldRenderer? _fieldRenderer;
    private Point _viewport = new(1280, 720);
    private Rectangle BackButtonBounds => ScreenLayout.BackButtonBounds(_viewport);
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
            _presenter.ResetMatch(_franchiseSession.PendingLiveMatchMode);
        }
        catch (Exception ex)
        {
            _fatalErrorMessage = ex.Message;
            Console.WriteLine(ex);
        }
    }

    public override void OnExit()
    {
        try
        {
            _presenter.SaveMatchProgress();
        }
        catch (Exception ex)
        {
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
                 BackButtonBounds.Contains(currentMouseState.Position))
        {
            ReturnToPreviousScreen();
        }

        if (_presenter.IsManagerMenuVisible)
        {
            if (IsNewKeyPress(currentKeyboardState, Keys.M) || IsNewKeyPress(currentKeyboardState, Keys.Escape))
            {
                _presenter.ToggleManagerMenu();
            }
            else if (IsNewKeyPress(currentKeyboardState, Keys.Tab))
            {
                _presenter.CycleManagerMode();
            }
            else if (IsNewKeyPress(currentKeyboardState, Keys.Up))
            {
                _presenter.MoveManagerSelection(-1);
            }
            else if (IsNewKeyPress(currentKeyboardState, Keys.Down))
            {
                _presenter.MoveManagerSelection(1);
            }
            else if (IsNewKeyPress(currentKeyboardState, Keys.Left))
            {
                _presenter.MoveManagerTargetLineupSlot(-1);
            }
            else if (IsNewKeyPress(currentKeyboardState, Keys.Right))
            {
                _presenter.MoveManagerTargetLineupSlot(1);
            }
            else if (IsNewKeyPress(currentKeyboardState, Keys.Enter))
            {
                _presenter.ApplySelectedManagerAction();
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
            return;
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

                if (IsNewKeyPress(currentKeyboardState, Keys.M))
                {
                    _presenter.ToggleManagerMenu();
                }

                UpdatePlayResolution(gameTime);
                UpdateFieldLayer(gameTime);
                UpdateOverlayLayer();
                HandlePauseAndManagerCommands();

                if (_presenter.ViewModel.IsGameOver)
                {
                    _screenManager.TransitionTo(nameof(BoxScoreScreen));
                    return;
                }
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
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);
        var isHovered = BackButtonBounds.Contains(Mouse.GetState().Position);

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
            uiRenderer.DrawText("Live match failed to load.", new Vector2(168, 120), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawWrappedTextInBounds(_fatalErrorMessage, new Rectangle(168, 166, 700, 48), Color.Orange, uiRenderer.UiSmallFont, 2);
            uiRenderer.DrawTextInBounds("Press Esc or click Back to return.", new Rectangle(168, 220, 360, 20), Color.White, uiRenderer.UiSmallFont);
        }

        uiRenderer.DrawButton("Back", BackButtonBounds, isHovered ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
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
        if (_presenter.ViewModel.IsGameOver)
        {
            _screenManager.TransitionTo(nameof(BoxScoreScreen));
            return;
        }

        _franchiseSession.SetPreferLiveBoxScore(false);
        var screenName = _franchiseSession.PendingLiveMatchMode == LiveMatchMode.QuickMatch
            ? nameof(MainMenuScreen)
            : nameof(FranchiseHubScreen);
        _screenManager.TransitionTo(screenName);
    }
}
