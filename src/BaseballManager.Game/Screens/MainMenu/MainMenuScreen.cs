using BaseballManager.Application.Franchise;
using BaseballManager.Application.SaveLoad;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.MainMenu;

public sealed class MainMenuScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly List<ButtonControl> _buttons = new();
    private readonly List<Rectangle> _buttonBounds = new();
    private readonly StartNewFranchiseUseCase _startNewFranchiseUseCase = new();
    private readonly LoadGameUseCase _loadGameUseCase = new();
    private MouseState _previousMouseState = default;

    public MainMenuScreen(ScreenManager screenManager)
    {
        _screenManager = screenManager;
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        var centerX = 640; // Assuming 1280x720 window
        var startY = 200;
        const int buttonWidth = 200;
        const int buttonHeight = 50;
        const int spacing = 20;

        // Start Game Button
        var startGameButton = new ButtonControl
        {
            Label = "Start Game",
            OnClick = () => StartNewGame()
        };
        _buttons.Add(startGameButton);
        _buttonBounds.Add(new Rectangle(
            centerX - buttonWidth / 2,
            startY,
            buttonWidth,
            buttonHeight));

        // Load Game Button
        var loadGameButton = new ButtonControl
        {
            Label = "Load Game",
            OnClick = () => LoadGame()
        };
        _buttons.Add(loadGameButton);
        _buttonBounds.Add(new Rectangle(
            centerX - buttonWidth / 2,
            startY + buttonHeight + spacing,
            buttonWidth,
            buttonHeight));

        // Exit Game Button
        var exitGameButton = new ButtonControl
        {
            Label = "Exit Game",
            OnClick = () => ExitGame()
        };
        _buttons.Add(exitGameButton);
        _buttonBounds.Add(new Rectangle(
            centerX - buttonWidth / 2,
            startY + (buttonHeight + spacing) * 2,
            buttonWidth,
            buttonHeight));
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;

        // Check for button clicks
        if (_previousMouseState.LeftButton == ButtonState.Released &&
            currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePos = currentMouseState.Position;
            for (int i = 0; i < _buttonBounds.Count; i++)
            {
                if (_buttonBounds[i].Contains(mousePos))
                {
                    _buttons[i].Click();
                }
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        // Draw title
        uiRenderer.DrawText("Baseball Manager", new Vector2(100, 50), Color.White);

        // Draw buttons with labels
        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            var bounds = _buttonBounds[i];
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
        _startNewFranchiseUseCase.Execute();
        Console.WriteLine("Starting new game...");
        _screenManager.TransitionTo(nameof(FranchiseHubScreen));
    }

    private void LoadGame()
    {
        _loadGameUseCase.Execute();
        Console.WriteLine("Loading game...");
    }

    private void ExitGame()
    {
        Console.WriteLine("Exiting game...");
        System.Environment.Exit(0);
    }
}
