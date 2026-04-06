using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens;
using BaseballManager.Game.Screens.Boot;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game;

public sealed class GameRoot : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private ScreenManager _screenManager = null!;
    private InputManager _inputManager = null!;
    private UiRenderer _uiRenderer = null!;

    public GameRoot()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Assets";
        IsMouseVisible = true;
        Window.Title = "Baseball Manager";
    }

    protected override void Initialize()
    {
        _inputManager = new InputManager();
        _uiRenderer = new UiRenderer(GraphicsDevice);
        _screenManager = new ScreenManager();
        _screenManager.SetInitialScreen(new BootScreen());
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        _inputManager.Update();
        _screenManager.Update(gameTime, _inputManager);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.ForestGreen);
        _screenManager.Draw(gameTime, _uiRenderer);
        base.Draw(gameTime);
    }
}
