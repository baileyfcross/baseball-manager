using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Input;

public sealed class InputManager
{
    public KeyboardState KeyboardState { get; private set; }
    public MouseState MouseState { get; private set; }

    public void Update()
    {
        KeyboardState = Keyboard.GetState();
        MouseState = Mouse.GetState();
    }
}
