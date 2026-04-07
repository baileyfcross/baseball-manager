using BaseballManager.Application.Franchise;
using BaseballManager.Contracts.ImportDtos;
using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.TeamSelection;

public sealed class TeamSelectionScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseSession _franchiseSession;
    private readonly StartNewFranchiseUseCase _startNewFranchiseUseCase = new();
    private readonly ButtonControl _backButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);

    public TeamSelectionScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _leagueData = leagueData;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(MainMenuScreen))
        };
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

        if (_previousMouseState.LeftButton == ButtonState.Released &&
            currentMouseState.LeftButton == ButtonState.Pressed)
        {
            if (GetBackButtonBounds().Contains(currentMouseState.Position))
            {
                _backButton.Click();
            }
            else
            {
                var team = GetTeamAtPosition(currentMouseState.Position);
                if (team != null)
                {
                    _franchiseSession.SelectTeam(team);
                    _startNewFranchiseUseCase.Execute();
                    _screenManager.TransitionTo(nameof(FranchiseHubScreen));
                }
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        uiRenderer.DrawText("Select A Team", new Vector2(100, 50), Color.White, uiRenderer.UiMediumFont);

        if (!_leagueData.HasData)
        {
            uiRenderer.DrawText("No imported team data found in data/imports/generated.", new Vector2(100, 120), Color.White);
            DrawBackButton(uiRenderer);
            return;
        }

        uiRenderer.DrawText($"Available Teams: {_leagueData.Teams.Count}", new Vector2(100, 100), Color.White);

        for (var i = 0; i < _leagueData.Teams.Count; i++)
        {
            var bounds = GetTeamButtonBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var bgColor = isHovered ? Color.DarkSlateBlue : Color.SlateGray;
            var team = _leagueData.Teams[i];
            uiRenderer.DrawButton($"{team.Abbreviation}  {team.Name}", bounds, bgColor, Color.White);
        }

        DrawBackButton(uiRenderer);
    }

    private void DrawBackButton(UiRenderer uiRenderer)
    {
        var bounds = GetBackButtonBounds();
        var isHovered = bounds.Contains(Mouse.GetState().Position);
        uiRenderer.DrawButton(_backButton.Label, bounds, isHovered ? Color.DarkGray : Color.Gray, Color.White);
    }

    private TeamImportDto? GetTeamAtPosition(Point position)
    {
        for (var i = 0; i < _leagueData.Teams.Count; i++)
        {
            if (GetTeamButtonBounds(i).Contains(position))
            {
                return _leagueData.Teams[i];
            }
        }

        return null;
    }

    private Rectangle GetBackButtonBounds() => new(40, _viewport.Y - 70, 140, 44);

    private Rectangle GetTeamButtonBounds(int index)
    {
        const int top = 140;
        const int bottomMargin = 90;
        const int buttonHeight = 36;
        const int rowSpacing = 8;
        const int columnSpacing = 20;
        const int leftMargin = 100;
        const int rightMargin = 100;

        var rowsPerColumn = Math.Max(1, (_viewport.Y - top - bottomMargin) / (buttonHeight + rowSpacing));
        var column = index / rowsPerColumn;
        var row = index % rowsPerColumn;
        var columnCount = Math.Max(1, (int)Math.Ceiling(_leagueData.Teams.Count / (double)rowsPerColumn));
        var availableWidth = _viewport.X - leftMargin - rightMargin - (columnSpacing * Math.Max(0, columnCount - 1));
        var buttonWidth = Math.Max(220, availableWidth / columnCount);

        var x = leftMargin + column * (buttonWidth + columnSpacing);
        var y = top + row * (buttonHeight + rowSpacing);
        return new Rectangle(x, y, buttonWidth, buttonHeight);
    }
}
