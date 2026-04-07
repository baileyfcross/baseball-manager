using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.LiveMatch;
using BaseballManager.Game.Screens.Options;
using BaseballManager.Game.Screens.TeamSelection;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.MainMenu;

public sealed class MainMenuScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly List<ButtonControl> _buttons = new();
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);

    private const int ButtonWidth = 200;
    private const int ButtonHeight = 50;
    private const int ButtonSpacing = 20;
    private const int StartY = 200;

    public MainMenuScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        _buttons.Clear();

        if (_franchiseSession.HasFranchiseSaveData)
        {
            _buttons.Add(new ButtonControl { Label = "Resume Game", OnClick = () => ResumeGame() });
        }

        _buttons.Add(new ButtonControl
        {
            Label = _franchiseSession.HasAnySaveData ? "New Game" : "Start Game",
            OnClick = () => StartNewGame()
        });
        _buttons.Add(new ButtonControl { Label = "Quick Match", OnClick = () => OpenQuickMatch() });
        _buttons.Add(new ButtonControl { Label = "Options", OnClick = () => OpenOptions() });
        _buttons.Add(new ButtonControl { Label = "Exit Game", OnClick = () => ExitGame() });
    }

    private Rectangle GetButtonBounds(int index, int viewportWidth, int viewportHeight)
    {
        var centerX = viewportWidth / 2;
        var y = StartY + index * (ButtonHeight + ButtonSpacing);
        var width = _buttons.Count == 0 ? ButtonWidth : _buttons.Max(button => ButtonControl.GetSuggestedWidth(button.Label, ButtonWidth));
        return new Rectangle(centerX - width / 2, y, width, ButtonHeight);
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

        // Check for button clicks
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
        InitializeButtons();
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        uiRenderer.DrawText("Baseball Manager", new Vector2(100, 50), Color.White, uiRenderer.UiMediumFont);

        if (_franchiseSession.HasFranchiseSaveData)
        {
            uiRenderer.DrawText($"Current Franchise: {_franchiseSession.SelectedTeamName}", new Vector2(100, 96), Color.White);
        }

        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            var bounds = GetButtonBounds(i, _viewport.X, _viewport.Y);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var bgColor = isHovered ? Color.DarkGray : Color.Gray;

            uiRenderer.DrawButton(button.Label, bounds, bgColor, Color.White);

            // Debug output to console for buttons (fallback while fonts load)
            if (isHovered)
            {
                Console.SetCursorPosition(0, i);
                Console.WriteLine($"> {button.Label}");
            }
        }
    }

    private void StartNewGame()
    {
        Console.WriteLine("Opening team selection...");
        _franchiseSession.PrepareFranchiseMatch();
        _screenManager.TransitionTo(nameof(TeamSelectionScreen));
    }

    private void ResumeGame()
    {
        if (_franchiseSession.SelectedTeam == null)
        {
            _screenManager.TransitionTo(nameof(TeamSelectionScreen));
            return;
        }

        Console.WriteLine($"Resuming franchise: {_franchiseSession.SelectedTeamName}");
        _franchiseSession.PrepareFranchiseMatch();
        _screenManager.TransitionTo(nameof(FranchiseHubScreen));
    }

    private void OpenQuickMatch()
    {
        Console.WriteLine("Launching quick live match...");
        _franchiseSession.PrepareQuickMatch();
        _screenManager.TransitionTo(nameof(LiveMatchScreen));
    }

    private void OpenOptions()
    {
        _screenManager.TransitionTo(nameof(OptionsScreen));
    }

    private void ExitGame()
    {
        Console.WriteLine("Exiting game...");
        System.Environment.Exit(0);
    }
}
