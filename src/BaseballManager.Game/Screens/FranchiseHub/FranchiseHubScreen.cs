using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.Roster;
using BaseballManager.Game.Screens.Schedule;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class FranchiseHubScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly List<ButtonControl> _buttons = new();
    private readonly List<Rectangle> _buttonBounds = new();
    private MouseState _previousMouseState = default;

    public FranchiseHubScreen(ScreenManager screenManager)
    {
        _screenManager = screenManager;
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        var centerX = 640;
        var startY = 200;
        const int buttonWidth = 200;
        const int buttonHeight = 50;
        const int spacing = 20;

        AddButton("Roster", nameof(RosterScreen), centerX, startY, buttonWidth, buttonHeight);
        AddButton("Lineup", nameof(LineupScreen), centerX, startY + (buttonHeight + spacing), buttonWidth, buttonHeight);
        AddButton("Rotation", nameof(RotationScreen), centerX, startY + (buttonHeight + spacing) * 2, buttonWidth, buttonHeight);
        AddButton("Schedule", nameof(ScheduleScreen), centerX, startY + (buttonHeight + spacing) * 3, buttonWidth, buttonHeight);
    }

    private void AddButton(string label, string screenName, int centerX, int y, int width, int height)
    {
        _buttons.Add(new ButtonControl
        {
            Label = label,
            OnClick = () => _screenManager.TransitionTo(screenName)
        });

        _buttonBounds.Add(new Rectangle(centerX - width / 2, y, width, height));
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;

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
        uiRenderer.DrawText("Franchise Hub", new Vector2(100, 50), Color.White);

        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            var bounds = _buttonBounds[i];
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var bgColor = isHovered ? Color.DarkGray : Color.Gray;

            uiRenderer.DrawButton(button.Label, bounds, bgColor, Color.White);
        }
    }
}
