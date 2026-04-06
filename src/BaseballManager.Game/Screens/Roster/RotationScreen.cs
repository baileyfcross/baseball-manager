using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Roster;

public sealed class RotationScreen : GameScreen
{
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly Rectangle _backButtonBounds = new(40, 40, 140, 44);
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private readonly ButtonControl _clearSlotButton;
    private MouseState _previousMouseState = default;
    private int _pageIndex;
    private Guid? _selectedPlayerId;
    private int? _selectedSlot;

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
                var bullpenRows = GetBullpenRows();
                var maxPage = Math.Max(0, (int)Math.Ceiling(bullpenRows.Count / 10d) - 1);
                if (_pageIndex < maxPage)
                {
                    _nextPageButton.Click();
                }
            }
            else if (GetClearSlotBounds().Contains(currentMouseState.Position))
            {
                _clearSlotButton.Click();
            }
            else if (TryHandleRotationSlotClick(currentMouseState.Position))
            {
            }
            else if (TryHandleBullpenSelection(currentMouseState.Position))
            {
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        uiRenderer.DrawText("Rotation", new Vector2(100, 50), Color.White);
        uiRenderer.DrawText(_franchiseSession.SelectedTeamName, new Vector2(100, 90), Color.White);

        var rotationRows = GetRotationRows();
        var bullpenRows = GetBullpenRows();

        if (rotationRows.Count == 0 && bullpenRows.Count == 0)
        {
            uiRenderer.DrawText("No rotation data found for the selected team.", new Vector2(100, 140), Color.White);
        }
        else
        {
            uiRenderer.DrawText("Click a pitcher, then click a rotation slot to assign. Click an occupied slot to pick that pitcher up.", new Vector2(100, 130), Color.White);
            uiRenderer.DrawText($"Selected: {GetSelectedPlayerName()}", new Vector2(100, 160), Color.White);

            uiRenderer.DrawText("STARTING ROTATION", new Vector2(100, 200), Color.White);
            for (var slot = 1; slot <= 5; slot++)
            {
                var bounds = GetRotationSlotBounds(slot);
                var row = rotationRows.FirstOrDefault(entry => entry.RotationSlot == slot);
                var label = row == null
                    ? $"{slot}. Empty"
                    : $"{slot}. {Truncate(row.PlayerName, 18)} {row.PrimaryPosition} Age {row.Age}";
                var slotHovered = bounds.Contains(Mouse.GetState().Position);
                var isSelected = _selectedSlot == slot;
                var color = isSelected ? Color.DarkOliveGreen : (slotHovered ? Color.DarkSlateBlue : Color.SlateGray);
                uiRenderer.DrawButton(label, bounds, color, Color.White);
            }

            uiRenderer.DrawText("BULLPEN / EXTRA ARMS", new Vector2(700, 200), Color.White);
            var pageSize = 10;
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
    }

    private void DrawPagingButtons(UiRenderer uiRenderer, int totalRows, int pageSize)
    {
        var maxPage = Math.Max(0, (int)Math.Ceiling(totalRows / (double)pageSize) - 1);
        uiRenderer.DrawText($"Bullpen Page {_pageIndex + 1} / {maxPage + 1}", new Vector2(700, 540), Color.White);

        var previousBounds = GetPreviousPageBounds();
        var nextBounds = GetNextPageBounds();
        uiRenderer.DrawButton(_previousPageButton.Label, previousBounds, previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, nextBounds, nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private Rectangle GetPreviousPageBounds() => new(700, 580, 120, 40);

    private Rectangle GetNextPageBounds() => new(840, 580, 120, 40);

    private Rectangle GetClearSlotBounds() => new(100, 440, 140, 40);

    private Rectangle GetRotationSlotBounds(int slot) => new(100, 240 + (slot - 1) * 34, 500, 30);

    private Rectangle GetBullpenRowBounds(int index) => new(700, 240 + index * 34, 460, 30);

    private bool TryHandleRotationSlotClick(Point position)
    {
        for (var slot = 1; slot <= 5; slot++)
        {
            if (!GetRotationSlotBounds(slot).Contains(position))
            {
                continue;
            }

            _selectedSlot = slot;
            if (_selectedPlayerId.HasValue)
            {
                _franchiseSession.AssignRotationSlot(_selectedPlayerId.Value, slot);
                _selectedPlayerId = null;
            }
            else
            {
                var slotPlayer = GetRotationRows().FirstOrDefault(entry => entry.RotationSlot == slot);
                _selectedPlayerId = slotPlayer?.PlayerId;
            }

            return true;
        }

        return false;
    }

    private bool TryHandleBullpenSelection(Point position)
    {
        var pageSize = 10;
        var visibleRows = GetBullpenRows().Skip(_pageIndex * pageSize).Take(pageSize).ToList();
        for (var i = 0; i < visibleRows.Count; i++)
        {
            if (GetBullpenRowBounds(i).Contains(position))
            {
                _selectedPlayerId = visibleRows[i].PlayerId;
                return true;
            }
        }

        return false;
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

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record RotationDisplayRow(Guid PlayerId, int RotationSlot, string PlayerName, string PrimaryPosition, string SecondaryPosition, int Age);
}
