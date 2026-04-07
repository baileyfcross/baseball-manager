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
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _toggleViewButton;
    private readonly ButtonControl _filterButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private RosterViewMode _viewMode;
    private RosterFilterMode _filterMode;
    private bool _showFilterDropdown;
    private int _pageIndex;
    private string? _sortColumnStandard;
    private string? _sortColumnSeasonStats;
    private string? _sortColumnLastSeasonStats;
    private string? _sortColumnScoutNotes;
    private string? _sortColumnHiddenRatings;
    private bool _sortAscending = true;
    private readonly Dictionary<string, Rectangle> _headerHitBoxes = new();
    private Point _viewport = new(1280, 720);
    private const int HeaderY = 160;
    private const int HeaderHeight = 28;
    private const int PageSize = 14;

    public RosterScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
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
                    RosterViewMode.SeasonStats => RosterViewMode.LastSeasonStats,
                    RosterViewMode.LastSeasonStats => RosterViewMode.ScoutNotes,
                    RosterViewMode.ScoutNotes => RosterViewMode.HiddenRatings,
                    _ => RosterViewMode.Standard
                };
                _pageIndex = 0;
            }
        };
        _filterButton = new ButtonControl
        {
            Label = "Filter: All Players",
            OnClick = () => _showFilterDropdown = !_showFilterDropdown
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
        _filterMode = RosterFilterMode.All;
        _showFilterDropdown = false;
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
            if (GetBackButtonBounds().Contains(currentMouseState.Position))
            {
                _backButton.Click();
            }
            else if (GetToggleViewBounds().Contains(currentMouseState.Position))
            {
                _toggleViewButton.Click();
            }
            else if (GetFilterButtonBounds().Contains(currentMouseState.Position))
            {
                _filterButton.Click();
            }
            else if (_showFilterDropdown)
            {
                TrySelectFilterOption(currentMouseState.Position);
            }
            else if (GetPreviousPageBounds().Contains(currentMouseState.Position))
            {
                _previousPageButton.Click();
            }
            else if (GetNextPageBounds().Contains(currentMouseState.Position))
            {
                var teamPlayers = ApplySorting(ApplyRosterFilter(GetRosterRows()), _viewMode);
                var maxPage = Math.Max(0, (int)Math.Ceiling(teamPlayers.Count / (double)PageSize) - 1);
                if (_pageIndex < maxPage)
                {
                    _nextPageButton.Click();
                }
            }
            else if (currentMouseState.Y >= GetHeaderY() && currentMouseState.Y < GetHeaderY() + HeaderHeight)
            {
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
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        _toggleViewButton.Label = _viewMode switch
        {
            RosterViewMode.Standard => "Season Stats",
            RosterViewMode.SeasonStats => "Last Season",
            RosterViewMode.LastSeasonStats => "Scout Notes",
            RosterViewMode.ScoutNotes => "Secret Attributes",
            _ => "Show Roster"
        };
        _filterButton.Label = _showFilterDropdown
            ? $"Filter: {GetFilterLabel(_filterMode)} ^"
            : $"Filter: {GetFilterLabel(_filterMode)} v";

        var title = _viewMode switch
        {
            RosterViewMode.SeasonStats => "Roster - Season Stats",
            RosterViewMode.LastSeasonStats => "Roster - Last Season",
            RosterViewMode.ScoutNotes => "Roster - Scout Notes",
            RosterViewMode.HiddenRatings => "Roster - Secret Attributes",
            _ => "Roster"
        };

        uiRenderer.DrawText(title, new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Active Filter: {GetFilterDescription(_filterMode)}", new Rectangle(168, 110, Math.Max(420, _viewport.X - 220), 20), Color.White, uiRenderer.ScoreboardFont);

        var rosterRows = GetRosterRows();
        var filteredRows = ApplyRosterFilter(rosterRows);
        var sortedRows = ApplySorting(filteredRows, _viewMode);

        if (sortedRows.Count == 0)
        {
            uiRenderer.DrawText("No roster data found for the selected team with the current filter.", new Vector2(GetHeaderX(), GetHeaderY() + 20), Color.White);
        }
        else
        {
            var startIndex = _pageIndex * PageSize;
            var visibleRows = sortedRows.Skip(startIndex).Take(PageSize).ToList();

            switch (_viewMode)
            {
                case RosterViewMode.SeasonStats:
                    DrawSeasonStatsHeader(uiRenderer, RosterViewMode.SeasonStats);
                    DrawSeasonStatsRows(uiRenderer, visibleRows, useLastSeasonStats: false);
                    break;

                case RosterViewMode.LastSeasonStats:
                    DrawSeasonStatsHeader(uiRenderer, RosterViewMode.LastSeasonStats);
                    DrawSeasonStatsRows(uiRenderer, visibleRows, useLastSeasonStats: true);
                    break;

                case RosterViewMode.ScoutNotes:
                    DrawScoutNotesHeader(uiRenderer);
                    for (var i = 0; i < visibleRows.Count; i++)
                    {
                        var row = visibleRows[i];
                        var note = Truncate(_franchiseSession.GetQuickScoutNote(row.PlayerId), 70);
                        var line = string.Format(
                            "{0,-18} {1,-3} {2}",
                            Truncate(row.PlayerName, 18),
                            row.PrimaryPosition,
                            note);
                        uiRenderer.DrawText(line, new Vector2(GetHeaderX(), GetRowY(i)), Color.White, uiRenderer.ScoreboardFont);
                    }
                    break;

                case RosterViewMode.HiddenRatings:
                    DrawHiddenRatingsHeader(uiRenderer);
                    for (var i = 0; i < visibleRows.Count; i++)
                    {
                        var row = visibleRows[i];
                        var ratings = row.HiddenRatings;
                        var line = string.Format(
                            "{0,-16} {1,-3} {2,3} {3,3} {4,3} {5,3} {6,3} {7,3} {8,3} {9,3} {10,3} {11,3}",
                            Truncate(row.PlayerName, 16),
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
                        uiRenderer.DrawText(line, new Vector2(GetHeaderX(), GetRowY(i)), Color.White, uiRenderer.ScoreboardFont);
                    }
                    break;

                default:
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
                        uiRenderer.DrawText(line, new Vector2(GetHeaderX(), GetRowY(i)), Color.White, uiRenderer.ScoreboardFont);
                    }
                    break;
            }

            DrawPagingButtons(uiRenderer, sortedRows.Count, PageSize);
        }

        var mousePosition = Mouse.GetState().Position;
        var backBounds = GetBackButtonBounds();
        var toggleBounds = GetToggleViewBounds();
        var filterBounds = GetFilterButtonBounds();
        uiRenderer.DrawButton(_backButton.Label, backBounds, backBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_toggleViewButton.Label, toggleBounds, toggleBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_filterButton.Label, filterBounds, filterBounds.Contains(mousePosition) || _showFilterDropdown ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);

        if (_showFilterDropdown)
        {
            DrawFilterDropdown(uiRenderer);
        }
    }

    private void DrawSeasonStatsRows(UiRenderer uiRenderer, IReadOnlyList<RosterDisplayRow> rows, bool useLastSeasonStats)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var stats = useLastSeasonStats ? row.LastSeasonStats : row.SeasonStats;
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
            uiRenderer.DrawText(line, new Vector2(GetHeaderX(), GetRowY(i)), Color.White, uiRenderer.ScoreboardFont);
        }
    }

    private void DrawPagingButtons(UiRenderer uiRenderer, int totalRows, int pageSize)
    {
        var maxPage = Math.Max(0, (int)Math.Ceiling(totalRows / (double)pageSize) - 1);
        uiRenderer.DrawText($"Page {_pageIndex + 1} / {maxPage + 1}", new Vector2(GetHeaderX(), GetPreviousPageBounds().Y - 26), Color.White, uiRenderer.ScoreboardFont);

        var previousBounds = GetPreviousPageBounds();
        var nextBounds = GetNextPageBounds();
        uiRenderer.DrawButton(_previousPageButton.Label, previousBounds, previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, nextBounds, nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private int GetHeaderX() => Math.Max(40, (_viewport.X - 1080) / 2);

    private int GetHeaderY() => Math.Max(HeaderY, (_viewport.Y - 560) / 2);

    private int GetRowY(int index) => GetHeaderY() + 40 + (index * 28);

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private Rectangle GetToggleViewBounds() => new(_viewport.X - 270, 40, 210, 44);

    private Rectangle GetFilterButtonBounds() => new(_viewport.X - 520, 40, 230, 44);

    private Rectangle GetPreviousPageBounds() => new(GetHeaderX(), _viewport.Y - 76, 120, 40);

    private Rectangle GetNextPageBounds() => new(GetHeaderX() + 140, _viewport.Y - 76, 120, 40);

    private void DrawFilterDropdown(UiRenderer uiRenderer)
    {
        var options = GetFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var bounds = GetFilterOptionBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = option == _filterMode;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton(GetFilterLabel(option), bounds, color, Color.White, uiRenderer.ScoreboardFont);
        }
    }

    private bool TrySelectFilterOption(Point mousePosition)
    {
        var options = GetFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            if (!GetFilterOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _filterMode = options[i];
            _pageIndex = 0;
            _showFilterDropdown = false;
            return true;
        }

        _showFilterDropdown = false;
        return false;
    }

    private Rectangle GetFilterOptionBounds(int index)
    {
        var buttonBounds = GetFilterButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Bottom + 4 + (index * 32), buttonBounds.Width, 28);
    }

    private static IReadOnlyList<RosterFilterMode> GetFilterOptions()
    {
        return
        [
            RosterFilterMode.All,
            RosterFilterMode.Hitters,
            RosterFilterMode.Pitchers,
            RosterFilterMode.Contact,
            RosterFilterMode.Power,
            RosterFilterMode.Speed,
            RosterFilterMode.Fielding,
            RosterFilterMode.Pitching,
            RosterFilterMode.CurrentOps,
            RosterFilterMode.CurrentEra,
            RosterFilterMode.LastOps,
            RosterFilterMode.LastEra
        ];
    }

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

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(GetHeaderX(), GetHeaderY()), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawSeasonStatsHeader(UiRenderer uiRenderer, RosterViewMode mode)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 18, mode);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, mode);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "AVG", 5, mode);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "HR", 3, mode);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "RBI", 3, mode);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "OPS", 5, mode);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "ERA", 5, mode);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "W-L", 5, mode);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(GetHeaderX(), GetHeaderY()), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawScoutNotesHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 18, RosterViewMode.ScoutNotes);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, RosterViewMode.ScoutNotes);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "REPORT", 42, RosterViewMode.ScoutNotes);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(GetHeaderX(), GetHeaderY()), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawHiddenRatingsHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 16, RosterViewMode.HiddenRatings);
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

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(GetHeaderX(), GetHeaderY()), Color.White, uiRenderer.ScoreboardFont);
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

        var left = GetHeaderX() + (int)MathF.Floor(MeasureTextWidth(prefix, uiRenderer.ScoreboardFont));
        var labelWidth = (int)MathF.Ceiling(MeasureTextWidth(visibleLabel, uiRenderer.ScoreboardFont));
        _headerHitBoxes[columnName] = new Rectangle(left, GetHeaderY(), Math.Max(1, labelWidth), HeaderHeight);

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
        var currentSort = GetCurrentSortColumn(mode);
        if (currentSort == columnName)
        {
            var arrow = _sortAscending ? "^" : "v";
            return $"{columnName}{arrow}";
        }

        return columnName;
    }

    private string? GetCurrentSortColumn(RosterViewMode mode)
    {
        return mode switch
        {
            RosterViewMode.Standard => _sortColumnStandard,
            RosterViewMode.SeasonStats => _sortColumnSeasonStats,
            RosterViewMode.LastSeasonStats => _sortColumnLastSeasonStats,
            RosterViewMode.ScoutNotes => _sortColumnScoutNotes,
            RosterViewMode.HiddenRatings => _sortColumnHiddenRatings,
            _ => null
        };
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
        var currentSort = GetCurrentSortColumn(_viewMode);

        if (currentSort == columnName)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            switch (_viewMode)
            {
                case RosterViewMode.Standard:
                    _sortColumnStandard = columnName;
                    break;
                case RosterViewMode.SeasonStats:
                    _sortColumnSeasonStats = columnName;
                    break;
                case RosterViewMode.LastSeasonStats:
                    _sortColumnLastSeasonStats = columnName;
                    break;
                case RosterViewMode.ScoutNotes:
                    _sortColumnScoutNotes = columnName;
                    break;
                case RosterViewMode.HiddenRatings:
                    _sortColumnHiddenRatings = columnName;
                    break;
            }

            _sortAscending = true;
        }

        _pageIndex = 0;
    }

    private List<RosterDisplayRow> ApplySorting(List<RosterDisplayRow> rows, RosterViewMode mode)
    {
        var currentSort = GetCurrentSortColumn(mode);
        if (string.IsNullOrEmpty(currentSort))
        {
            return ApplyDefaultFilterSort(rows);
        }

        var sorted = mode switch
        {
            RosterViewMode.Standard => SortStandardRows(rows, currentSort),
            RosterViewMode.SeasonStats => SortSeasonStatsRows(rows, currentSort, useLastSeasonStats: false),
            RosterViewMode.LastSeasonStats => SortSeasonStatsRows(rows, currentSort, useLastSeasonStats: true),
            RosterViewMode.ScoutNotes => SortScoutNoteRows(rows, currentSort),
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

    private List<RosterDisplayRow> SortSeasonStatsRows(List<RosterDisplayRow> rows, string columnName, bool useLastSeasonStats)
    {
        Func<RosterDisplayRow, PlayerSeasonStatsState> statsSelector = row => useLastSeasonStats ? row.LastSeasonStats : row.SeasonStats;

        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "AVG" => rows.OrderBy(r => statsSelector(r).AtBats == 0 ? 0 : statsSelector(r).Hits / (double)statsSelector(r).AtBats).ToList(),
            "HR" => rows.OrderBy(r => statsSelector(r).HomeRuns).ToList(),
            "RBI" => rows.OrderBy(r => statsSelector(r).RunsBattedIn).ToList(),
            "OPS" => rows.OrderBy(r => CalculateOps(statsSelector(r))).ToList(),
            "ERA" => rows.OrderBy(r => CalculateEra(statsSelector(r))).ToList(),
            "W-L" => rows.OrderBy(r => statsSelector(r).Wins).ToList(),
            _ => rows
        };
    }

    private static double CalculateOps(PlayerSeasonStatsState stats)
    {
        var obp = stats.PlateAppearances == 0 ? 0d : (stats.Hits + stats.Walks) / (double)stats.PlateAppearances;
        var slg = stats.AtBats == 0 ? 0d : GetTotalBases(stats) / (double)stats.AtBats;
        return obp + slg;
    }

    private static double CalculateEra(PlayerSeasonStatsState stats)
    {
        return stats.InningsPitchedOuts == 0 ? double.MaxValue : (stats.EarnedRuns * 9d) / (stats.InningsPitchedOuts / 3d);
    }

    private static int GetTotalBases(PlayerSeasonStatsState stats)
    {
        var singles = Math.Max(0, stats.Hits - stats.Doubles - stats.Triples - stats.HomeRuns);
        return singles + (stats.Doubles * 2) + (stats.Triples * 3) + (stats.HomeRuns * 4);
    }

    private static List<RosterDisplayRow> SortScoutNoteRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "REPORT" => rows.OrderBy(r => r.HiddenRatings.OverallRating).ToList(),
            _ => rows
        };
    }

    private static List<RosterDisplayRow> SortHiddenRatingsRows(List<RosterDisplayRow> rows, string columnName)
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

    private List<RosterDisplayRow> ApplyRosterFilter(List<RosterDisplayRow> rows)
    {
        return _filterMode switch
        {
            RosterFilterMode.Hitters or RosterFilterMode.Contact or RosterFilterMode.Power or RosterFilterMode.Speed or RosterFilterMode.Fielding or RosterFilterMode.CurrentOps or RosterFilterMode.LastOps
                => rows.Where(row => !IsPitcher(row)).ToList(),
            RosterFilterMode.Pitchers or RosterFilterMode.Pitching or RosterFilterMode.CurrentEra or RosterFilterMode.LastEra
                => rows.Where(IsPitcher).ToList(),
            _ => rows
        };
    }

    private List<RosterDisplayRow> ApplyDefaultFilterSort(List<RosterDisplayRow> rows)
    {
        return _filterMode switch
        {
            RosterFilterMode.Contact => rows.OrderByDescending(row => row.HiddenRatings.ContactRating).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.Power => rows.OrderByDescending(row => row.HiddenRatings.PowerRating).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.Speed => rows.OrderByDescending(row => row.HiddenRatings.SpeedRating).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.Fielding => rows.OrderByDescending(row => row.HiddenRatings.FieldingRating).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.Pitching => rows.OrderByDescending(row => row.HiddenRatings.PitchingRating).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.CurrentOps => rows.OrderByDescending(row => CalculateOps(row.SeasonStats)).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.CurrentEra => rows.OrderBy(row => CalculateEra(row.SeasonStats)).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.LastOps => rows.OrderByDescending(row => CalculateOps(row.LastSeasonStats)).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.LastEra => rows.OrderBy(row => CalculateEra(row.LastSeasonStats)).ThenBy(row => row.PlayerName).ToList(),
            RosterFilterMode.Pitchers => rows.OrderByDescending(row => row.HiddenRatings.PitchingRating).ThenBy(row => row.PlayerName).ToList(),
            _ => rows.OrderBy(row => row.RotationSlot ?? 99).ThenBy(row => row.LineupSlot ?? 99).ThenBy(row => row.PlayerName).ToList()
        };
    }

    private List<RosterDisplayRow> GetRosterRows()
    {
        return _franchiseSession.GetSelectedTeamRoster()
            .Select(row => new RosterDisplayRow(
                row.PlayerId,
                row.PlayerName,
                row.PrimaryPosition,
                row.SecondaryPosition,
                row.Age,
                row.LineupSlot,
                row.RotationSlot,
                _franchiseSession.GetPlayerRatings(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age),
                _franchiseSession.GetPlayerSeasonStats(row.PlayerId),
                _franchiseSession.GetLastSeasonStats(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age)))
            .ToList();
    }

    private static bool IsPitcher(RosterDisplayRow row)
    {
        return row.PrimaryPosition is "SP" or "RP";
    }

    private static RosterFilterMode GetNextFilterMode(RosterFilterMode currentFilter)
    {
        return currentFilter switch
        {
            RosterFilterMode.All => RosterFilterMode.Hitters,
            RosterFilterMode.Hitters => RosterFilterMode.Pitchers,
            RosterFilterMode.Pitchers => RosterFilterMode.Contact,
            RosterFilterMode.Contact => RosterFilterMode.Power,
            RosterFilterMode.Power => RosterFilterMode.Speed,
            RosterFilterMode.Speed => RosterFilterMode.Fielding,
            RosterFilterMode.Fielding => RosterFilterMode.Pitching,
            RosterFilterMode.Pitching => RosterFilterMode.CurrentOps,
            RosterFilterMode.CurrentOps => RosterFilterMode.CurrentEra,
            RosterFilterMode.CurrentEra => RosterFilterMode.LastOps,
            RosterFilterMode.LastOps => RosterFilterMode.LastEra,
            _ => RosterFilterMode.All
        };
    }

    private static string GetFilterLabel(RosterFilterMode filterMode)
    {
        return filterMode switch
        {
            RosterFilterMode.All => "All Players",
            RosterFilterMode.Hitters => "Hitters",
            RosterFilterMode.Pitchers => "Pitchers",
            RosterFilterMode.Contact => "Best Contact",
            RosterFilterMode.Power => "Best Power",
            RosterFilterMode.Speed => "Most Speed",
            RosterFilterMode.Fielding => "Best Gloves",
            RosterFilterMode.Pitching => "Best Arms",
            RosterFilterMode.CurrentOps => "Current OPS",
            RosterFilterMode.CurrentEra => "Current ERA",
            RosterFilterMode.LastOps => "Last OPS",
            _ => "Last ERA"
        };
    }

    private static string GetFilterDescription(RosterFilterMode filterMode)
    {
        return filterMode switch
        {
            RosterFilterMode.All => "showing the full club",
            RosterFilterMode.Hitters => "showing only hitters and position players",
            RosterFilterMode.Pitchers => "showing only pitchers",
            RosterFilterMode.Contact => "sorted by contact skill",
            RosterFilterMode.Power => "sorted by power bats",
            RosterFilterMode.Speed => "sorted by speed and athleticism",
            RosterFilterMode.Fielding => "sorted by glove work",
            RosterFilterMode.Pitching => "sorted by pitching stuff",
            RosterFilterMode.CurrentOps => "sorted by current-season OPS",
            RosterFilterMode.CurrentEra => "sorted by current-season ERA",
            RosterFilterMode.LastOps => "sorted by last-season OPS",
            _ => "sorted by last-season ERA"
        };
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
        PlayerSeasonStatsState SeasonStats,
        PlayerSeasonStatsState LastSeasonStats);

    private enum RosterViewMode
    {
        Standard,
        SeasonStats,
        LastSeasonStats,
        ScoutNotes,
        HiddenRatings
    }

    private enum RosterFilterMode
    {
        All,
        Hitters,
        Pitchers,
        Contact,
        Power,
        Speed,
        Fielding,
        Pitching,
        CurrentOps,
        CurrentEra,
        LastOps,
        LastEra
    }
}
