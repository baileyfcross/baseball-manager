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

    private const int ButtonWidth = 220;
    private const int ButtonSpacing = 18;

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
        var panelBounds = GetMenuPanelBounds(viewportWidth, viewportHeight);
        var buttonHeight = GetButtonHeight(viewportHeight);
        var width = _buttons.Count == 0 ? ButtonWidth : Math.Min(panelBounds.Width - 24, _buttons.Max(button => ButtonControl.GetSuggestedWidth(button.Label, ButtonWidth)));
        var x = panelBounds.X + ((panelBounds.Width - width) / 2);
        var y = panelBounds.Y + 18 + index * (buttonHeight + ButtonSpacing);
        return new Rectangle(x, y, width, buttonHeight);
    }

    private Rectangle GetMenuPanelBounds(int viewportWidth, int viewportHeight)
    {
        var buttonHeight = GetButtonHeight(viewportHeight);
        var totalHeight = (_buttons.Count * buttonHeight) + (Math.Max(0, _buttons.Count - 1) * ButtonSpacing) + 36;
        var width = Math.Clamp(viewportWidth / 3, 320, 460);
        var x = (viewportWidth - width) / 2;
        var y = Math.Max(140, (viewportHeight - totalHeight) / 2);
        return new Rectangle(x, y, width, totalHeight);
    }

    private static int GetButtonHeight(int viewportHeight)
    {
        return Math.Clamp(viewportHeight / 13, 44, 58);
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

        uiRenderer.DrawText("Baseball Manager", new Vector2(56, 42), Color.White, uiRenderer.UiMediumFont);

        if (_franchiseSession.HasFranchiseSaveData)
        {
            uiRenderer.DrawTextInBounds($"Current Franchise: {_franchiseSession.SelectedTeamName}", new Rectangle(56, 82, Math.Max(320, _viewport.X - 112), 28), Color.White, uiRenderer.UiSmallFont);
        }

        var menuPanelBounds = GetMenuPanelBounds(_viewport.X, _viewport.Y);
        uiRenderer.DrawButton(string.Empty, menuPanelBounds, new Color(38, 48, 56), Color.White);

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
