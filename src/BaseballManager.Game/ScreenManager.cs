using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens;
using Microsoft.Xna.Framework;

namespace BaseballManager.Game;

public sealed class ScreenManager
{
    private readonly Dictionary<string, GameScreen> _screens = new(StringComparer.Ordinal);
    private GameScreen? _current;

    public string CurrentScreenName => _current?.Name ?? string.Empty;

    public void Register(GameScreen screen)
    {
        _screens[screen.Name] = screen;
    }

    public void SetInitialScreen(GameScreen screen)
    {
        Register(screen);
        _current = screen;
        _current.OnEnter();
    }

    public bool TransitionTo(string screenName)
    {
        if (!_screens.TryGetValue(screenName, out var next))
        {
            return false;
        }

        _current?.OnExit();
        _current = next;
        _current.OnEnter();
        return true;
    }

    public void Update(GameTime gameTime, InputManager inputManager)
    {
        _current?.Update(gameTime, inputManager);
    }

    public void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _current?.Draw(gameTime, uiRenderer);
    }
}
