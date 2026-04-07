using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class CoachingStaffScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly Rectangle _backButtonBounds = new(24, 34, 120, 36);
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private string _selectedRole = "Manager";
    private string _statusMessage = "Select a role, then drag a coach from the list onto that role to make the change.";
    private bool _isDraggingCandidate;
    private Point _viewport = new(1280, 720);
    private CoachProfileView? _draggedCandidate;
    private Point _dragPosition;

    public CoachingStaffScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _isDraggingCandidate = false;
        _draggedCandidate = null;
        _dragPosition = Point.Zero;

        var coaches = _franchiseSession.GetCoachingStaff();
        if (coaches.Count > 0 && coaches.All(coach => !string.Equals(coach.Role, _selectedRole, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedRole = coaches[0].Role;
        }

        _statusMessage = BuildRoleStatusMessage();
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

        if (_isDraggingCandidate)
        {
            _dragPosition = mousePosition;
        }

        if (isPress)
        {
            if (_backButtonBounds.Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (TryStartDragFromCandidate(mousePosition))
            {
            }
            else
            {
                TrySelectRole(mousePosition);
            }
        }

        if (isRelease && _isDraggingCandidate)
        {
            TryDropCandidate(mousePosition);
            _isDraggingCandidate = false;
            _draggedCandidate = null;
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        var coaches = _franchiseSession.GetCoachingStaff();
        var selectedCoach = coaches.FirstOrDefault(coach => string.Equals(coach.Role, _selectedRole, StringComparison.OrdinalIgnoreCase))
            ?? coaches.FirstOrDefault();
        var candidates = _franchiseSession.GetCoachCandidates(_selectedRole);
        var mousePosition = Mouse.GetState().Position;
        var selectedCoachPanelBounds = GetSelectedCoachPanelBounds();
        var candidatePanelBounds = GetCandidatePanelBounds();
        var selectedPanelColor = _isDraggingCandidate && _draggedCandidate != null && string.Equals(_draggedCandidate.Role, _selectedRole, StringComparison.OrdinalIgnoreCase)
            ? new Color(70, 84, 42)
            : new Color(38, 48, 56);

        uiRenderer.DrawText("Coaching Staff", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(
            "Select a role on the left, then drag a coach from the list onto that role to hire them.",
            new Rectangle(168, 112, 560, 42),
            Color.White,
            uiRenderer.UiSmallFont,
            2);

        uiRenderer.DrawText("CURRENT STAFF", new Vector2(68, 176), Color.White, uiRenderer.UiMediumFont);
        for (var i = 0; i < coaches.Count; i++)
        {
            var coach = coaches[i];
            var bounds = GetRoleBounds(i);
            var isHovered = bounds.Contains(mousePosition);
            var isSelected = string.Equals(_selectedRole, coach.Role, StringComparison.OrdinalIgnoreCase);
            var isDropTarget = _isDraggingCandidate && _draggedCandidate != null && bounds.Contains(_dragPosition) && string.Equals(_draggedCandidate.Role, coach.Role, StringComparison.OrdinalIgnoreCase);
            var color = isDropTarget
                ? Color.Goldenrod
                : (isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray));
            uiRenderer.DrawButton($"{coach.Role}: {coach.Name}", bounds, color, Color.White);
        }

        uiRenderer.DrawText("SELECTED ROLE", new Vector2(selectedCoachPanelBounds.X + 16, 176), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawButton(string.Empty, selectedCoachPanelBounds, selectedPanelColor, Color.White);
        if (selectedCoach != null)
        {
            uiRenderer.DrawTextInBounds($"Current {selectedCoach.Role}", new Rectangle(selectedCoachPanelBounds.X + 16, selectedCoachPanelBounds.Y + 10, 220, 16), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(selectedCoach.Name, new Rectangle(selectedCoachPanelBounds.X + 16, selectedCoachPanelBounds.Y + 30, selectedCoachPanelBounds.Width - 32, 22), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"Specialty: {selectedCoach.Specialty}", new Rectangle(selectedCoachPanelBounds.X + 16, selectedCoachPanelBounds.Y + 60, selectedCoachPanelBounds.Width - 32, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"Voice: {selectedCoach.Voice}", new Rectangle(selectedCoachPanelBounds.X + 16, selectedCoachPanelBounds.Y + 82, selectedCoachPanelBounds.Width - 32, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds("Drop a coach here or on the matching role row to make the move.", new Rectangle(selectedCoachPanelBounds.X + 16, selectedCoachPanelBounds.Y + 108, selectedCoachPanelBounds.Width - 32, 36), Color.White, uiRenderer.UiSmallFont, 2);
        }

        uiRenderer.DrawText($"AVAILABLE {_selectedRole.ToUpperInvariant()} OPTIONS", new Vector2(candidatePanelBounds.X + 16, candidatePanelBounds.Y - 26), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawButton(string.Empty, candidatePanelBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawTextInBounds("Current coach is highlighted in green. Drag any option onto the role to hire them.", new Rectangle(candidatePanelBounds.X + 12, candidatePanelBounds.Y + 6, candidatePanelBounds.Width - 24, 20), Color.Gold, uiRenderer.UiSmallFont);

        var candidateFont = candidates.Count <= 8 ? uiRenderer.UiMediumFont : uiRenderer.UiSmallFont;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var bounds = GetCandidateBounds(i);
            var isHovered = bounds.Contains(mousePosition);
            var isCurrentCoach = selectedCoach != null && IsSameCoach(candidate, selectedCoach);
            var isDraggingThisCoach = _isDraggingCandidate && _draggedCandidate != null && IsSameCoach(candidate, _draggedCandidate);
            var color = isDraggingThisCoach
                ? Color.Goldenrod
                : (isCurrentCoach ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray));
            var label = $"{Truncate(candidate.Name, 22)} | {Truncate(candidate.Specialty, 20)} | {candidate.Voice}";
            uiRenderer.DrawButton(label, bounds, color, Color.White, candidateFont);
        }

        var statusBounds = new Rectangle(68, _viewport.Y - 110, Math.Max(540, _viewport.X - 136), 68);
        uiRenderer.DrawButton(string.Empty, statusBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawTextInBounds("Staff Update", new Rectangle(statusBounds.X + 12, statusBounds.Y + 6, 180, 16), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(statusBounds.X + 12, statusBounds.Y + 24, statusBounds.Width - 24, statusBounds.Height - 28), Color.White, uiRenderer.UiSmallFont, 2);

        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, _backButtonBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);

        if (_isDraggingCandidate && _draggedCandidate != null)
        {
            uiRenderer.DrawText($"Dragging: {Truncate(_draggedCandidate.Name, 24)}", new Vector2(_dragPosition.X + 16, _dragPosition.Y + 16), Color.Gold, uiRenderer.UiSmallFont);
        }
    }

    private bool TrySelectRole(Point mousePosition)
    {
        var coaches = _franchiseSession.GetCoachingStaff();
        for (var i = 0; i < coaches.Count; i++)
        {
            if (!GetRoleBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedRole = coaches[i].Role;
            _statusMessage = BuildRoleStatusMessage();
            return true;
        }

        return false;
    }

    private bool TryStartDragFromCandidate(Point mousePosition)
    {
        var candidates = _franchiseSession.GetCoachCandidates(_selectedRole);
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!GetCandidateBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _draggedCandidate = candidates[i];
            _isDraggingCandidate = true;
            _dragPosition = mousePosition;
            _statusMessage = $"Dragging {candidates[i].Name} for the {_selectedRole} opening.";
            return true;
        }

        return false;
    }

    private void TryDropCandidate(Point mousePosition)
    {
        if (_draggedCandidate == null)
        {
            return;
        }

        var coaches = _franchiseSession.GetCoachingStaff();
        for (var i = 0; i < coaches.Count; i++)
        {
            var coach = coaches[i];
            if (!GetRoleBounds(i).Contains(mousePosition))
            {
                continue;
            }

            if (!string.Equals(coach.Role, _draggedCandidate.Role, StringComparison.OrdinalIgnoreCase))
            {
                _statusMessage = $"That coach belongs on the {_draggedCandidate.Role} list. Select that role first to make the move.";
                return;
            }

            _selectedRole = coach.Role;
            _franchiseSession.AssignCoach(coach.Role, _draggedCandidate.Name, _draggedCandidate.Specialty, _draggedCandidate.Voice, out _statusMessage);
            return;
        }

        if (GetSelectedCoachPanelBounds().Contains(mousePosition))
        {
            _franchiseSession.AssignCoach(_selectedRole, _draggedCandidate.Name, _draggedCandidate.Specialty, _draggedCandidate.Voice, out _statusMessage);
        }
    }

    private string BuildRoleStatusMessage()
    {
        if (_selectedRole is "Team Doctor" or "Physiologist")
        {
            var atRiskPlayers = _franchiseSession.GetMedicalRiskBoard(3);
            if (atRiskPlayers.Count == 0)
            {
                return $"{_selectedRole} report: no major fatigue or injury concerns on the roster right now.";
            }

            var summary = string.Join(" ", atRiskPlayers.Select(player => $"{player.PlayerName} ({player.Status})"));
            return $"{_selectedRole} report: watch {summary}.";
        }

        return $"Viewing the {_selectedRole} role. Drag a coach from the list to make a change.";
    }

    private static bool IsSameCoach(CoachProfileView left, CoachProfileView right)
    {
        return string.Equals(left.Role, right.Role, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Specialty, right.Specialty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Voice, right.Voice, StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private Rectangle GetRoleBounds(int index)
    {
        var leftPanelWidth = Math.Clamp((_viewport.X / 2) - 100, 360, 560);
        return new Rectangle(68, 210 + (index * 44), leftPanelWidth, 34);
    }

    private Rectangle GetSelectedCoachPanelBounds()
    {
        var roleBounds = GetRoleBounds(0);
        var panelX = roleBounds.Right + 36;
        var panelWidth = Math.Max(320, _viewport.X - panelX - 68);
        return new Rectangle(panelX, 204, panelWidth, 152);
    }

    private Rectangle GetCandidatePanelBounds()
    {
        var selectedPanel = GetSelectedCoachPanelBounds();
        var statusTop = _viewport.Y - 110;
        var panelY = selectedPanel.Bottom + 58;
        return new Rectangle(selectedPanel.X, panelY, selectedPanel.Width, Math.Max(180, statusTop - panelY - 12));
    }

    private int GetCandidateRowHeight(int candidateCount)
    {
        var panel = GetCandidatePanelBounds();
        var count = Math.Max(1, candidateCount);
        const int topPadding = 30;
        const int bottomPadding = 10;
        const int rowSpacing = 4;
        return Math.Max(14, (panel.Height - topPadding - bottomPadding - ((count - 1) * rowSpacing)) / count);
    }

    private Rectangle GetCandidateBounds(int index)
    {
        var panel = GetCandidatePanelBounds();
        var candidateCount = Math.Max(1, _franchiseSession.GetCoachCandidates(_selectedRole).Count);
        var rowHeight = GetCandidateRowHeight(candidateCount);
        const int rowSpacing = 4;
        return new Rectangle(panel.X + 16, panel.Y + 28 + (index * (rowHeight + rowSpacing)), panel.Width - 32, rowHeight);
    }
}
