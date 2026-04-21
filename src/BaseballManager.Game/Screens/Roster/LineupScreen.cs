using BaseballManager.Core.Teams;
using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using BaseballManager.Game.UI.Layout;
using BaseballManager.Game.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Roster;

public sealed class LineupScreen : GameScreen
{
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private readonly ButtonControl _clearSlotButton;
    private readonly ButtonControl _designateDhButton;
    private readonly ButtonControl _vsLeftPitcherButton;
    private readonly ButtonControl _vsRightPitcherButton;
    private MouseState _previousMouseState = default;
    private int _pageIndex;
    private Guid? _selectedPlayerId;
    private int? _selectedSlot;
    private LineupPresetType _selectedPreset = LineupPresetType.VsRightHandedPitcher;
    private bool _isDragging;
    private Guid? _draggedPlayerId;
    private string _draggedPlayerName = string.Empty;
    private Point _dragPosition;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private Rectangle BackButtonBounds => ScreenLayout.BackButtonBounds(_viewport);
    private readonly PlayerContextOverlay _playerContextOverlay = new();
    private string _statusMessage = "A valid lineup needs coverage for C, 1B, 2B, 3B, SS, LF, CF, RF, and one explicit DH.";
    private List<LineupDisplayRow> _cachedLineupRows = [];
    private List<LineupDisplayRow> _cachedBenchRows = [];
    private IReadOnlyList<FranchiseRosterEntry> _cachedSelectedTeamRoster = [];
    private LineupValidationView _cachedLineupValidation = new(false, string.Empty, [], []);
    private string _selectedPlayerRecentStatsLine = "Last 10 G: select a player to view recent batting stats.";

    public LineupScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
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
        _clearSlotButton = new ButtonControl
        {
            Label = "Clear Slot",
            OnClick = () =>
            {
                if (_selectedSlot.HasValue)
                {
                    _franchiseSession.ClearLineupSlot(_selectedSlot.Value, _selectedPreset);
                    _selectedPlayerId = null;
                    RefreshViewData();
                }
            }
        };
        _designateDhButton = new ButtonControl
        {
            Label = "Mark DH",
            OnClick = () =>
            {
                if (_selectedPlayerId.HasValue)
                {
                    _franchiseSession.SetSelectedTeamDesignatedHitter(_selectedPlayerId.Value, _selectedPreset);
                    RefreshViewData();
                }
            }
        };
        _vsLeftPitcherButton = new ButtonControl
        {
            Label = "Vs LHP",
            OnClick = () => SwitchLineupPreset(LineupPresetType.VsLeftHandedPitcher)
        };
        _vsRightPitcherButton = new ButtonControl
        {
            Label = "Vs RHP",
            OnClick = () => SwitchLineupPreset(LineupPresetType.VsRightHandedPitcher)
        };
    }

    public override void OnEnter()
    {
        _pageIndex = 0;
        _selectedPlayerId = null;
        _selectedSlot = null;
        _isDragging = false;
        _draggedPlayerId = null;
        _draggedPlayerName = string.Empty;
        _selectedPreset = _franchiseSession.GetSelectedTeamPregameLineupPreset();
        _ignoreClicksUntilRelease = true;
        _playerContextOverlay.Close();
        RefreshViewData();
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;
        var mousePosition = currentMouseState.Position;
        var isPress = _previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed;
        var isRelease = _previousMouseState.LeftButton == ButtonState.Pressed && currentMouseState.LeftButton == ButtonState.Released;
        var isRightPress = _previousMouseState.RightButton == ButtonState.Released && currentMouseState.RightButton == ButtonState.Pressed;

        if (_ignoreClicksUntilRelease)
        {
            if (currentMouseState.LeftButton == ButtonState.Released)
            {
                _ignoreClicksUntilRelease = false;
            }

            _previousMouseState = currentMouseState;
            return;
        }

        if (_isDragging)
        {
            _dragPosition = mousePosition;
        }

        if (isPress)
        {
            if (_playerContextOverlay.HandleLeftClick(mousePosition, _viewport, out var contextAction))
            {
                if (contextAction.HasValue)
                {
                    ExecuteContextAction(contextAction.Value);
                }

                _previousMouseState = currentMouseState;
                return;
            }

            if (BackButtonBounds.Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetVsLeftPitcherBounds().Contains(mousePosition))
            {
                _vsLeftPitcherButton.Click();
            }
            else if (GetVsRightPitcherBounds().Contains(mousePosition))
            {
                _vsRightPitcherButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(mousePosition))
            {
                _previousPageButton.Click();
            }
            else if (GetNextPageBounds().Contains(mousePosition))
            {
                var benchRows = _cachedBenchRows;
                var pageSize = GetBenchPageSize();
                var maxPage = Math.Max(0, (int)Math.Ceiling(benchRows.Count / (double)pageSize) - 1);
                if (_pageIndex < maxPage)
                {
                    _nextPageButton.Click();
                }
            }
            else if (GetClearSlotBounds().Contains(mousePosition))
            {
                _clearSlotButton.Click();
            }
            else if (GetDesignateDhBounds().Contains(mousePosition))
            {
                _designateDhButton.Click();
            }
            else if (TryStartDragFromLineupSlot(mousePosition))
            {
            }
            else if (TryStartDragFromBench(mousePosition))
            {
            }
        }

        if (isRightPress && !_isDragging)
        {
            if (!TryOpenPlayerContextMenu(mousePosition))
            {
                _playerContextOverlay.Close();
            }
        }

        if (isRelease && _isDragging)
        {
            TryDropOnLineupSlot(mousePosition);
            _isDragging = false;
            _draggedPlayerId = null;
            _draggedPlayerName = string.Empty;
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        uiRenderer.DrawText("Lineup Presets", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        DrawPresetButtons(uiRenderer);
        uiRenderer.DrawTextInBounds($"Editing {GetPresetLabel(_selectedPreset)}. Upcoming game uses {GetPresetLabel(_franchiseSession.GetSelectedTeamPregameLineupPreset())}.", new Rectangle(100, 126, Math.Max(720, _viewport.X - 220), 22), Color.Gold, uiRenderer.UiSmallFont);

        var lineupPanelBounds = GetLineupPanelBounds();
        var benchPanelBounds = GetBenchPanelBounds();
        var headerY = lineupPanelBounds.Y - 28;

        var lineupRows = _cachedLineupRows;
        var benchRows = _cachedBenchRows;
        var lineupValidation = _cachedLineupValidation;

        if (lineupRows.Count == 0 && benchRows.Count == 0)
        {
            uiRenderer.DrawText("No lineup data found for the selected team.", new Vector2(100, 140), Color.White);
        }
        else
        {
            uiRenderer.DrawWrappedTextInBounds("Drag a player to a lineup slot to assign. Drag from a slot to move players.", new Rectangle(100, 150, Math.Max(560, _viewport.X - 220), 24), Color.White, uiRenderer.ScoreboardFont, 1);
            uiRenderer.DrawTextInBounds($"Selected: {GetSelectedPlayerName()}", new Rectangle(100, 178, Math.Max(560, _viewport.X - 220), 22), Color.White, uiRenderer.ScoreboardFont);
            uiRenderer.DrawTextInBounds(_selectedPlayerRecentStatsLine, new Rectangle(100, 204, Math.Max(760, _viewport.X - 220), 22), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(lineupValidation.Summary, new Rectangle(100, 228, Math.Max(860, _viewport.X - 220), 22), lineupValidation.IsValid ? Color.LightGreen : Color.Orange, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(_statusMessage, new Rectangle(100, 250, Math.Max(860, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);

            DrawPositionAssignments(uiRenderer, lineupValidation);

            uiRenderer.DrawButton(string.Empty, lineupPanelBounds, new Color(38, 48, 56), Color.White);
            uiRenderer.DrawButton(string.Empty, benchPanelBounds, new Color(38, 48, 56), Color.White);
            uiRenderer.DrawText("LINEUP", new Vector2(lineupPanelBounds.X + 12, headerY), Color.White, uiRenderer.UiMediumFont);
            for (var slot = 1; slot <= 9; slot++)
            {
                var bounds = GetLineupSlotBounds(slot);
                var row = lineupRows.FirstOrDefault(entry => entry.LineupSlot == slot);
                var label = row == null
                    ? $"{slot}. Empty"
                    : $"{slot}. {Truncate(row.PlayerName, 18)} {(row.IsDesignatedHitter ? "DH" : row.PrimaryPosition)} Age {row.Age}";
                var slotHovered = bounds.Contains(Mouse.GetState().Position);
                var isDropTarget = _isDragging && bounds.Contains(_dragPosition);
                var isSelected = _selectedSlot == slot;
                var color = isDropTarget
                    ? Color.Goldenrod
                    : (isSelected ? Color.DarkOliveGreen : (slotHovered ? Color.DarkSlateBlue : Color.SlateGray));
                uiRenderer.DrawButton(label, bounds, color, Color.White);
            }

            uiRenderer.DrawText("BENCH / RESERVES", new Vector2(benchPanelBounds.X + 12, headerY), Color.White, uiRenderer.UiMediumFont);
            var pageSize = GetBenchPageSize();
            var startIndex = _pageIndex * pageSize;
            var visibleRows = benchRows.Skip(startIndex).Take(pageSize).ToList();
            for (var i = 0; i < visibleRows.Count; i++)
            {
                var row = visibleRows[i];
                var bounds = GetBenchRowBounds(i);
                var label = $"{Truncate(row.PlayerName, 18)} {row.PrimaryPosition}/{row.SecondaryPosition} Age {row.Age}";
                var benchHovered = bounds.Contains(Mouse.GetState().Position);
                var isSelected = _selectedPlayerId == row.PlayerId;
                var color = isSelected ? Color.DarkOliveGreen : (benchHovered ? Color.DarkSlateBlue : Color.SlateGray);
                uiRenderer.DrawButton(label, bounds, color, Color.White);
            }

            DrawPagingButtons(uiRenderer, benchRows.Count, pageSize);
            var clearBounds = GetClearSlotBounds();
            uiRenderer.DrawButton(_clearSlotButton.Label, clearBounds, clearBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
            var designateDhBounds = GetDesignateDhBounds();
            uiRenderer.DrawButton(_designateDhButton.Label, designateDhBounds, designateDhBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        }

        var isHovered = BackButtonBounds.Contains(Mouse.GetState().Position);
        var bgColor = isHovered ? Color.DarkGray : Color.Gray;
        uiRenderer.DrawButton(_backButton.Label, BackButtonBounds, bgColor, Color.White);

        if (_isDragging)
        {
            uiRenderer.DrawText($"Dragging: {Truncate(_draggedPlayerName, 22)}", new Vector2(_dragPosition.X + 16, _dragPosition.Y + 16), Color.Yellow);
        }

        _playerContextOverlay.Draw(uiRenderer, Mouse.GetState().Position, _viewport);
    }

    private void DrawPagingButtons(UiRenderer uiRenderer, int totalRows, int pageSize)
    {
        var maxPage = Math.Max(0, (int)Math.Ceiling(totalRows / (double)pageSize) - 1);
        var pageLabel = $"Bench Page {_pageIndex + 1} / {maxPage + 1}";

        var previousBounds = GetPreviousPageBounds();
        var nextBounds = GetNextPageBounds();
        uiRenderer.DrawButton(_previousPageButton.Label, previousBounds, previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, nextBounds, nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);

        var pageFont = uiRenderer.ScoreboardFont ?? uiRenderer.UiSmallFont;
        var labelWidth = pageFont?.MeasureString(pageLabel).X ?? 0f;
        var labelCenterX = (previousBounds.Left + nextBounds.Right) / 2f;
        var labelX = labelCenterX - (labelWidth / 2f);
        var labelY = Math.Max(previousBounds.Bottom, nextBounds.Bottom) + 8f;
        uiRenderer.DrawText(pageLabel, new Vector2(labelX, labelY), Color.White, uiRenderer.ScoreboardFont);
    }

    private Rectangle GetPreviousPageBounds()
    {
        var benchPanelBounds = GetBenchPanelBounds();
        return new Rectangle(benchPanelBounds.X, benchPanelBounds.Bottom + 12, 120, 40);
    }

    private Rectangle GetNextPageBounds()
    {
        var benchPanelBounds = GetBenchPanelBounds();
        return new Rectangle(benchPanelBounds.Right - 120, benchPanelBounds.Bottom + 12, 120, 40);
    }

    private Rectangle GetClearSlotBounds()
    {
        var lineupPanelBounds = GetLineupPanelBounds();
        return new Rectangle(lineupPanelBounds.X, lineupPanelBounds.Bottom + 12, 140, 40);
    }

    private Rectangle GetDesignateDhBounds()
    {
        var clearBounds = GetClearSlotBounds();
        return new Rectangle(clearBounds.Right + 12, clearBounds.Y, 140, clearBounds.Height);
    }

    private Rectangle GetLineupPanelBounds()
    {
        var width = Math.Max(420, (_viewport.X - 180) / 2);
        var height = Math.Max(240, _viewport.Y - 454);
        return new Rectangle(60, 360, width, height);
    }

    private Rectangle GetBenchPanelBounds()
    {
        var lineupPanelBounds = GetLineupPanelBounds();
        var gap = 24;
        return new Rectangle(lineupPanelBounds.Right + gap, lineupPanelBounds.Y, Math.Max(360, _viewport.X - lineupPanelBounds.Right - gap - 60), lineupPanelBounds.Height);
    }

    private static int GetListRowHeight(Rectangle panelBounds, int rowCount)
    {
        const int spacing = 6;
        const int verticalPadding = 24;
        return Math.Clamp((panelBounds.Height - verticalPadding - ((rowCount - 1) * spacing)) / rowCount, 28, 40);
    }

    private int GetBenchPageSize()
    {
        const int spacing = 6;
        const int minRowHeight = 28;
        const int verticalPadding = 24;
        var panel = GetBenchPanelBounds();
        return Math.Max(1, (panel.Height - verticalPadding + spacing) / (minRowHeight + spacing));
    }

    private Rectangle GetLineupSlotBounds(int slot)
    {
        var panel = GetLineupPanelBounds();
        var rowHeight = GetListRowHeight(panel, 9);
        return new Rectangle(panel.X + 10, panel.Y + 14 + (slot - 1) * (rowHeight + 6), panel.Width - 20, rowHeight);
    }

    private Rectangle GetBenchRowBounds(int index)
    {
        var panel = GetBenchPanelBounds();
        var rowHeight = GetListRowHeight(panel, GetBenchPageSize());
        return new Rectangle(panel.X + 10, panel.Y + 14 + index * (rowHeight + 6), panel.Width - 20, rowHeight);
    }

    private void DrawPresetButtons(UiRenderer uiRenderer)
    {
        var vsLeftBounds = GetVsLeftPitcherBounds();
        var vsRightBounds = GetVsRightPitcherBounds();
        var leftSelected = _selectedPreset == LineupPresetType.VsLeftHandedPitcher;
        var rightSelected = _selectedPreset == LineupPresetType.VsRightHandedPitcher;

        uiRenderer.DrawButton(_vsLeftPitcherButton.Label, vsLeftBounds, leftSelected ? Color.DarkOliveGreen : new Color(52, 74, 90), Color.White);
        uiRenderer.DrawButton(_vsRightPitcherButton.Label, vsRightBounds, rightSelected ? Color.DarkOliveGreen : new Color(52, 74, 90), Color.White);
    }

    private Rectangle GetVsLeftPitcherBounds()
    {
        return new Rectangle(100, 112, 140, 34);
    }

    private Rectangle GetVsRightPitcherBounds()
    {
        return new Rectangle(250, 112, 140, 34);
    }

    private bool TryStartDragFromLineupSlot(Point position)
    {
        for (var slot = 1; slot <= 9; slot++)
        {
            if (!GetLineupSlotBounds(slot).Contains(position))
            {
                continue;
            }

            _selectedSlot = slot;
            var slotPlayer = _cachedLineupRows.FirstOrDefault(entry => entry.LineupSlot == slot);
            if (slotPlayer != null)
            {
                BeginDrag(slotPlayer.PlayerId, slotPlayer.PlayerName, position);
            }

            return true;
        }

        return false;
    }

    private void DrawPositionAssignments(UiRenderer uiRenderer, LineupValidationView lineupValidation)
    {
        var bounds = GetPositionAssignmentsBounds();
        uiRenderer.DrawButton(string.Empty, bounds, new Color(32, 46, 58), Color.Transparent);
        uiRenderer.DrawTextInBounds("DEFENSIVE COVERAGE", new Rectangle(bounds.X + 10, bounds.Y + 6, bounds.Width - 20, 18), Color.Gold, uiRenderer.UiSmallFont);

        for (var i = 0; i < lineupValidation.PositionAssignments.Count; i++)
        {
            var assignment = lineupValidation.PositionAssignments[i];
            var assignmentBounds = GetPositionAssignmentCellBounds(i);
            var isMissing = assignment.PlayerId == null;
            var background = isMissing ? new Color(90, 62, 34) : new Color(52, 74, 90);
            var textColor = isMissing ? Color.White : Color.White;
            var playerLabel = isMissing
                ? "Missing"
                : assignment.LineupSlot.HasValue
                    ? $"#{assignment.LineupSlot} {Truncate(assignment.PlayerName, 12)}"
                    : Truncate(assignment.PlayerName, 14);

            uiRenderer.DrawButton(string.Empty, assignmentBounds, background, Color.Transparent);
            uiRenderer.DrawTextInBounds(assignment.Position, new Rectangle(assignmentBounds.X + 6, assignmentBounds.Y + 4, assignmentBounds.Width - 12, 16), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(playerLabel, new Rectangle(assignmentBounds.X + 6, assignmentBounds.Y + 22, assignmentBounds.Width - 12, assignmentBounds.Height - 26), textColor, uiRenderer.UiSmallFont);
        }
    }

    private Rectangle GetPositionAssignmentsBounds()
    {
        return new Rectangle(60, 260, Math.Max(780, _viewport.X - 120), 80);
    }

    private Rectangle GetPositionAssignmentCellBounds(int index)
    {
        var bounds = GetPositionAssignmentsBounds();
        var topRowCount = 5;
        var isTopRow = index < topRowCount;
        var rowIndex = isTopRow ? 0 : 1;
        var columnIndex = isTopRow ? index : index - topRowCount;
        var columnCount = isTopRow ? topRowCount : 4;
        var gap = 8;
        var availableWidth = bounds.Width - 20 - ((columnCount - 1) * gap);
        var cellWidth = availableWidth / columnCount;
        var cellHeight = 24;
        var x = bounds.X + 10 + columnIndex * (cellWidth + gap);
        var y = bounds.Y + 24 + rowIndex * (cellHeight + 8);
        return new Rectangle(x, y, cellWidth, cellHeight);
    }

    private bool TryStartDragFromBench(Point position)
    {
        var pageSize = GetBenchPageSize();
        var visibleRows = _cachedBenchRows.Skip(_pageIndex * pageSize).Take(pageSize).ToList();
        for (var i = 0; i < visibleRows.Count; i++)
        {
            if (GetBenchRowBounds(i).Contains(position))
            {
                BeginDrag(visibleRows[i].PlayerId, visibleRows[i].PlayerName, position);
                return true;
            }
        }

        return false;
    }

    private bool TryDropOnLineupSlot(Point position)
    {
        if (!_draggedPlayerId.HasValue)
        {
            return false;
        }

        for (var slot = 1; slot <= 9; slot++)
        {
            if (!GetLineupSlotBounds(slot).Contains(position))
            {
                continue;
            }

            _selectedSlot = slot;
            _selectedPlayerId = _draggedPlayerId;
            _franchiseSession.AssignLineupSlot(_draggedPlayerId.Value, slot, _selectedPreset);
            RefreshViewData();
            return true;
        }

        return false;
    }

    private void BeginDrag(Guid playerId, string playerName, Point position)
    {
        _selectedPlayerId = playerId;
        _draggedPlayerId = playerId;
        _draggedPlayerName = playerName;
        _dragPosition = position;
        _isDragging = true;
        UpdateSelectedPlayerDetails();
    }

    private bool TryOpenPlayerContextMenu(Point position)
    {
        var orgRosterById = _franchiseSession.GetSelectedTeamOrganizationRoster().ToDictionary(player => player.PlayerId, player => player);

        for (var slot = 1; slot <= 9; slot++)
        {
            if (!GetLineupSlotBounds(slot).Contains(position))
            {
                continue;
            }

            var slotPlayer = _cachedLineupRows.FirstOrDefault(entry => entry.LineupSlot == slot);
            if (slotPlayer != null && orgRosterById.TryGetValue(slotPlayer.PlayerId, out var rosterPlayer))
            {
                OpenPlayerContext(position, rosterPlayer);
                return true;
            }
        }

        var pageSize = GetBenchPageSize();
        var visibleRows = _cachedBenchRows.Skip(_pageIndex * pageSize).Take(pageSize).ToList();
        for (var i = 0; i < visibleRows.Count; i++)
        {
            if (!GetBenchRowBounds(i).Contains(position))
            {
                continue;
            }

            if (orgRosterById.TryGetValue(visibleRows[i].PlayerId, out var rosterPlayer))
            {
                OpenPlayerContext(position, rosterPlayer);
                return true;
            }
        }

        return false;
    }

    private void OpenPlayerContext(Point position, OrganizationRosterPlayerView player)
    {
        _selectedPlayerId = player.PlayerId;
        UpdateSelectedPlayerDetails();
        var rosterActions = new List<PlayerContextActionView>
        {
            new(PlayerContextAction.AssignToFortyMan, "Add To 40-Man", player.CanAssignToFortyMan),
            new(PlayerContextAction.AssignToTripleA, "Send To AAA", player.AffiliateLevel != MinorLeagueAffiliateLevel.TripleA),
            new(PlayerContextAction.AssignToDoubleA, "Send To AA", player.AffiliateLevel != MinorLeagueAffiliateLevel.DoubleA),
            new(PlayerContextAction.AssignToSingleA, "Send To A", player.AffiliateLevel != MinorLeagueAffiliateLevel.SingleA),
            new(PlayerContextAction.RemoveFromFortyMan, "Remove From 40-Man", player.IsOnFortyMan),
            new(PlayerContextAction.ReleasePlayer, "Release", player.CanRelease)
        };
        _playerContextOverlay.Open(
            position,
            player.PlayerName,
            [
                new PlayerContextActionView(PlayerContextAction.OpenRosterAssignments, "Roster", rosterActions.Any(action => action.IsEnabled)),
                new PlayerContextActionView(PlayerContextAction.OpenProfile, "Profile")
            ],
            rosterActions,
            _franchiseSession.GetPlayerProfile(player.PlayerId));
    }

    private void ExecuteContextAction(PlayerContextAction action)
    {
        if (!_selectedPlayerId.HasValue)
        {
            return;
        }

        switch (action)
        {
            case PlayerContextAction.AssignToFortyMan:
                _franchiseSession.AssignSelectedTeamPlayerToFortyMan(_selectedPlayerId.Value, out _);
                break;
            case PlayerContextAction.AssignToTripleA:
                _franchiseSession.AssignSelectedTeamPlayerToAffiliate(_selectedPlayerId.Value, MinorLeagueAffiliateLevel.TripleA, out _);
                break;
            case PlayerContextAction.AssignToDoubleA:
                _franchiseSession.AssignSelectedTeamPlayerToAffiliate(_selectedPlayerId.Value, MinorLeagueAffiliateLevel.DoubleA, out _);
                break;
            case PlayerContextAction.AssignToSingleA:
                _franchiseSession.AssignSelectedTeamPlayerToAffiliate(_selectedPlayerId.Value, MinorLeagueAffiliateLevel.SingleA, out _);
                break;
            case PlayerContextAction.RemoveFromFortyMan:
                _franchiseSession.RemoveSelectedTeamPlayerFromFortyMan(_selectedPlayerId.Value, out _);
                break;
            case PlayerContextAction.ReleasePlayer:
                _franchiseSession.ReleaseSelectedTeamPlayer(_selectedPlayerId.Value, out _);
                break;
        }

        RefreshViewData();
    }

    private List<LineupDisplayRow> GetLineupRows()
    {
        return _cachedSelectedTeamRoster
            .Where(row => row.LineupSlot.HasValue)
            .OrderBy(row => row.LineupSlot)
            .ThenBy(row => row.PlayerName)
            .Select(row => new LineupDisplayRow(row.PlayerId, row.LineupSlot ?? 0, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age, row.IsDesignatedHitter))
            .ToList();
    }

    private List<LineupDisplayRow> GetBenchRows()
    {
        return _cachedSelectedTeamRoster
            .Where(row => !row.LineupSlot.HasValue && row.PrimaryPosition is not "SP" and not "RP")
            .OrderBy(row => row.PrimaryPosition)
            .ThenBy(row => row.PlayerName)
            .Select(row => new LineupDisplayRow(row.PlayerId, 0, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age, false))
            .ToList();
    }

    private void SwitchLineupPreset(LineupPresetType lineupPresetType)
    {
        if (_selectedPreset == lineupPresetType)
        {
            return;
        }

        _selectedPreset = lineupPresetType;
        _pageIndex = 0;
        _selectedPlayerId = null;
        _selectedSlot = null;
        _playerContextOverlay.Close();
        RefreshViewData();
    }

    private void RefreshViewData()
    {
        _cachedSelectedTeamRoster = _franchiseSession.GetSelectedTeamRoster(_selectedPreset);
        _cachedLineupRows = GetLineupRows();
        _cachedBenchRows = GetBenchRows();
        _cachedLineupValidation = _franchiseSession.GetSelectedTeamLineupValidation(_selectedPreset);
        UpdateStatusMessage();
        UpdateSelectedPlayerDetails();

        var pageSize = GetBenchPageSize();
        var maxPage = Math.Max(0, (int)Math.Ceiling(_cachedBenchRows.Count / (double)pageSize) - 1);
        _pageIndex = Math.Min(_pageIndex, maxPage);
    }

    private void UpdateStatusMessage()
    {
        var validation = _cachedLineupValidation;
        var designatedHitter = _franchiseSession.GetSelectedTeamDesignatedHitter(_selectedPreset);
        var designatedHitterLabel = designatedHitter == null ? "No DH selected" : $"DH: {designatedHitter.PlayerName}";
        _statusMessage = $"{validation.Summary} {designatedHitterLabel}.";
    }

    private void UpdateSelectedPlayerDetails()
    {
        if (!_selectedPlayerId.HasValue)
        {
            _selectedPlayerRecentStatsLine = "Last 10 G: select a player to view recent batting stats.";
            return;
        }

        var recent = _franchiseSession.GetRecentPlayerStats(_selectedPlayerId.Value, 10);
        _selectedPlayerRecentStatsLine = $"Last 10 G: GP {recent.GamesPlayed} | H {recent.Hits} | HR {recent.HomeRuns} | AVG {recent.BattingAverageDisplay} | OPS {recent.OpsDisplay} | SO {recent.Strikeouts}";
    }

    private static string GetPresetLabel(LineupPresetType lineupPresetType)
    {
        return lineupPresetType == LineupPresetType.VsLeftHandedPitcher ? "lineup vs LHP" : "lineup vs RHP";
    }

    private string GetSelectedPlayerName()
    {
        if (!_selectedPlayerId.HasValue)
        {
            return "None";
        }

        var player = _cachedSelectedTeamRoster.FirstOrDefault(entry => entry.PlayerId == _selectedPlayerId.Value);
        return player?.PlayerName ?? "None";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record LineupDisplayRow(Guid PlayerId, int LineupSlot, string PlayerName, string PrimaryPosition, string SecondaryPosition, int Age, bool IsDesignatedHitter);
}
