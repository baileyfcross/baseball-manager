using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Schedule;

public sealed class ScheduleScreen : GameScreen
{
    private readonly ButtonControl _backButton;
    private readonly Rectangle _backButtonBounds = new(40, 40, 140, 44);
    private MouseState _previousMouseState = default;

    public ScheduleScreen(ScreenManager screenManager)
    {
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;

        if (_previousMouseState.LeftButton == ButtonState.Released &&
            currentMouseState.LeftButton == ButtonState.Pressed &&
            _backButtonBounds.Contains(currentMouseState.Position))
        {
            _backButton.Click();
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        uiRenderer.DrawText("Schedule", new Vector2(100, 50), Color.White);
        uiRenderer.DrawText("Season schedule screen", new Vector2(100, 130), Color.White);

        var isHovered = _backButtonBounds.Contains(Mouse.GetState().Position);
        var bgColor = isHovered ? Color.DarkGray : Color.Gray;
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, bgColor, Color.White);
    }
}
