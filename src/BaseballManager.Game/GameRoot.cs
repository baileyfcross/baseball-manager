using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens;
using BaseballManager.Game.Screens.MainMenu;
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

        // Register screens
        _screenManager.Register(new MainMenuScreen(_screenManager));

        // Set initial screen
        _screenManager.SetInitialScreen(new MainMenuScreen(_screenManager));
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _uiRenderer.LoadContent(Content);
        base.LoadContent();
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
