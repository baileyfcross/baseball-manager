using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Roster;

public sealed class RosterScreen : GameScreen
{
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly Rectangle _backButtonBounds = new(40, 40, 140, 44);
    private readonly ButtonControl _toggleViewButton;
    private readonly Rectangle _toggleViewBounds = new(200, 40, 180, 44);
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private RosterViewMode _viewMode;
    private int _pageIndex;

    public RosterScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _leagueData = leagueData;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _toggleViewButton = new ButtonControl
        {
            Label = "Season Stats",
            OnClick = () =>
            {
                _viewMode = _viewMode switch
                {
                    RosterViewMode.Standard => RosterViewMode.SeasonStats,
                    RosterViewMode.SeasonStats => RosterViewMode.HiddenRatings,
                    _ => RosterViewMode.Standard
                };
                _pageIndex = 0;
            }
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
        _viewMode = RosterViewMode.Standard;
        _ignoreClicksUntilRelease = true;
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
            if (_backButtonBounds.Contains(currentMouseState.Position))
            {
                _backButton.Click();
            }
            else if (_toggleViewBounds.Contains(currentMouseState.Position))
            {
                _toggleViewButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(currentMouseState.Position))
            {
                _previousPageButton.Click();
            }
            else if (GetNextPageBounds().Contains(currentMouseState.Position))
            {
                var teamPlayers = GetRosterRows();
                var maxPage = Math.Max(0, (int)Math.Ceiling(teamPlayers.Count / 14d) - 1);
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
        _toggleViewButton.Label = _viewMode switch
        {
            RosterViewMode.Standard => "Season Stats",
            RosterViewMode.SeasonStats => "Hidden Stats",
            _ => "Show Roster"
        };

        var title = _viewMode switch
        {
            RosterViewMode.SeasonStats => "Roster - Season Stats",
            RosterViewMode.HiddenRatings => "Roster - Hidden Stats",
            _ => "Roster"
        };

        uiRenderer.DrawText(title, new Vector2(100, 50), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText(_franchiseSession.SelectedTeamName, new Vector2(100, 90), Color.White);

        var rosterRows = GetRosterRows();
        if (rosterRows.Count == 0)
        {
            uiRenderer.DrawText("No roster data found for the selected team.", new Vector2(100, 140), Color.White);
        }
        else
        {
            var pageSize = 14;
            var startIndex = _pageIndex * pageSize;
            var visibleRows = rosterRows.Skip(startIndex).Take(pageSize).ToList();

            if (_viewMode == RosterViewMode.HiddenRatings)
            {
                uiRenderer.DrawText("NAME               POS OVR TOT CON POW DIS SPD FLD ARM PIT DUR", new Vector2(100, 140), Color.White, uiRenderer.ScoreboardFont);
                for (var i = 0; i < visibleRows.Count; i++)
                {
                    var row = visibleRows[i];
                    var ratings = row.HiddenRatings;
                    var line = string.Format(
                        "{0,-18} {1,-3} {2,3} {3,3} {4,3} {5,3} {6,3} {7,3} {8,3} {9,3} {10,3} {11,3}",
                        Truncate(row.PlayerName, 18),
                        row.PrimaryPosition,
                        ratings.OverallRating,
                        ratings.AttributeTotal,
                        ratings.ContactRating,
                        ratings.PowerRating,
                        ratings.DisciplineRating,
                        ratings.SpeedRating,
                        ratings.FieldingRating,
                        ratings.ArmRating,
                        ratings.PitchingRating,
                        ratings.DurabilityRating);
                    uiRenderer.DrawText(line, new Vector2(100, 180 + i * 28), Color.White, uiRenderer.ScoreboardFont);
                }
            }
            else if (_viewMode == RosterViewMode.SeasonStats)
            {
                uiRenderer.DrawText("NAME               POS  AVG  HR RBI  OPS  ERA  W-L", new Vector2(100, 140), Color.White, uiRenderer.ScoreboardFont);
                for (var i = 0; i < visibleRows.Count; i++)
                {
                    var row = visibleRows[i];
                    var stats = row.SeasonStats;
                    var line = string.Format(
                        "{0,-18} {1,-3} {2,5} {3,3} {4,3} {5,5} {6,5} {7,5}",
                        Truncate(row.PlayerName, 18),
                        row.PrimaryPosition,
                        stats.BattingAverageDisplay,
                        stats.HomeRuns,
                        stats.RunsBattedIn,
                        stats.OpsDisplay,
                        stats.EarnedRunAverageDisplay,
                        stats.WinLossDisplay);
                    uiRenderer.DrawText(line, new Vector2(100, 180 + i * 28), Color.White, uiRenderer.ScoreboardFont);
                }
            }
            else
            {
                uiRenderer.DrawText("ID        NAME                    POS  SEC  AGE  LINEUP  ROT", new Vector2(100, 140), Color.White, uiRenderer.ScoreboardFont);
                for (var i = 0; i < visibleRows.Count; i++)
                {
                    var row = visibleRows[i];
                    var line = string.Format(
                        "{0}  {1,-22} {2,-4} {3,-4} {4,3}   {5,2}      {6,2}",
                        row.PlayerId.ToString("N")[..8],
                        Truncate(row.PlayerName, 22),
                        row.PrimaryPosition,
                        row.SecondaryPosition,
                        row.Age,
                        row.LineupSlot?.ToString() ?? "-",
                        row.RotationSlot?.ToString() ?? "-");
                    uiRenderer.DrawText(line, new Vector2(100, 180 + i * 28), Color.White, uiRenderer.ScoreboardFont);
                }
            }

            DrawPagingButtons(uiRenderer, rosterRows.Count, pageSize);
        }

        var mousePosition = Mouse.GetState().Position;
        var isHovered = _backButtonBounds.Contains(mousePosition);
        var bgColor = isHovered ? Color.DarkGray : Color.Gray;
        var toggleColor = _toggleViewBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray;
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, bgColor, Color.White);
        uiRenderer.DrawButton(_toggleViewButton.Label, _toggleViewBounds, toggleColor, Color.White);
    }

    private void DrawPagingButtons(UiRenderer uiRenderer, int totalRows, int pageSize)
    {
        var maxPage = Math.Max(0, (int)Math.Ceiling(totalRows / (double)pageSize) - 1);
        uiRenderer.DrawText($"Page {_pageIndex + 1} / {maxPage + 1}", new Vector2(100, 600), Color.White, uiRenderer.ScoreboardFont);

        var previousBounds = GetPreviousPageBounds();
        var nextBounds = GetNextPageBounds();
        uiRenderer.DrawButton(_previousPageButton.Label, previousBounds, previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, nextBounds, nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private Rectangle GetPreviousPageBounds() => new(100, 640, 120, 40);

    private Rectangle GetNextPageBounds() => new(240, 640, 120, 40);

    private List<RosterDisplayRow> GetRosterRows()
    {
        return _franchiseSession.GetSelectedTeamRoster()
            .OrderBy(row => row.RotationSlot ?? 99)
            .ThenBy(row => row.LineupSlot ?? 99)
            .ThenBy(row => row.PlayerName)
            .Select(row => new RosterDisplayRow(
                row.PlayerId,
                row.PlayerName,
                row.PrimaryPosition,
                row.SecondaryPosition,
                row.Age,
                row.LineupSlot,
                row.RotationSlot,
                _franchiseSession.GetPlayerRatings(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age),
                _franchiseSession.GetPlayerSeasonStats(row.PlayerId)))
            .ToList();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record RosterDisplayRow(
        Guid PlayerId,
        string PlayerName,
        string PrimaryPosition,
        string SecondaryPosition,
        int Age,
        int? LineupSlot,
        int? RotationSlot,
        PlayerHiddenRatingsState HiddenRatings,
        PlayerSeasonStatsState SeasonStats);

    private enum RosterViewMode
    {
        Standard,
        SeasonStats,
        HiddenRatings
    }
}
