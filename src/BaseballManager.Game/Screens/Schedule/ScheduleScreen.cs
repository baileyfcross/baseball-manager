using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Schedule;

public sealed class ScheduleScreen : GameScreen
{
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly Rectangle _backButtonBounds = new(40, 40, 140, 44);
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private int _pageIndex;

    public ScheduleScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _leagueData = leagueData;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _previousPageButton = new ButtonControl
        {
            Label = "Prev",
            OnClick = () => _pageIndex = Math.Max(0, _pageIndex - 1)
        };
        _nextPageButton = new ButtonControl
        {
            Label = "Next",
            OnClick = () => _pageIndex++
        };
    }

    public override void OnEnter()
    {
        _pageIndex = 0;
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;

        if (_previousMouseState.LeftButton == ButtonState.Released &&
            currentMouseState.LeftButton == ButtonState.Pressed)
        {
            if (_backButtonBounds.Contains(currentMouseState.Position))
            {
                _backButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(currentMouseState.Position))
            {
                _previousPageButton.Click();
            }
            else if (GetNextPageBounds().Contains(currentMouseState.Position))
            {
                var teamGames = GetScheduleRows();
                var maxPage = Math.Max(0, (int)Math.Ceiling(teamGames.Count / 14d) - 1);
                if (_pageIndex < maxPage)
                {
                    _nextPageButton.Click();
                }
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        uiRenderer.DrawText("Schedule", new Vector2(100, 50), Color.White);
        uiRenderer.DrawText(_franchiseSession.SelectedTeamName, new Vector2(100, 90), Color.White);

        var teamGames = GetScheduleRows();
        if (teamGames.Count == 0)
        {
            uiRenderer.DrawText("No schedule data found for the selected team.", new Vector2(100, 140), Color.White);
        }
        else
        {
            uiRenderer.DrawText("DATE         OPPONENT                 SITE  GAME", new Vector2(100, 140), Color.White);
            var pageSize = 14;
            var startIndex = _pageIndex * pageSize;
            var visibleRows = teamGames.Skip(startIndex).Take(pageSize).ToList();
            for (var i = 0; i < visibleRows.Count; i++)
            {
                var row = visibleRows[i];
                var line = string.Format(
                    "{0}   {1,-22} {2,-4} {3,3}",
                    row.Date.ToString("yyyy-MM-dd"),
                    Truncate(row.Opponent, 22),
                    row.Site,
                    row.GameNumber ?? 0);
                uiRenderer.DrawText(line, new Vector2(100, 180 + i * 28), Color.White);
            }

            DrawPagingButtons(uiRenderer, teamGames.Count, pageSize);
        }

        var isHovered = _backButtonBounds.Contains(Mouse.GetState().Position);
        var bgColor = isHovered ? Color.DarkGray : Color.Gray;
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, bgColor, Color.White);
    }

    private void DrawPagingButtons(UiRenderer uiRenderer, int totalRows, int pageSize)
    {
        var maxPage = Math.Max(0, (int)Math.Ceiling(totalRows / (double)pageSize) - 1);
        uiRenderer.DrawText($"Page {_pageIndex + 1} / {maxPage + 1}", new Vector2(100, 600), Color.White);

        var previousBounds = GetPreviousPageBounds();
        var nextBounds = GetNextPageBounds();
        uiRenderer.DrawButton(_previousPageButton.Label, previousBounds, previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, nextBounds, nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private Rectangle GetPreviousPageBounds() => new(100, 640, 120, 40);

    private Rectangle GetNextPageBounds() => new(240, 640, 120, 40);

    private List<ScheduleDisplayRow> GetScheduleRows()
    {
        if (_franchiseSession.SelectedTeam == null)
        {
            return new List<ScheduleDisplayRow>();
        }

        return _leagueData.Schedule
            .Where(game => string.Equals(game.HomeTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(game.AwayTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber)
            .Select(game => new ScheduleDisplayRow(
                game.Date,
                string.Equals(game.HomeTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ? game.AwayTeamName : game.HomeTeamName,
                string.Equals(game.HomeTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ? "HOME" : "AWAY",
                game.GameNumber))
            .ToList();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record ScheduleDisplayRow(DateTime Date, string Opponent, string Site, int? GameNumber);
}
