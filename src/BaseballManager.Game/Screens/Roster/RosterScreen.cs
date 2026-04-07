using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private string? _sortColumnStandard;
    private string? _sortColumnSeasonStats;
    private string? _sortColumnHiddenRatings;
    private bool _sortAscending = true;
    private readonly Dictionary<string, Rectangle> _headerHitBoxes = new();
    private const int HeaderX = 100;
    private const int HeaderY = 140;
    private const int HeaderHeight = 28;

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
        _sortAscending = true;
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
            else if (currentMouseState.Y >= HeaderY && currentMouseState.Y < HeaderY + HeaderHeight)
            {
                // Handle header click for sorting
                var clickedColumn = GetClickedColumn(currentMouseState.X);
                if (!string.IsNullOrEmpty(clickedColumn))
                {
                    HandleColumnSort(clickedColumn);
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

        // Apply sorting to all rows BEFORE pagination
        var sortedRows = ApplySorting(rosterRows, _viewMode);

        if (sortedRows.Count == 0)
        {
            uiRenderer.DrawText("No roster data found for the selected team.", new Vector2(100, 140), Color.White);
        }
        else
        {
            var pageSize = 14;
            var startIndex = _pageIndex * pageSize;
            var visibleRows = sortedRows.Skip(startIndex).Take(pageSize).ToList();

            if (_viewMode == RosterViewMode.HiddenRatings)
            {
                DrawHiddenRatingsHeader(uiRenderer);
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
                DrawSeasonStatsHeader(uiRenderer);
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
                DrawStandardHeader(uiRenderer);
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

            DrawPagingButtons(uiRenderer, sortedRows.Count, pageSize);
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

    private void DrawStandardHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendStatic(headerBuilder, "ID        ");
        AppendColumn(headerBuilder, uiRenderer, "NAME", 22, RosterViewMode.Standard);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 4, RosterViewMode.Standard);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "SEC", 4, RosterViewMode.Standard);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "AGE", 3, RosterViewMode.Standard);
        AppendStatic(headerBuilder, "   ");
        AppendColumn(headerBuilder, uiRenderer, "LINEUP", 6, RosterViewMode.Standard);
        AppendStatic(headerBuilder, "  ");
        AppendColumn(headerBuilder, uiRenderer, "ROT", 3, RosterViewMode.Standard);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(HeaderX, HeaderY), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawSeasonStatsHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 18, RosterViewMode.SeasonStats);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, RosterViewMode.SeasonStats);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "AVG", 5, RosterViewMode.SeasonStats);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "HR", 3, RosterViewMode.SeasonStats);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "RBI", 3, RosterViewMode.SeasonStats);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "OPS", 5, RosterViewMode.SeasonStats);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "ERA", 5, RosterViewMode.SeasonStats);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "W-L", 5, RosterViewMode.SeasonStats);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(HeaderX, HeaderY), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawHiddenRatingsHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 18, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "OVR", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "TOT", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "CON", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POW", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "DIS", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "SPD", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "FLD", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "ARM", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "PIT", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "DUR", 3, RosterViewMode.HiddenRatings);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(HeaderX, HeaderY), Color.White, uiRenderer.ScoreboardFont);
    }

    private static void AppendStatic(System.Text.StringBuilder builder, string value)
    {
        builder.Append(value);
    }

    private void AppendColumn(System.Text.StringBuilder builder, UiRenderer uiRenderer, string columnName, int width, RosterViewMode mode)
    {
        var prefix = builder.ToString();
        var visibleLabel = GetDisplayColumnLabel(columnName, mode);
        var paddedColumn = visibleLabel.PadRight(width);

        var left = HeaderX + (int)MathF.Floor(MeasureTextWidth(prefix, uiRenderer.ScoreboardFont));
        var labelWidth = (int)MathF.Ceiling(MeasureTextWidth(visibleLabel, uiRenderer.ScoreboardFont));
        _headerHitBoxes[columnName] = new Rectangle(left, HeaderY, Math.Max(1, labelWidth), HeaderHeight);

        builder.Append(paddedColumn);
    }

    private static float MeasureTextWidth(string text, SpriteFont? font)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        return font?.MeasureString(text).X ?? (text.Length * 8f);
    }

    private string GetDisplayColumnLabel(string columnName, RosterViewMode mode)
    {
        var currentSort = mode switch
        {
            RosterViewMode.Standard => _sortColumnStandard,
            RosterViewMode.SeasonStats => _sortColumnSeasonStats,
            RosterViewMode.HiddenRatings => _sortColumnHiddenRatings,
            _ => null
        };

        if (currentSort == columnName)
        {
            var arrow = _sortAscending ? "^" : "v";
            return $"{columnName}{arrow}";
        }

        return columnName;
    }

    private string FormatColumnHeader(string columnName, int width, RosterViewMode mode)
    {
        return GetDisplayColumnLabel(columnName, mode).PadRight(width);
    }

    private string? GetClickedColumn(int mouseX)
    {
        foreach (var (columnName, hitBox) in _headerHitBoxes)
        {
            if (mouseX >= hitBox.Left && mouseX < hitBox.Right)
            {
                return columnName;
            }
        }

        return null;
    }

    private void HandleColumnSort(string columnName)
    {
        var currentSort = _viewMode switch
        {
            RosterViewMode.Standard => _sortColumnStandard,
            RosterViewMode.SeasonStats => _sortColumnSeasonStats,
            RosterViewMode.HiddenRatings => _sortColumnHiddenRatings,
            _ => null
        };

        if (currentSort == columnName)
        {
            // Toggle sort direction
            _sortAscending = !_sortAscending;
        }
        else
        {
            // New column selected, start with ascending
            switch (_viewMode)
            {
                case RosterViewMode.Standard:
                    _sortColumnStandard = columnName;
                    break;
                case RosterViewMode.SeasonStats:
                    _sortColumnSeasonStats = columnName;
                    break;
                case RosterViewMode.HiddenRatings:
                    _sortColumnHiddenRatings = columnName;
                    break;
            }
            _sortAscending = true;
        }

        _pageIndex = 0; // Reset to first page when sorting changes
    }

    private List<RosterDisplayRow> ApplySorting(List<RosterDisplayRow> rows, RosterViewMode mode)
    {
        var currentSort = mode switch
        {
            RosterViewMode.Standard => _sortColumnStandard,
            RosterViewMode.SeasonStats => _sortColumnSeasonStats,
            RosterViewMode.HiddenRatings => _sortColumnHiddenRatings,
            _ => null
        };

        if (string.IsNullOrEmpty(currentSort))
        {
            return rows;
        }

        var sorted = mode switch
        {
            RosterViewMode.Standard => SortStandardRows(rows, currentSort),
            RosterViewMode.SeasonStats => SortSeasonStatsRows(rows, currentSort),
            RosterViewMode.HiddenRatings => SortHiddenRatingsRows(rows, currentSort),
            _ => rows
        };

        return _sortAscending ? sorted : sorted.AsEnumerable().Reverse().ToList();
    }

    private List<RosterDisplayRow> SortStandardRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "SEC" => rows.OrderBy(r => r.SecondaryPosition).ToList(),
            "AGE" => rows.OrderBy(r => r.Age).ToList(),
            "LINEUP" => rows.OrderBy(r => r.LineupSlot ?? 99).ToList(),
            "ROT" => rows.OrderBy(r => r.RotationSlot ?? 99).ToList(),
            _ => rows
        };
    }

    private List<RosterDisplayRow> SortSeasonStatsRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "AVG" => rows.OrderBy(r => r.SeasonStats.AtBats == 0 ? 0 : r.SeasonStats.Hits / (double)r.SeasonStats.AtBats).ToList(),
            "HR" => rows.OrderBy(r => r.SeasonStats.HomeRuns).ToList(),
            "RBI" => rows.OrderBy(r => r.SeasonStats.RunsBattedIn).ToList(),
            "OPS" => rows.OrderBy(r => CalculateOps(r.SeasonStats)).ToList(),
            "ERA" => rows.OrderBy(r => CalculateEra(r.SeasonStats)).ToList(),
            "W-L" => rows.OrderBy(r => r.SeasonStats.Wins).ToList(),
            _ => rows
        };
    }

    private double CalculateOps(PlayerSeasonStatsState stats)
    {
        var obp = stats.PlateAppearances == 0 ? 0d : (stats.Hits + stats.Walks) / (double)stats.PlateAppearances;
        var slg = stats.AtBats == 0 ? 0d : GetTotalBases(stats) / (double)stats.AtBats;
        return obp + slg;
    }

    private double CalculateEra(PlayerSeasonStatsState stats)
    {
        return stats.InningsPitchedOuts == 0 ? 0 : (stats.EarnedRuns * 9d) / (stats.InningsPitchedOuts / 3d);
    }

    private int GetTotalBases(PlayerSeasonStatsState stats)
    {
        var singles = Math.Max(0, stats.Hits - stats.Doubles - stats.Triples - stats.HomeRuns);
        return singles + (stats.Doubles * 2) + (stats.Triples * 3) + (stats.HomeRuns * 4);
    }

    private List<RosterDisplayRow> SortHiddenRatingsRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "OVR" => rows.OrderBy(r => r.HiddenRatings.OverallRating).ToList(),
            "TOT" => rows.OrderBy(r => r.HiddenRatings.AttributeTotal).ToList(),
            "CON" => rows.OrderBy(r => r.HiddenRatings.ContactRating).ToList(),
            "POW" => rows.OrderBy(r => r.HiddenRatings.PowerRating).ToList(),
            "DIS" => rows.OrderBy(r => r.HiddenRatings.DisciplineRating).ToList(),
            "SPD" => rows.OrderBy(r => r.HiddenRatings.SpeedRating).ToList(),
            "FLD" => rows.OrderBy(r => r.HiddenRatings.FieldingRating).ToList(),
            "ARM" => rows.OrderBy(r => r.HiddenRatings.ArmRating).ToList(),
            "PIT" => rows.OrderBy(r => r.HiddenRatings.PitchingRating).ToList(),
            "DUR" => rows.OrderBy(r => r.HiddenRatings.DurabilityRating).ToList(),
            _ => rows
        };
    }

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
