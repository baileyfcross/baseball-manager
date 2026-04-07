using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Roster;

public sealed class LineupScreen : GameScreen
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
                    _franchiseSession.ClearLineupSlot(_selectedSlot.Value);
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
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;
        var mousePosition = currentMouseState.Position;
        var isPress = _previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed;
        var isRelease = _previousMouseState.LeftButton == ButtonState.Pressed && currentMouseState.LeftButton == ButtonState.Released;

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
                var benchRows = GetBenchRows();
                var maxPage = Math.Max(0, (int)Math.Ceiling(benchRows.Count / 10d) - 1);
                if (_pageIndex < maxPage)
                {
                    _nextPageButton.Click();
                }
            }
            else if (GetClearSlotBounds().Contains(mousePosition))
            {
                _clearSlotButton.Click();
            }
            else if (TryStartDragFromLineupSlot(mousePosition))
            {
            }
            else if (TryStartDragFromBench(mousePosition))
            {
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

        uiRenderer.DrawText("Lineup", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);

        var lineupPanelBounds = GetLineupPanelBounds();
        var benchPanelBounds = GetBenchPanelBounds();
        var headerY = lineupPanelBounds.Y - 34;

        var lineupRows = GetLineupRows();
        var benchRows = GetBenchRows();

        if (lineupRows.Count == 0 && benchRows.Count == 0)
        {
            uiRenderer.DrawText("No lineup data found for the selected team.", new Vector2(100, 140), Color.White);
        }
        else
        {
            uiRenderer.DrawWrappedTextInBounds("Drag a player to a lineup slot to assign. Drag from a slot to move players.", new Rectangle(100, 126, Math.Max(560, _viewport.X - 220), 24), Color.White, uiRenderer.ScoreboardFont, 1);
            uiRenderer.DrawTextInBounds($"Selected: {GetSelectedPlayerName()}", new Rectangle(100, 154, Math.Max(560, _viewport.X - 220), 22), Color.White, uiRenderer.ScoreboardFont);

            uiRenderer.DrawButton(string.Empty, lineupPanelBounds, new Color(38, 48, 56), Color.White);
            uiRenderer.DrawButton(string.Empty, benchPanelBounds, new Color(38, 48, 56), Color.White);
            uiRenderer.DrawText("LINEUP", new Vector2(lineupPanelBounds.X + 12, headerY), Color.White, uiRenderer.UiMediumFont);
            for (var slot = 1; slot <= 9; slot++)
            {
                var bounds = GetLineupSlotBounds(slot);
                var row = lineupRows.FirstOrDefault(entry => entry.LineupSlot == slot);
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

            uiRenderer.DrawText("BENCH / RESERVES", new Vector2(benchPanelBounds.X + 12, headerY), Color.White, uiRenderer.UiMediumFont);
            var pageSize = 10;
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
        }

        var isHovered = _backButtonBounds.Contains(Mouse.GetState().Position);
        var bgColor = isHovered ? Color.DarkGray : Color.Gray;
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, bgColor, Color.White);

        if (_isDragging)
        {
            uiRenderer.DrawText($"Dragging: {Truncate(_draggedPlayerName, 22)}", new Vector2(_dragPosition.X + 16, _dragPosition.Y + 16), Color.Yellow);
        }
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

    private Rectangle GetLineupPanelBounds()
    {
        var width = Math.Max(420, (_viewport.X - 180) / 2);
        var height = Math.Max(320, _viewport.Y - 320);
        return new Rectangle(60, 230, width, height);
    }

    private Rectangle GetBenchPanelBounds()
    {
        var lineupPanelBounds = GetLineupPanelBounds();
        var gap = 24;
        return new Rectangle(lineupPanelBounds.Right + gap, lineupPanelBounds.Y, Math.Max(360, _viewport.X - lineupPanelBounds.Right - gap - 60), lineupPanelBounds.Height);
    }

    private int GetListRowHeight(int rowCount)
    {
        var panelHeight = GetLineupPanelBounds().Height;
        var spacing = 6;
        return Math.Clamp((panelHeight - 58 - ((rowCount - 1) * spacing)) / rowCount, 28, 40);
    }

    private Rectangle GetLineupSlotBounds(int slot)
    {
        var panel = GetLineupPanelBounds();
        var rowHeight = GetListRowHeight(9);
        return new Rectangle(panel.X + 10, panel.Y + 14 + (slot - 1) * (rowHeight + 6), panel.Width - 20, rowHeight);
    }

    private Rectangle GetBenchRowBounds(int index)
    {
        var panel = GetBenchPanelBounds();
        var rowHeight = GetListRowHeight(10);
        return new Rectangle(panel.X + 10, panel.Y + 14 + index * (rowHeight + 6), panel.Width - 20, rowHeight);
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
            var slotPlayer = GetLineupRows().FirstOrDefault(entry => entry.LineupSlot == slot);
            if (slotPlayer != null)
            {
                BeginDrag(slotPlayer.PlayerId, slotPlayer.PlayerName, position);
            }

            return true;
        }

        return false;
    }

    private bool TryStartDragFromBench(Point position)
    {
        var pageSize = 10;
        var visibleRows = GetBenchRows().Skip(_pageIndex * pageSize).Take(pageSize).ToList();
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
            _franchiseSession.AssignLineupSlot(_draggedPlayerId.Value, slot);
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

    private List<LineupDisplayRow> GetLineupRows()
    {
        return _franchiseSession.GetLineupPlayers()
            .Select(row => new LineupDisplayRow(row.PlayerId, row.LineupSlot ?? 0, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age))
            .ToList();
    }

    private List<LineupDisplayRow> GetBenchRows()
    {
        return _franchiseSession.GetBenchPlayers()
            .Select(row => new LineupDisplayRow(row.PlayerId, 0, row.PlayerName, row.PrimaryPosition, row.SecondaryPosition, row.Age))
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

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record LineupDisplayRow(Guid PlayerId, int LineupSlot, string PlayerName, string PrimaryPosition, string SecondaryPosition, int Age);
}
