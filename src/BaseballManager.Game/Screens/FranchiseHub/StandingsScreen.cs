using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class StandingsScreen : GameScreen
{
    private static readonly (string Key, string Label, int Width, bool Center)[] Columns =
    [
        ("TEAM", "TEAM", 220, false),
        ("LG", "LG", 40, true),
        ("DIV", "DIV", 92, false),
        ("W", "W", 34, true),
        ("L", "L", 34, true),
        ("PCT", "PCT", 52, true),
        ("STRK", "STRK", 50, true)
    ];

    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private readonly Dictionary<string, Rectangle> _headerHitBoxes = new();
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private int _pageIndex;
    private string? _sortColumn;
    private bool _sortAscending = true;

    public StandingsScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _previousPageButton = new ButtonControl { Label = "Prev" };
        _nextPageButton = new ButtonControl { Label = "Next" };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _pageIndex = 0;
        _sortColumn ??= "W";
        _sortAscending = false;
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

        if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePosition = currentMouseState.Position;
            if (GetBackButtonBounds().Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(mousePosition))
            {
                _pageIndex = Math.Max(0, _pageIndex - 1);
            }
            else if (GetNextPageBounds().Contains(mousePosition))
            {
                var standings = GetSortedStandings();
                var maxPage = Math.Max(0, (int)Math.Ceiling(standings.Count / (double)GetPageSize()) - 1);
                _pageIndex = Math.Min(maxPage, _pageIndex + 1);
            }
            else if (TryGetClickedHeader(mousePosition, out var columnKey))
            {
                if (_sortColumn == columnKey)
                {
                    _sortAscending = !_sortAscending;
                }
                else
                {
                    _sortColumn = columnKey;
                    _sortAscending = columnKey is "TEAM" or "LG" or "DIV";
                }

                _pageIndex = 0;
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);
        var standings = GetSortedStandings();
        var mousePosition = Mouse.GetState().Position;

        uiRenderer.DrawText("League Standings", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"Current Team: {_franchiseSession.SelectedTeamName}", new Rectangle(168, 82, Math.Max(360, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds("Every club is listed here with its overall record and current win/loss streak. Click a column header to sort it.", new Rectangle(48, 112, Math.Max(620, _viewport.X - 96), 40), Color.White, uiRenderer.UiSmallFont, 2);

        var panelBounds = GetPanelBounds();
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(38, 48, 56), Color.White);

        DrawHeader(uiRenderer, mousePosition);
        DrawStandingsRows(uiRenderer, standings);
        DrawPaging(uiRenderer, standings.Count, mousePosition);

        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private void DrawHeader(UiRenderer uiRenderer, Point mousePosition)
    {
        _headerHitBoxes.Clear();
        for (var columnIndex = 0; columnIndex < Columns.Length; columnIndex++)
        {
            var column = Columns[columnIndex];
            var bounds = GetColumnBounds(-1, columnIndex);
            _headerHitBoxes[column.Key] = bounds;
            var display = _sortColumn == column.Key ? $"{column.Label}{(_sortAscending ? "^" : "v")}" : column.Label;
            uiRenderer.DrawTextInBounds(display, bounds, bounds.Contains(mousePosition) ? Color.White : Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: column.Center);
        }
    }

    private void DrawStandingsRows(UiRenderer uiRenderer, IReadOnlyList<TeamStandingView> standings)
    {
        if (standings.Count == 0)
        {
            uiRenderer.DrawTextInBounds("No standings are available yet.", new Rectangle(GetPanelBounds().X + 12, GetPanelBounds().Y + 48, GetPanelBounds().Width - 24, 20), Color.White, uiRenderer.UiSmallFont);
            return;
        }

        var startIndex = _pageIndex * GetPageSize();
        var visibleRows = standings.Skip(startIndex).Take(GetPageSize()).ToList();

        for (var i = 0; i < visibleRows.Count; i++)
        {
            var row = visibleRows[i];
            var rowBounds = GetRowBounds(i);
            var isSelectedTeam = string.Equals(row.TeamName, _franchiseSession.SelectedTeamName, StringComparison.OrdinalIgnoreCase);
            uiRenderer.DrawButton(string.Empty, rowBounds, isSelectedTeam ? Color.DarkOliveGreen : new Color(54, 62, 70), Color.White);

            DrawRowCell(uiRenderer, 0, i, Truncate($"{row.TeamAbbreviation} {row.TeamName}", 22), false);
            DrawRowCell(uiRenderer, 1, i, Truncate(row.League, 4), true);
            DrawRowCell(uiRenderer, 2, i, Truncate(row.Division, 10), false);
            DrawRowCell(uiRenderer, 3, i, row.Wins.ToString(), true);
            DrawRowCell(uiRenderer, 4, i, row.Losses.ToString(), true);
            DrawRowCell(uiRenderer, 5, i, row.WinningPercentage, true);
            DrawRowCell(uiRenderer, 6, i, row.Streak, true);
        }
    }

    private void DrawRowCell(UiRenderer uiRenderer, int columnIndex, int rowIndex, string value, bool center)
    {
        var bounds = GetColumnBounds(rowIndex, columnIndex);
        uiRenderer.DrawTextInBounds(value, bounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: center);
    }

    private void DrawPaging(UiRenderer uiRenderer, int totalRows, Point mousePosition)
    {
        var pageSize = GetPageSize();
        var maxPage = Math.Max(0, (int)Math.Ceiling(totalRows / (double)pageSize) - 1);
        uiRenderer.DrawTextInBounds($"Page {_pageIndex + 1} / {maxPage + 1}", new Rectangle(GetPanelBounds().X + 10, GetPreviousPageBounds().Y - 20, 160, 18), Color.White, uiRenderer.UiSmallFont);

        var previousBounds = GetPreviousPageBounds();
        var nextBounds = GetNextPageBounds();
        uiRenderer.DrawButton(_previousPageButton.Label, previousBounds, previousBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawButton(_nextPageButton.Label, nextBounds, nextBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White, uiRenderer.UiSmallFont);
    }

    private List<TeamStandingView> GetSortedStandings()
    {
        var standings = _franchiseSession.GetStandings().ToList();
        if (string.IsNullOrWhiteSpace(_sortColumn))
        {
            return standings;
        }

        var sorted = _sortColumn switch
        {
            "TEAM" => standings.OrderBy(team => team.TeamName).ThenBy(team => team.TeamAbbreviation).ToList(),
            "LG" => standings.OrderBy(team => team.League).ThenBy(team => team.Division).ThenByDescending(team => team.Wins).ToList(),
            "DIV" => standings.OrderBy(team => team.Division).ThenByDescending(team => team.Wins).ThenBy(team => team.TeamName).ToList(),
            "W" => standings.OrderBy(team => team.Wins).ThenBy(team => team.Losses).ToList(),
            "L" => standings.OrderBy(team => team.Losses).ThenByDescending(team => team.Wins).ToList(),
            "PCT" => standings.OrderBy(team => team.WinningPercentage).ThenByDescending(team => team.Wins).ToList(),
            "STRK" => standings.OrderBy(team => ParseStreakValue(team.Streak)).ThenByDescending(team => team.Wins).ToList(),
            _ => standings
        };

        return _sortAscending ? sorted : sorted.AsEnumerable().Reverse().ToList();
    }

    private bool TryGetClickedHeader(Point mousePosition, out string columnKey)
    {
        foreach (var (key, bounds) in _headerHitBoxes)
        {
            if (bounds.Contains(mousePosition))
            {
                columnKey = key;
                return true;
            }
        }

        columnKey = string.Empty;
        return false;
    }

    private static int ParseStreakValue(string streak)
    {
        if (string.IsNullOrWhiteSpace(streak) || streak == "-")
        {
            return 0;
        }

        var isWinStreak = streak.StartsWith("W", StringComparison.OrdinalIgnoreCase);
        var valueText = streak.Length > 1 ? streak[1..] : "0";
        return int.TryParse(valueText, out var value)
            ? (isWinStreak ? value : -value)
            : 0;
    }

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private Rectangle GetPanelBounds() => new(48, 160, Math.Max(640, _viewport.X - 96), Math.Max(360, _viewport.Y - 240));

    private int GetPageSize() => Math.Max(8, (GetPanelBounds().Height - 76) / 30);

    private Rectangle GetRowBounds(int index)
    {
        var panelBounds = GetPanelBounds();
        return new Rectangle(panelBounds.X + 10, panelBounds.Y + 36 + (index * 30), panelBounds.Width - 20, 24);
    }

    private Rectangle GetColumnBounds(int rowIndex, int columnIndex)
    {
        var panelBounds = GetPanelBounds();
        var x = panelBounds.X + 14;
        for (var i = 0; i < columnIndex; i++)
        {
            x += Columns[i].Width + 8;
        }

        var y = rowIndex < 0
            ? panelBounds.Y + 10
            : GetRowBounds(rowIndex).Y + 3;

        return new Rectangle(x, y, Columns[columnIndex].Width, 18);
    }

    private Rectangle GetPreviousPageBounds()
    {
        var panelBounds = GetPanelBounds();
        return new Rectangle(panelBounds.X + 10, panelBounds.Bottom + 10, 120, 36);
    }

    private Rectangle GetNextPageBounds()
    {
        var previousBounds = GetPreviousPageBounds();
        return new Rectangle(previousBounds.Right + 12, previousBounds.Y, 120, 36);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
