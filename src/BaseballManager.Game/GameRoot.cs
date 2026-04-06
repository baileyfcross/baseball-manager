using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Game.Screens.Roster;
using BaseballManager.Game.Screens.Schedule;
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
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Content.RootDirectory = "Assets";
        IsMouseVisible = true;
        Window.Title = "Baseball Manager";
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        _inputManager = new InputManager();
        _uiRenderer = new UiRenderer(GraphicsDevice);
        _screenManager = new ScreenManager();

        var mainMenuScreen = new MainMenuScreen(_screenManager);
        _screenManager.Register(mainMenuScreen);
        _screenManager.Register(new FranchiseHubScreen(_screenManager));
        _screenManager.Register(new RosterScreen(_screenManager));
        _screenManager.Register(new LineupScreen(_screenManager));
        _screenManager.Register(new RotationScreen(_screenManager));
        _screenManager.Register(new ScheduleScreen(_screenManager));

        _screenManager.SetInitialScreen(mainMenuScreen);
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
