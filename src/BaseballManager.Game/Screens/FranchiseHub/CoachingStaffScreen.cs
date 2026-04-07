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
    private readonly ButtonControl _previousCoachButton;
    private readonly ButtonControl _nextCoachButton;
    private readonly Rectangle _backButtonBounds = new(1080, 40, 140, 44);
    private readonly Rectangle _previousCoachBounds = new(720, 320, 160, 40);
    private readonly Rectangle _nextCoachBounds = new(900, 320, 160, 40);
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private string _selectedRole = "Manager";
    private string _statusMessage = "Select a role and cycle through candidates to refresh your staff.";

    public CoachingStaffScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _previousCoachButton = new ButtonControl
        {
            Label = "Prev Coach",
            OnClick = () => ChangeCoach(-1)
        };
        _nextCoachButton = new ButtonControl
        {
            Label = "Next Coach",
            OnClick = () => ChangeCoach(1)
        };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        var coaches = _franchiseSession.GetCoachingStaff();
        if (coaches.Count > 0 && coaches.All(coach => !string.Equals(coach.Role, _selectedRole, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedRole = coaches[0].Role;
        }
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
            if (_backButtonBounds.Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (_previousCoachBounds.Contains(mousePosition))
            {
                _previousCoachButton.Click();
            }
            else if (_nextCoachBounds.Contains(mousePosition))
            {
                _nextCoachButton.Click();
            }
            else
            {
                TrySelectRole(mousePosition);
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        var coaches = _franchiseSession.GetCoachingStaff();
        var selectedCoach = coaches.FirstOrDefault(coach => string.Equals(coach.Role, _selectedRole, StringComparison.OrdinalIgnoreCase))
            ?? coaches.FirstOrDefault();

        uiRenderer.DrawText("Coaching Staff", new Vector2(100, 50), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText(_franchiseSession.SelectedTeamName, new Vector2(100, 90), Color.White);

        var introY = 120f;
        foreach (var introLine in WrapText("Swap voices and specialties here, then visit scouting to hear how each coach sizes up players.", 88))
        {
            uiRenderer.DrawText(introLine, new Vector2(100, introY), Color.White, uiRenderer.ScoreboardFont);
            introY += 18f;
        }

        uiRenderer.DrawText("CURRENT STAFF", new Vector2(100, 184), Color.White, uiRenderer.UiMediumFont);
        for (var i = 0; i < coaches.Count; i++)
        {
            var coach = coaches[i];
            var bounds = GetRoleBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = string.Equals(_selectedRole, coach.Role, StringComparison.OrdinalIgnoreCase);
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton($"{coach.Role}: {coach.Name}", bounds, color, Color.White);
        }

        uiRenderer.DrawText("SELECTED COACH", new Vector2(720, 184), Color.White, uiRenderer.UiMediumFont);
        if (selectedCoach != null)
        {
            uiRenderer.DrawText(selectedCoach.Name, new Vector2(720, 210), Color.Goldenrod, uiRenderer.UiMediumFont);
            uiRenderer.DrawText($"Role: {selectedCoach.Role}", new Vector2(720, 246), Color.White, uiRenderer.ScoreboardFont);
            uiRenderer.DrawText($"Specialty: {selectedCoach.Specialty}", new Vector2(720, 270), Color.White, uiRenderer.ScoreboardFont);
            uiRenderer.DrawText($"Voice: {selectedCoach.Voice}", new Vector2(720, 294), Color.White, uiRenderer.ScoreboardFont);
        }

        uiRenderer.DrawButton(_previousCoachButton.Label, _previousCoachBounds, _previousCoachBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_nextCoachButton.Label, _nextCoachBounds, _nextCoachBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);

        var infoLines = new[]
        {
            "Manager: gives the broadest read on who can help your club right now.",
            "Hitting Coach: leans into contact, patience, and raw pop.",
            "Pitching Coach: looks hard at stuff, stamina, and arm strength.",
            "Bench Coach: notices range, instincts, and late-game utility.",
            "Scouting Director: blends ceiling, floor, and long-term fit."
        };

        uiRenderer.DrawText("WHAT EACH ROLE LISTENS FOR", new Vector2(720, 390), Color.White, uiRenderer.UiMediumFont);
        for (var i = 0; i < infoLines.Length; i++)
        {
            uiRenderer.DrawText(infoLines[i], new Vector2(720, 426 + (i * 24)), Color.White, uiRenderer.ScoreboardFont);
        }

        var statusY = 632f;
        foreach (var statusLine in WrapText($"Status: {_statusMessage}", 92).Take(2))
        {
            uiRenderer.DrawText(statusLine, new Vector2(100, statusY), Color.White, uiRenderer.ScoreboardFont);
            statusY += 18f;
        }

        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, _backButtonBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private void ChangeCoach(int direction)
    {
        _franchiseSession.ChangeCoach(_selectedRole, direction, out _statusMessage);
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
            _statusMessage = $"Selected {coaches[i].Name} for the {coaches[i].Role} spot.";
            return true;
        }

        return false;
    }

    private static Rectangle GetRoleBounds(int index) => new(100, 224 + (index * 50), 500, 42);

    private static IEnumerable<string> WrapText(string text, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (candidate.Length <= maxCharacters)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }

            currentLine = word;
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            yield return currentLine;
        }
    }
}
