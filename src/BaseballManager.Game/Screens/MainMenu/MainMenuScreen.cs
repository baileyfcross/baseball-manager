using BaseballManager.Application.SaveLoad;
using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
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
    private readonly LoadGameUseCase _loadGameUseCase = new();
    private MouseState _previousMouseState = default;
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
        _buttons.Add(new ButtonControl { Label = "Start Game", OnClick = () => StartNewGame() });
        _buttons.Add(new ButtonControl { Label = "Load Game", OnClick = () => LoadGame() });
        _buttons.Add(new ButtonControl { Label = "Exit Game", OnClick = () => ExitGame() });
    }

    private Rectangle GetButtonBounds(int index, int viewportWidth, int viewportHeight)
    {
        var centerX = viewportWidth / 2;
        var y = StartY + index * (ButtonHeight + ButtonSpacing);
        return new Rectangle(centerX - ButtonWidth / 2, y, ButtonWidth, ButtonHeight);
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;

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

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        // Draw title
        uiRenderer.DrawText("Baseball Manager", new Vector2(100, 50), Color.White);

        // Draw buttons with labels
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
        _screenManager.TransitionTo(nameof(TeamSelectionScreen));
    }

    private void LoadGame()
    {
        _loadGameUseCase.Execute();

        if (_franchiseSession.SelectedTeam != null)
        {
            Console.WriteLine($"Resuming franchise: {_franchiseSession.SelectedTeamName}");
            _screenManager.TransitionTo(nameof(FranchiseHubScreen));
            return;
        }

        Console.WriteLine("No saved franchise team found. Opening team selection...");
        _screenManager.TransitionTo(nameof(TeamSelectionScreen));
    }

    private void ExitGame()
    {
        Console.WriteLine("Exiting game...");
        System.Environment.Exit(0);
    }
}
