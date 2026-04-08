using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Data;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens;
using BaseballManager.Game.Screens.LiveMatch;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Game.Screens.Options;
using BaseballManager.Game.Screens.Roster;
using BaseballManager.Game.Screens.Schedule;
using BaseballManager.Game.Screens.TeamSelection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game;

public sealed class GameRoot : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private ScreenManager _screenManager = null!;
    private InputManager _inputManager = null!;
    private UiRenderer _uiRenderer = null!;
    private ImportedLeagueData _leagueData = null!;
    private FranchiseSession _franchiseSession = null!;
    private FranchiseStateStore _franchiseStateStore = null!;

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
        _leagueData = new LeagueDataLoader().Load();
        _franchiseStateStore = new FranchiseStateStore();
        _franchiseSession = new FranchiseSession(_leagueData, _franchiseStateStore);

        var displaySettings = _franchiseSession.GetDisplaySettings();
        ApplyDisplaySettings(displaySettings.ScreenWidth, displaySettings.ScreenHeight, displaySettings.RefreshRate, displaySettings.WindowMode);

        var mainMenuScreen = new MainMenuScreen(_screenManager, _franchiseSession);
        _screenManager.Register(mainMenuScreen);
        _screenManager.Register(new TeamSelectionScreen(_screenManager, _leagueData, _franchiseSession));
        _screenManager.Register(new FranchiseHubScreen(_screenManager, _franchiseSession));
        _screenManager.Register(new OptionsScreen(_screenManager, _franchiseSession, this));
        _screenManager.Register(new RosterScreen(_screenManager, _leagueData, _franchiseSession));
        _screenManager.Register(new LineupScreen(_screenManager, _leagueData, _franchiseSession));
        _screenManager.Register(new RotationScreen(_screenManager, _leagueData, _franchiseSession));
        _screenManager.Register(new ScheduleScreen(_screenManager, _leagueData, _franchiseSession));
        _screenManager.Register(new TrainingReportsScreen(_screenManager, _franchiseSession));
        _screenManager.Register(new ScoutingScreen(_screenManager, _franchiseSession));
        _screenManager.Register(new CoachingStaffScreen(_screenManager, _franchiseSession));
        _screenManager.Register(new StandingsScreen(_screenManager, _franchiseSession));
        _screenManager.Register(new FinancesScreen(_screenManager, _franchiseSession));
        _screenManager.Register(new LiveMatchScreen(_screenManager, _leagueData, _franchiseSession));

        _screenManager.SetInitialScreen(mainMenuScreen);
        base.Initialize();
    }

    public void ApplyDisplaySettings(int screenWidth, int screenHeight, int refreshRate, DisplayWindowMode windowMode)
    {
        var safeWidth = Math.Max(800, screenWidth);
        var safeHeight = Math.Max(600, screenHeight);
        var targetRefreshRate = Math.Clamp(refreshRate, 30, 240);
        var desktopMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var windowedWidth = Math.Min(safeWidth, Math.Max(800, desktopMode.Width - 160));
        var windowedHeight = Math.Min(safeHeight, Math.Max(600, desktopMode.Height - 160));

        _graphics.SynchronizeWithVerticalRetrace = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1d / targetRefreshRate);

        if ((_graphics.IsFullScreen || Window.IsBorderless) && windowMode != DisplayWindowMode.Fullscreen)
        {
            _graphics.HardwareModeSwitch = false;
            _graphics.IsFullScreen = false;
            Window.IsBorderless = false;
            _graphics.PreferredBackBufferWidth = windowedWidth;
            _graphics.PreferredBackBufferHeight = windowedHeight;
            _graphics.ApplyChanges();
        }

        Window.IsBorderless = false;
        _graphics.HardwareModeSwitch = windowMode == DisplayWindowMode.Fullscreen;

        switch (windowMode)
        {
            case DisplayWindowMode.Fullscreen:
                _graphics.IsFullScreen = true;
                _graphics.PreferredBackBufferWidth = safeWidth;
                _graphics.PreferredBackBufferHeight = safeHeight;
                break;

            case DisplayWindowMode.BorderlessWindow:
                _graphics.IsFullScreen = false;
                Window.IsBorderless = true;
                _graphics.PreferredBackBufferWidth = desktopMode.Width;
                _graphics.PreferredBackBufferHeight = desktopMode.Height;
                break;

            default:
                _graphics.IsFullScreen = false;
                Window.IsBorderless = false;
                _graphics.PreferredBackBufferWidth = windowedWidth;
                _graphics.PreferredBackBufferHeight = windowedHeight;
                break;
        }

        _graphics.ApplyChanges();

        if (windowMode == DisplayWindowMode.BorderlessWindow)
        {
            Window.Position = Point.Zero;
        }
        else if (windowMode == DisplayWindowMode.Windowed)
        {
            CenterWindowOnDesktop(windowedWidth, windowedHeight, desktopMode.Width, desktopMode.Height);
        }
    }

    private void CenterWindowOnDesktop(int windowWidth, int windowHeight, int desktopWidth, int desktopHeight)
    {
        var centeredX = Math.Max(0, (desktopWidth - windowWidth) / 2);
        var centeredY = Math.Max(0, (desktopHeight - windowHeight) / 2);
        Window.Position = new Point(centeredX, centeredY);
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
        var backgroundColor = _franchiseSession.SelectedTeam != null
            ? TeamColorPalette.GetBackgroundColor(_franchiseSession.SelectedTeam.HexColor, Color.ForestGreen)
            : Color.ForestGreen;

        GraphicsDevice.Clear(backgroundColor);
        _screenManager.Draw(gameTime, _uiRenderer);
        DrawTopRightDateTime();
        base.Draw(gameTime);
    }

    private void DrawTopRightDateTime()
    {
        var displaySettings = _franchiseSession.GetDisplaySettings();
        var franchiseDateText = $"Franchise: {_franchiseSession.GetCurrentFranchiseDate():yyyy-MM-dd}";
        var clockText = DateTime.Now.ToString("hh:mm:ss tt");
        var combined = displaySettings.ShowRealTimeClock
            ? $"{franchiseDateText}   {clockText}"
            : franchiseDateText;

        var font = _uiRenderer.ScoreboardFont;
        var textWidth = font?.MeasureString(combined).X ?? (combined.Length * 8f);
        var x = _uiRenderer.Viewport.Width - textWidth - 24f;
        _uiRenderer.DrawText(combined, new Vector2(Math.Max(12f, x), 12f), Color.White, font);
    }
}
