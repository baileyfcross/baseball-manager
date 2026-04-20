using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using BaseballManager.Game.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Roster;

public sealed class RotationScreen : GameScreen
{
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly Rectangle _backButtonBounds = new(24, 34, 120, 36);
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private readonly ButtonControl _clearSlotButton;
    private MouseState _previousMouseState = default;
    private int _pageIndex;
    private Guid? _selectedPlayerId;
    private int? _selectedSlot;
    private bool _isDragging;
    private Guid? _draggedPlayerId;
    private string _draggedPlayerName = string.Empty;
    private Point _dragPosition;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private readonly PlayerContextOverlay _playerContextOverlay = new();

    public RotationScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
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
                    _franchiseSession.ClearRotationSlot(_selectedSlot.Value);
                    _selectedPlayerId = null;
                }
            }
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
        _ignoreClicksUntilRelease = true;
        _playerContextOverlay.Close();
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

            if (_backButtonBounds.Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(mousePosition))
            {
                _previousPageButton.Click();
            }
            else if (GetNextPageBounds().Contains(mousePosition))
            {
                var bullpenRows = GetBullpenRows();
                var pageSize = GetBullpenPageSize();
                var maxPage = Math.Max(0, (int)Math.Ceiling(bullpenRows.Count / (double)pageSize) - 1);
                if (_pageIndex < maxPage)
                {
                    _nextPageButton.Click();
                }
            }
            else if (GetClearSlotBounds().Contains(mousePosition))
            {
                _clearSlotButton.Click();
            }
            else if (TryStartDragFromRotationSlot(mousePosition))
            {
            }
            else if (TryStartDragFromBullpen(mousePosition))
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
            TryDropOnRotationSlot(mousePosition);
            _isDragging = false;
            _draggedPlayerId = null;
            _draggedPlayerName = string.Empty;
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        uiRenderer.DrawText("Rotation", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);

        var rotationPanelBounds = GetRotationPanelBounds();
        var bullpenPanelBounds = GetBullpenPanelBounds();
        var headerY = rotationPanelBounds.Y - 34;

        var rotationRows = GetRotationRows();
        var bullpenRows = GetBullpenRows();

        if (rotationRows.Count == 0 && bullpenRows.Count == 0)
        {
            uiRenderer.DrawText("No rotation data found for the selected team.", new Vector2(100, 140), Color.White);
        }
        else
        {
            uiRenderer.DrawWrappedTextInBounds("Drag a pitcher to a rotation slot to assign. Drag from a slot to move pitchers.", new Rectangle(100, 126, Math.Max(560, _viewport.X - 220), 24), Color.White, uiRenderer.ScoreboardFont, 1);
            uiRenderer.DrawTextInBounds($"Selected: {GetSelectedPlayerName()}", new Rectangle(100, 154, Math.Max(560, _viewport.X - 220), 22), Color.White, uiRenderer.ScoreboardFont);
            uiRenderer.DrawTextInBounds(GetSelectedPlayerRecentStatsLine(), new Rectangle(100, 180, Math.Max(760, _viewport.X - 220), 22), Color.Gold, uiRenderer.UiSmallFont);

            uiRenderer.DrawButton(string.Empty, rotationPanelBounds, new Color(38, 48, 56), Color.White);
            uiRenderer.DrawButton(string.Empty, bullpenPanelBounds, new Color(38, 48, 56), Color.White);
            uiRenderer.DrawText("STARTING ROTATION", new Vector2(rotationPanelBounds.X + 12, headerY), Color.White, uiRenderer.UiMediumFont);
            for (var slot = 1; slot <= 5; slot++)
            {
                var bounds = GetRotationSlotBounds(slot);
                var row = rotationRows.FirstOrDefault(entry => entry.RotationSlot == slot);
                var label = row == null
                    ? $"{slot}. Empty"
                    : $"{slot}. {Truncate(row.PlayerName, 18)} {row.PrimaryPosition} Age {row.Age}";
                var slotHovered = bounds.Contains(Mouse.GetState().Position);
                var isDropTarget = _isDragging && bounds.Contains(_dragPosition);
                var isSelected = _selectedSlot == slot;
                var color = isDropTarget
                    ? Color.Goldenrod
                    : (isSelected ? Color.DarkOliveGreen : (slotHovered ? Color.DarkSlateBlue : Color.SlateGray));
                uiRenderer.DrawButton(label, bounds, color, Color.White);
            }

            uiRenderer.DrawText("BULLPEN / EXTRA ARMS", new Vector2(bullpenPanelBounds.X + 12, headerY), Color.White, uiRenderer.UiMediumFont);
            var pageSize = GetBullpenPageSize();
            var startIndex = _pageIndex * pageSize;
            var visibleRows = bullpenRows.Skip(startIndex).Take(pageSize).ToList();
            for (var i = 0; i < visibleRows.Count; i++)
            {
                var row = visibleRows[i];
                var bounds = GetBullpenRowBounds(i);
                var label = $"{Truncate(row.PlayerName, 18)} {row.PrimaryPosition}/{row.SecondaryPosition} Age {row.Age}";
                var bullpenHovered = bounds.Contains(Mouse.GetState().Position);
                var isSelected = _selectedPlayerId == row.PlayerId;
                var color = isSelected ? Color.DarkOliveGreen : (bullpenHovered ? Color.DarkSlateBlue : Color.SlateGray);
                uiRenderer.DrawButton(label, bounds, color, Color.White);
            }

            DrawPagingButtons(uiRenderer, bullpenRows.Count, pageSize);
            var clearBounds = GetClearSlotBounds();
            uiRenderer.DrawButton(_clearSlotButton.Label, clearBounds, clearBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        }

        var isHovered = _backButtonBounds.Contains(Mouse.GetState().Position);
        var bgColor = isHovered ? Color.DarkGray : Color.Gray;
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, bgColor, Color.White);

        if (_isDragging)
        {
            uiRenderer.DrawText($"Dragging: {Truncate(_draggedPlayerName, 22)}", new Vector2(_dragPosition.X + 16, _dragPosition.Y + 16), Color.Yellow);
        }

        _playerContextOverlay.Draw(uiRenderer, Mouse.GetState().Position, _viewport);
    }

    private void DrawPagingButtons(UiRenderer uiRenderer, int totalRows, int pageSize)
    {
        var maxPage = Math.Max(0, (int)Math.Ceiling(totalRows / (double)pageSize) - 1);
        var pageLabel = $"Bullpen Page {_pageIndex + 1} / {maxPage + 1}";

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
        var bullpenPanelBounds = GetBullpenPanelBounds();
        return new Rectangle(bullpenPanelBounds.X, bullpenPanelBounds.Bottom + 12, 120, 40);
    }

    private Rectangle GetNextPageBounds()
    {
        var bullpenPanelBounds = GetBullpenPanelBounds();
        return new Rectangle(bullpenPanelBounds.Right - 120, bullpenPanelBounds.Bottom + 12, 120, 40);
    }

    private Rectangle GetClearSlotBounds()
    {
        var rotationPanelBounds = GetRotationPanelBounds();
        return new Rectangle(rotationPanelBounds.X, rotationPanelBounds.Bottom + 12, 140, 40);
    }

    private Rectangle GetRotationPanelBounds()
    {
        var width = Math.Max(420, (_viewport.X - 180) / 2);
        var height = Math.Max(220, _viewport.Y - 320);
        return new Rectangle(60, 230, width, height);
    }

    private Rectangle GetBullpenPanelBounds()
    {
        var rotationPanelBounds = GetRotationPanelBounds();
        var gap = 24;
        return new Rectangle(rotationPanelBounds.Right + gap, rotationPanelBounds.Y, Math.Max(360, _viewport.X - rotationPanelBounds.Right - gap - 60), rotationPanelBounds.Height);
    }

    private static int GetListRowHeight(Rectangle panelBounds, int rowCount)
    {
        const int spacing = 6;
        const int verticalPadding = 28;
        return Math.Clamp((panelBounds.Height - verticalPadding - ((rowCount - 1) * spacing)) / rowCount, 28, 40);
    }

    private int GetBullpenPageSize()
    {
        const int spacing = 6;
        const int minRowHeight = 28;
        const int verticalPadding = 28;
        var panel = GetBullpenPanelBounds();
        return Math.Max(1, (panel.Height - verticalPadding + spacing) / (minRowHeight + spacing));
    }

    private Rectangle GetRotationSlotBounds(int slot)
    {
        var panel = GetRotationPanelBounds();
        var rowHeight = GetListRowHeight(panel, 5);
        return new Rectangle(panel.X + 10, panel.Y + 14 + (slot - 1) * (rowHeight + 6), panel.Width - 20, rowHeight);
    }

    private Rectangle GetBullpenRowBounds(int index)
    {
        var panel = GetBullpenPanelBounds();
        var rowHeight = GetListRowHeight(panel, GetBullpenPageSize());
        return new Rectangle(panel.X + 10, panel.Y + 14 + index * (rowHeight + 6), panel.Width - 20, rowHeight);
    }

    private bool TryStartDragFromRotationSlot(Point position)
    {
        for (var slot = 1; slot <= 5; slot++)
        {
            if (!GetRotationSlotBounds(slot).Contains(position))
            {
                continue;
            }

            _selectedSlot = slot;
            var slotPlayer = GetRotationRows().FirstOrDefault(entry => entry.RotationSlot == slot);
            if (slotPlayer != null)
            {
                BeginDrag(slotPlayer.PlayerId, slotPlayer.PlayerName, position);
            }

            return true;
        }

        return false;
    }

    private bool TryStartDragFromBullpen(Point position)
    {
        var pageSize = GetBullpenPageSize();
        var visibleRows = GetBullpenRows().Skip(_pageIndex * pageSize).Take(pageSize).ToList();
        for (var i = 0; i < visibleRows.Count; i++)
        {
            if (GetBullpenRowBounds(i).Contains(position))
            {
                BeginDrag(visibleRows[i].PlayerId, visibleRows[i].PlayerName, position);
                return true;
            }
        }

        return false;
    }

    private bool TryDropOnRotationSlot(Point position)
    {
        if (!_draggedPlayerId.HasValue)
        {
            return false;
        }

        for (var slot = 1; slot <= 5; slot++)
        {
            if (!GetRotationSlotBounds(slot).Contains(position))
            {
                continue;
            }

            _selectedSlot = slot;
            _selectedPlayerId = _draggedPlayerId;
            _franchiseSession.AssignRotationSlot(_draggedPlayerId.Value, slot);
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
    }

    private bool TryOpenPlayerContextMenu(Point position)
    {
        var orgRosterById = _franchiseSession.GetSelectedTeamOrganizationRoster().ToDictionary(player => player.PlayerId, player => player);

        for (var slot = 1; slot <= 5; slot++)
        {
            if (!GetRotationSlotBounds(slot).Contains(position))
            {
                continue;
            }

            var slotPlayer = GetRotationRows().FirstOrDefault(entry => entry.RotationSlot == slot);
            if (slotPlayer != null && orgRosterById.TryGetValue(slotPlayer.PlayerId, out var rosterPlayer))
            {
                OpenPlayerContext(position, rosterPlayer);
                return true;
            }
        }

        var pageSize = GetBullpenPageSize();
        var visibleRows = GetBullpenRows().Skip(_pageIndex * pageSize).Take(pageSize).ToList();
        for (var i = 0; i < visibleRows.Count; i++)
        {
            if (!GetBullpenRowBounds(i).Contains(position))
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
    }

    private List<RotationDisplayRow> GetRotationRows()
    {
        return _franchiseSession.GetRotationPlayers()
            .Select(row => new RotationDisplayRow(row.PlayerId, row.RotationSlot ?? 0, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age))
            .ToList();
    }

    private List<RotationDisplayRow> GetBullpenRows()
    {
        return _franchiseSession.GetBullpenPlayers()
            .Select(row => new RotationDisplayRow(row.PlayerId, 0, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age))
            .ToList();
    }

    private string GetSelectedPlayerName()
    {
        if (!_selectedPlayerId.HasValue)
        {
            return "None";
        }

        var player = _franchiseSession.GetSelectedTeamRoster().FirstOrDefault(entry => entry.PlayerId == _selectedPlayerId.Value);
        return player?.PlayerName ?? "None";
    }

    private string GetSelectedPlayerRecentStatsLine()
    {
        if (!_selectedPlayerId.HasValue)
        {
            return "Last 10 G: select a player to view recent stats.";
        }

        var recent = _franchiseSession.GetRecentPlayerStats(_selectedPlayerId.Value, 10);
        return $"Last 10 G: GP {recent.GamesPlayed} | IP {recent.InningsPitchedDisplay} | ERA {recent.EraDisplay} | K {recent.PitcherStrikeouts} | W {recent.Wins} | L {recent.Losses}";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record RotationDisplayRow(Guid PlayerId, int RotationSlot, string PlayerName, string PrimaryPosition, string SecondaryPosition, int Age);
}
