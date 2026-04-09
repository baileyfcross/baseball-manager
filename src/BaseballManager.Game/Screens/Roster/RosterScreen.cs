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
    private readonly ButtonControl _viewButton;
    private readonly ButtonControl _hiddenMenuButton;
    private readonly ButtonControl _filterButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private RosterViewMode _viewMode;
    private RosterFilterMode _filterMode;
    private bool _showViewDropdown;
    private bool _showFilterDropdown;
    private bool _showTrainingGrowthMenu;
    private int _pageIndex;
    private string? _sortColumnStandard;
    private string? _sortColumnSeasonStats;
    private string? _sortColumnLastSeasonStats;
    private string? _sortColumnScoutNotes;
    private string? _sortColumnMedicalReport;
    private string? _sortColumnHiddenRatings;
    private string? _sortColumnTrainingGrowth;
    private string? _sortColumnContracts;
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
        _viewButton = new ButtonControl
        {
            Label = "View: Roster",
            OnClick = () =>
            {
                _showViewDropdown = !_showViewDropdown;
                if (_showViewDropdown)
                {
                    _showFilterDropdown = false;
                }
            }
        };
        _hiddenMenuButton = new ButtonControl
        {
            Label = "Training Growth: Off",
            OnClick = () =>
            {
                _showTrainingGrowthMenu = !_showTrainingGrowthMenu;
                _showViewDropdown = false;
                _showFilterDropdown = false;
                _pageIndex = 0;
            }
        };
        _filterButton = new ButtonControl
        {
            Label = "Filter: All Players",
            OnClick = () =>
            {
                _showFilterDropdown = !_showFilterDropdown;
                if (_showFilterDropdown)
                {
                    _showViewDropdown = false;
                }
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
        _filterMode = RosterFilterMode.All;
        _showViewDropdown = false;
        _showFilterDropdown = false;
        _showTrainingGrowthMenu = false;
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
            else if (GetViewButtonBounds().Contains(currentMouseState.Position))
            {
                _viewButton.Click();
            }
            else if (GetFilterButtonBounds().Contains(currentMouseState.Position))
            {
                _filterButton.Click();
            }
            else if (GetHiddenMenuButtonBounds().Contains(currentMouseState.Position))
            {
                _hiddenMenuButton.Click();
            }
            else if (_showViewDropdown)
            {
                TrySelectViewOption(currentMouseState.Position);
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
                var displayMode = _showTrainingGrowthMenu ? RosterViewMode.TrainingGrowth : _viewMode;
                var teamPlayers = ApplySorting(ApplyRosterFilter(GetRosterRows()), displayMode);
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

        var displayMode = _showTrainingGrowthMenu ? RosterViewMode.TrainingGrowth : _viewMode;

        _viewButton.Label = _showViewDropdown
            ? $"View: {GetViewLabel(_viewMode)} ^"
            : $"View: {GetViewLabel(_viewMode)} v";
        _hiddenMenuButton.Label = _showTrainingGrowthMenu ? "Training Growth: On" : "Training Growth: Off";
        _filterButton.Label = _showFilterDropdown
            ? $"Filter: {GetFilterLabel(_filterMode)} ^"
            : $"Filter: {GetFilterLabel(_filterMode)} v";

        var title = displayMode switch
        {
            RosterViewMode.SeasonStats => "Roster - Season Stats",
            RosterViewMode.LastSeasonStats => "Roster - Last Season",
            RosterViewMode.ScoutNotes => "Roster - Scout Notes",
            RosterViewMode.MedicalReport => "Roster - Medical Report",
            RosterViewMode.HiddenRatings => "Roster - Secret Attributes",
            RosterViewMode.TrainingGrowth => "Roster - Training Growth",
            RosterViewMode.Contracts => "Roster - Contracts",
            _ => "Roster"
        };

        uiRenderer.DrawText(title, new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Active Filter: {GetFilterDescription(_filterMode)}", new Rectangle(168, 110, Math.Max(420, _viewport.X - 220), 20), Color.White, uiRenderer.ScoreboardFont);
        if (displayMode == RosterViewMode.TrainingGrowth)
        {
            var trainingWindowText = $"Training window: {_franchiseSession.GetSpringTrainingStartDate():MMM d, yyyy} -> {_franchiseSession.GetCurrentFranchiseDate():MMM d, yyyy}";
            uiRenderer.DrawTextInBounds(trainingWindowText, new Rectangle(168, 132, Math.Max(520, _viewport.X - 220), 20), Color.Gold, uiRenderer.UiSmallFont);
        }

        var rosterRows = GetRosterRows();
        var filteredRows = ApplyRosterFilter(rosterRows);
        var sortedRows = ApplySorting(filteredRows, displayMode);

        if (sortedRows.Count == 0)
        {
            uiRenderer.DrawText("No roster data found for the selected team with the current filter.", new Vector2(GetHeaderX(), GetHeaderY() + 20), Color.White);
        }
        else
        {
            var startIndex = _pageIndex * PageSize;
            var visibleRows = sortedRows.Skip(startIndex).Take(PageSize).ToList();

            switch (displayMode)
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

                case RosterViewMode.MedicalReport:
                    DrawMedicalReportHeader(uiRenderer);
                    for (var i = 0; i < visibleRows.Count; i++)
                    {
                        var row = visibleRows[i];
                        var line = string.Format(
                            "{0,-18} {1,-3} {2,-10} {3}",
                            Truncate(row.PlayerName, 18),
                            row.PrimaryPosition,
                            Truncate(row.MedicalStatus, 10),
                            Truncate(row.MedicalReport, 54));
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
                            "{0,-12} {1,-3} {2,3} {3,3} {4,3} {5,4:0.#} {6,4:0.#} {7,4:0.#} {8,4:0.#} {9,4:0.#} {10,4:0.#} {11,4:0.#} {12,4:0.#} {13,4:0.#}",
                            Truncate(row.PlayerName, 12),
                            row.PrimaryPosition,
                            row.Age,
                            ratings.OverallRating,
                            ratings.AttributeTotal,
                            ratings.ContactExactRating,
                            ratings.PowerExactRating,
                            ratings.DisciplineExactRating,
                            ratings.SpeedExactRating,
                            ratings.FieldingExactRating,
                            ratings.ArmExactRating,
                            ratings.PitchingExactRating,
                            ratings.StaminaExactRating,
                            ratings.DurabilityExactRating);
                        uiRenderer.DrawText(line, new Vector2(GetHeaderX(), GetRowY(i)), Color.White, uiRenderer.ScoreboardFont);
                    }
                    break;

                case RosterViewMode.TrainingGrowth:
                    DrawTrainingGrowthHeader(uiRenderer);
                    DrawTrainingGrowthRows(uiRenderer, visibleRows);
                    break;

                case RosterViewMode.Contracts:
                    DrawContractsHeader(uiRenderer);
                    for (var i = 0; i < visibleRows.Count; i++)
                    {
                        var row = visibleRows[i];
                        var line = string.Format(
                            "{0,-18} {1,-3} {2,3}  {3,7}  {4,2}yr",
                            Truncate(row.PlayerName, 18),
                            row.PrimaryPosition,
                            row.Age,
                            FormatSalary(row.AnnualSalary),
                            row.ContractYears);
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
        var viewBounds = GetViewButtonBounds();
        var filterBounds = GetFilterButtonBounds();
        var hiddenMenuBounds = GetHiddenMenuButtonBounds();
        uiRenderer.DrawButton(_backButton.Label, backBounds, backBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_viewButton.Label, viewBounds, viewBounds.Contains(mousePosition) || _showViewDropdown ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_filterButton.Label, filterBounds, filterBounds.Contains(mousePosition) || _showFilterDropdown ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
        uiRenderer.DrawButton(_hiddenMenuButton.Label, hiddenMenuBounds, hiddenMenuBounds.Contains(mousePosition) || _showTrainingGrowthMenu ? Color.DarkGoldenrod : Color.SaddleBrown, Color.White);

        if (_showViewDropdown)
        {
            DrawViewDropdown(uiRenderer);
        }

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

    private Rectangle GetViewButtonBounds() => new(_viewport.X - 270, 40, 210, 44);

    private Rectangle GetFilterButtonBounds() => new(_viewport.X - 520, 40, 230, 44);

    private Rectangle GetHiddenMenuButtonBounds() => new(Math.Max(40, _viewport.X - 750), 40, 210, 44);

    private Rectangle GetPreviousPageBounds() => new(GetHeaderX(), _viewport.Y - 76, 120, 40);

    private Rectangle GetNextPageBounds() => new(GetHeaderX() + 140, _viewport.Y - 76, 120, 40);

    private void DrawViewDropdown(UiRenderer uiRenderer)
    {
        var options = GetViewOptions();
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var bounds = GetViewOptionBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = option == _viewMode;
            var color = isSelected ? Color.DarkSlateBlue : (isHovered ? Color.SteelBlue : Color.SlateGray);
            uiRenderer.DrawButton(GetViewLabel(option), bounds, color, Color.White, uiRenderer.ScoreboardFont);
        }
    }

    private bool TrySelectViewOption(Point mousePosition)
    {
        var options = GetViewOptions();
        for (var i = 0; i < options.Count; i++)
        {
            if (!GetViewOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _viewMode = options[i];
            _pageIndex = 0;
            _showViewDropdown = false;
            _showTrainingGrowthMenu = false;
            return true;
        }

        _showViewDropdown = false;
        return false;
    }

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

    private Rectangle GetViewOptionBounds(int index)
    {
        var buttonBounds = GetViewButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Bottom + 4 + (index * 32), buttonBounds.Width, 28);
    }

    private Rectangle GetFilterOptionBounds(int index)
    {
        var buttonBounds = GetFilterButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Bottom + 4 + (index * 32), buttonBounds.Width, 28);
    }

    private static IReadOnlyList<RosterViewMode> GetViewOptions()
    {
        return
        [
            RosterViewMode.Standard,
            RosterViewMode.SeasonStats,
            RosterViewMode.LastSeasonStats,
            RosterViewMode.ScoutNotes,
            RosterViewMode.MedicalReport,
            RosterViewMode.HiddenRatings,
            RosterViewMode.Contracts
        ];
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

    private void DrawMedicalReportHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 18, RosterViewMode.MedicalReport);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, RosterViewMode.MedicalReport);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "STATUS", 10, RosterViewMode.MedicalReport);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "REPORT", 36, RosterViewMode.MedicalReport);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(GetHeaderX(), GetHeaderY()), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawHiddenRatingsHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 12, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "AGE", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "OVR", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "TOT", 3, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "CON", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POW", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "DIS", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "SPD", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "FLD", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "ARM", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "PIT", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "STA", 4, RosterViewMode.HiddenRatings);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "DUR", 4, RosterViewMode.HiddenRatings);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(GetHeaderX(), GetHeaderY()), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawTrainingGrowthHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 18, RosterViewMode.TrainingGrowth);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, RosterViewMode.TrainingGrowth);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "AGE", 3, RosterViewMode.TrainingGrowth);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "TOT", 5, RosterViewMode.TrainingGrowth);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "OVR", 4, RosterViewMode.TrainingGrowth);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "DETAILS", 42, RosterViewMode.TrainingGrowth);

        uiRenderer.DrawText(headerBuilder.ToString(), new Vector2(GetHeaderX(), GetHeaderY()), Color.White, uiRenderer.ScoreboardFont);
    }

    private void DrawTrainingGrowthRows(UiRenderer uiRenderer, IReadOnlyList<RosterDisplayRow> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = string.Format(
                "{0,-18} {1,-3} {2,3} {3,5} {4,4} {5}",
                Truncate(row.PlayerName, 18),
                row.PrimaryPosition,
                row.Age,
                FormatSignedValue(row.TrainingGrowth.TotalGain),
                FormatSignedValue(row.TrainingGrowth.OverallGain),
                Truncate(row.TrainingGrowth.TopDetails, 42));
            uiRenderer.DrawText(line, new Vector2(GetHeaderX(), GetRowY(i)), Color.White, uiRenderer.ScoreboardFont);
        }
    }

    private void DrawContractsHeader(UiRenderer uiRenderer)
    {
        _headerHitBoxes.Clear();
        var headerBuilder = new System.Text.StringBuilder();

        AppendColumn(headerBuilder, uiRenderer, "NAME", 18, RosterViewMode.Contracts);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "POS", 3, RosterViewMode.Contracts);
        AppendStatic(headerBuilder, " ");
        AppendColumn(headerBuilder, uiRenderer, "AGE", 3, RosterViewMode.Contracts);
        AppendStatic(headerBuilder, "  ");
        AppendColumn(headerBuilder, uiRenderer, "SALARY", 7, RosterViewMode.Contracts);
        AppendStatic(headerBuilder, "  ");
        AppendColumn(headerBuilder, uiRenderer, "YRS", 4, RosterViewMode.Contracts);

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
            RosterViewMode.MedicalReport => _sortColumnMedicalReport,
            RosterViewMode.HiddenRatings => _sortColumnHiddenRatings,
            RosterViewMode.TrainingGrowth => _sortColumnTrainingGrowth,
            RosterViewMode.Contracts => _sortColumnContracts,
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
        var activeMode = _showTrainingGrowthMenu ? RosterViewMode.TrainingGrowth : _viewMode;
        var currentSort = GetCurrentSortColumn(activeMode);

        if (currentSort == columnName)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            switch (activeMode)
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
                case RosterViewMode.MedicalReport:
                    _sortColumnMedicalReport = columnName;
                    break;
                case RosterViewMode.HiddenRatings:
                    _sortColumnHiddenRatings = columnName;
                    break;
                case RosterViewMode.TrainingGrowth:
                    _sortColumnTrainingGrowth = columnName;
                    break;
                case RosterViewMode.Contracts:
                    _sortColumnContracts = columnName;
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
            return mode switch
            {
                RosterViewMode.MedicalReport => rows.OrderByDescending(row => row.Health.InjuryDaysRemaining)
                    .ThenByDescending(row => row.Health.DaysUntilAvailable)
                    .ThenByDescending(row => row.Health.Fatigue)
                    .ThenBy(row => row.PlayerName)
                    .ToList(),
                RosterViewMode.TrainingGrowth => rows.OrderByDescending(row => row.TrainingGrowth.TotalGain)
                    .ThenByDescending(row => row.TrainingGrowth.OverallGain)
                    .ThenBy(row => row.PlayerName)
                    .ToList(),
                _ => ApplyDefaultFilterSort(rows)
            };
        }

        var sorted = mode switch
        {
            RosterViewMode.Standard => SortStandardRows(rows, currentSort),
            RosterViewMode.SeasonStats => SortSeasonStatsRows(rows, currentSort, useLastSeasonStats: false),
            RosterViewMode.LastSeasonStats => SortSeasonStatsRows(rows, currentSort, useLastSeasonStats: true),
            RosterViewMode.ScoutNotes => SortScoutNoteRows(rows, currentSort),
            RosterViewMode.MedicalReport => SortMedicalRows(rows, currentSort),
            RosterViewMode.HiddenRatings => SortHiddenRatingsRows(rows, currentSort),
            RosterViewMode.TrainingGrowth => SortTrainingGrowthRows(rows, currentSort),
            RosterViewMode.Contracts => SortContractsRows(rows, currentSort),
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

    private static List<RosterDisplayRow> SortMedicalRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "STATUS" => rows.OrderByDescending(r => r.Health.InjuryDaysRemaining)
                .ThenByDescending(r => r.Health.DaysUntilAvailable)
                .ThenByDescending(r => r.Health.Fatigue)
                .ToList(),
            "REPORT" => rows.OrderBy(r => r.MedicalReport).ToList(),
            _ => rows
        };
    }

    private static List<RosterDisplayRow> SortHiddenRatingsRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "AGE" => rows.OrderBy(r => r.Age).ToList(),
            "OVR" => rows.OrderBy(r => r.HiddenRatings.OverallRating).ToList(),
            "TOT" => rows.OrderBy(r => r.HiddenRatings.AttributeTotal).ToList(),
            "CON" => rows.OrderBy(r => r.HiddenRatings.ContactExactRating).ToList(),
            "POW" => rows.OrderBy(r => r.HiddenRatings.PowerExactRating).ToList(),
            "DIS" => rows.OrderBy(r => r.HiddenRatings.DisciplineExactRating).ToList(),
            "SPD" => rows.OrderBy(r => r.HiddenRatings.SpeedExactRating).ToList(),
            "FLD" => rows.OrderBy(r => r.HiddenRatings.FieldingExactRating).ToList(),
            "ARM" => rows.OrderBy(r => r.HiddenRatings.ArmExactRating).ToList(),
            "PIT" => rows.OrderBy(r => r.HiddenRatings.PitchingExactRating).ToList(),
            "STA" => rows.OrderBy(r => r.HiddenRatings.StaminaExactRating).ToList(),
            "DUR" => rows.OrderBy(r => r.HiddenRatings.DurabilityExactRating).ToList(),
            _ => rows
        };
    }

    private static List<RosterDisplayRow> SortTrainingGrowthRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "AGE" => rows.OrderBy(r => r.Age).ToList(),
            "TOT" => rows.OrderBy(r => r.TrainingGrowth.TotalGain).ToList(),
            "OVR" => rows.OrderBy(r => r.TrainingGrowth.OverallGain).ToList(),
            "DETAILS" => rows.OrderBy(r => r.TrainingGrowth.TopDetails).ToList(),
            _ => rows
        };
    }

    private static List<RosterDisplayRow> SortContractsRows(List<RosterDisplayRow> rows, string columnName)
    {
        return columnName switch
        {
            "NAME" => rows.OrderBy(r => r.PlayerName).ToList(),
            "POS" => rows.OrderBy(r => r.PrimaryPosition).ToList(),
            "AGE" => rows.OrderBy(r => r.Age).ToList(),
            "SALARY" => rows.OrderBy(r => r.AnnualSalary).ToList(),
            "YRS" => rows.OrderBy(r => r.ContractYears).ToList(),
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
        var economy = _franchiseSession.GetSelectedTeamEconomy();
        var contractsByPlayerId = economy.PlayerContracts.ToDictionary(c => c.SubjectId, c => c);
        return _franchiseSession.GetSelectedTeamRoster()
            .Select(row =>
            {
                var hiddenRatings = _franchiseSession.GetPlayerRatings(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age);
                contractsByPlayerId.TryGetValue(row.PlayerId, out var contract);
                return new RosterDisplayRow(
                    row.PlayerId,
                    row.PlayerName,
                    row.PrimaryPosition,
                    row.SecondaryPosition,
                    row.Age,
                    row.LineupSlot,
                    row.RotationSlot,
                    hiddenRatings,
                    _franchiseSession.GetPlayerSeasonStats(row.PlayerId),
                    _franchiseSession.GetLastSeasonStats(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age),
                    _franchiseSession.GetPlayerHealth(row.PlayerId),
                    _franchiseSession.GetPlayerMedicalStatus(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age),
                    _franchiseSession.GetPlayerMedicalReport(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age),
                    BuildTrainingGrowthSummary(row.PlayerId, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age, hiddenRatings),
                    contract?.AnnualSalary ?? 0m,
                    contract?.YearsRemaining ?? 0);
            })
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

    private static string GetViewLabel(RosterViewMode viewMode)
    {
        return viewMode switch
        {
            RosterViewMode.Standard => "Roster",
            RosterViewMode.SeasonStats => "Season Stats",
            RosterViewMode.LastSeasonStats => "Last Season",
            RosterViewMode.ScoutNotes => "Scout Notes",
            RosterViewMode.MedicalReport => "Medical Report",
            RosterViewMode.HiddenRatings => "Secret Attributes",
            RosterViewMode.TrainingGrowth => "Training Growth",
            RosterViewMode.Contracts => "Contracts",
            _ => "Roster"
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

    private static TrainingGrowthSummary BuildTrainingGrowthSummary(Guid playerId, string playerName, string primaryPosition, string secondaryPosition, int age, PlayerHiddenRatingsState currentRatings)
    {
        var baseline = PlayerRatingsGenerator.Generate(playerId, playerName, primaryPosition, secondaryPosition, age);
        var gains = new[]
        {
            new AttributeGain("CON", RoundToHalf(currentRatings.ContactExactRating - baseline.ContactExactRating)),
            new AttributeGain("POW", RoundToHalf(currentRatings.PowerExactRating - baseline.PowerExactRating)),
            new AttributeGain("DIS", RoundToHalf(currentRatings.DisciplineExactRating - baseline.DisciplineExactRating)),
            new AttributeGain("SPD", RoundToHalf(currentRatings.SpeedExactRating - baseline.SpeedExactRating)),
            new AttributeGain("FLD", RoundToHalf(currentRatings.FieldingExactRating - baseline.FieldingExactRating)),
            new AttributeGain("ARM", RoundToHalf(currentRatings.ArmExactRating - baseline.ArmExactRating)),
            new AttributeGain("PIT", RoundToHalf(currentRatings.PitchingExactRating - baseline.PitchingExactRating)),
            new AttributeGain("STA", RoundToHalf(currentRatings.StaminaExactRating - baseline.StaminaExactRating)),
            new AttributeGain("DUR", RoundToHalf(currentRatings.DurabilityExactRating - baseline.DurabilityExactRating))
        };

        var positiveGains = gains
            .Where(gain => gain.Value > 0.01d)
            .OrderByDescending(gain => gain.Value)
            .ToList();

        var totalGain = RoundToHalf(positiveGains.Sum(gain => gain.Value));
        var overallGain = currentRatings.OverallRating - baseline.OverallRating;
        var details = positiveGains.Count == 0
            ? "No visible training gains yet"
            : string.Join(", ", positiveGains.Take(3).Select(gain => $"{gain.Label} {FormatSignedValue(gain.Value)}"));

        return new TrainingGrowthSummary(totalGain, overallGain, details);
    }

    private static double RoundToHalf(double value)
    {
        return Math.Round(value * 2d, MidpointRounding.AwayFromZero) / 2d;
    }

    private static string FormatSalary(decimal salary)
    {
        return salary >= 1_000_000m ? $"${salary / 1_000_000m:0.0}M" : $"${salary / 1_000m:0}K";
    }

    private static string FormatSignedValue(double value)
    {
        var rounded = RoundToHalf(value);
        if (Math.Abs(rounded) < 0.01d)
        {
            return "0.0";
        }

        return rounded > 0d ? $"+{rounded:0.#}" : $"{rounded:0.#}";
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
        PlayerSeasonStatsState LastSeasonStats,
        PlayerHealthState Health,
        string MedicalStatus,
        string MedicalReport,
        TrainingGrowthSummary TrainingGrowth,
        decimal AnnualSalary,
        int ContractYears);

    private sealed record TrainingGrowthSummary(double TotalGain, double OverallGain, string TopDetails);

    private sealed record AttributeGain(string Label, double Value);

    private enum RosterViewMode
    {
        Standard,
        SeasonStats,
        LastSeasonStats,
        ScoutNotes,
        MedicalReport,
        HiddenRatings,
        TrainingGrowth,
        Contracts
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
