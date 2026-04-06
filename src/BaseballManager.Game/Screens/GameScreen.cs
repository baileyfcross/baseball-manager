using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using Microsoft.Xna.Framework;

namespace BaseballManager.Game.Screens;

public abstract class GameScreen
{
    public virtual string Name => GetType().Name;

    public virtual void OnEnter()
    {
    }

    public virtual void OnExit()
    {
    }

    public virtual void Update(GameTime gameTime, InputManager inputManager)
    {
    }

    public virtual void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
    }
}
