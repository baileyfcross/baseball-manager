using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Schedule;

public sealed class ScheduleScreen : GameScreen
{
    private readonly ImportedLeagueData _leagueData;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _previousMonthButton;
    private readonly ButtonControl _nextMonthButton;
    private readonly ButtonControl _practiceFocusPreviousButton;
    private readonly ButtonControl _practiceFocusNextButton;
    private readonly ButtonControl _simSeasonButton;
    private readonly ButtonControl _confirmSimSeasonButton;
    private readonly ButtonControl _cancelSimSeasonButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private bool _showSeasonSimConfirmation;
    private Point _viewport = new(1280, 720);
    private DateTime _visibleMonth;
    private DateTime _selectedDate;
    private string _statusMessage = "Off-days now include team practices and recovery sessions. Use Sim Season to fast-forward to the regular-season finish.";

    private const int LayoutMargin = 48;
    private const int CalendarTop = 188;
    private const int CalendarGap = 4;

    public ScheduleScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _leagueData = leagueData;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _previousMonthButton = new ButtonControl
        {
            Label = "<",
            OnClick = () => ChangeVisibleMonth(-1)
        };
        _nextMonthButton = new ButtonControl
        {
            Label = ">",
            OnClick = () => ChangeVisibleMonth(1)
        };
        _practiceFocusPreviousButton = new ButtonControl
        {
            Label = "<",
            OnClick = () => ChangeSelectedDayPracticeFocus(-1)
        };
        _practiceFocusNextButton = new ButtonControl
        {
            Label = ">",
            OnClick = () => ChangeSelectedDayPracticeFocus(1)
        };
        _simSeasonButton = new ButtonControl
        {
            Label = "Sim Season",
            OnClick = PromptSimToSeasonEnd
        };
        _confirmSimSeasonButton = new ButtonControl
        {
            Label = "Confirm",
            OnClick = SimToSeasonEnd
        };
        _cancelSimSeasonButton = new ButtonControl
        {
            Label = "Cancel",
            OnClick = CancelSeasonSim
        };
    }

    public override void OnEnter()
    {
        SyncCalendarToFranchiseDate();
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
            var mousePosition = currentMouseState.Position;
            if (_showSeasonSimConfirmation)
            {
                if (GetConfirmSimSeasonBounds().Contains(mousePosition))
                {
                    _confirmSimSeasonButton.Click();
                }
                else if (GetCancelSimSeasonBounds().Contains(mousePosition))
                {
                    _cancelSimSeasonButton.Click();
                }

                _previousMouseState = currentMouseState;
                return;
            }

            if (GetBackButtonBounds().Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetPreviousMonthBounds().Contains(mousePosition))
            {
                _previousMonthButton.Click();
            }
            else if (GetNextMonthBounds().Contains(mousePosition))
            {
                _nextMonthButton.Click();
            }
            else if (GetPracticeFocusLeftBounds().Contains(mousePosition))
            {
                _practiceFocusPreviousButton.Click();
            }
            else if (GetPracticeFocusRightBounds().Contains(mousePosition))
            {
                _practiceFocusNextButton.Click();
            }
            else if (GetSimSeasonButtonBounds().Contains(mousePosition))
            {
                _simSeasonButton.Click();
            }
            else if (TrySelectCalendarDay(mousePosition))
            {
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        var teamGames = GetScheduleRows();
        var mousePosition = Mouse.GetState().Position;
        var selectedPracticePlan = GetPracticePlan(_selectedDate.Date, teamGames);
        var practiceFocus = _franchiseSession.GetPracticeFocus(selectedPracticePlan != null ? _selectedDate.Date : null);
        var practiceFocusLabel = FranchiseSession.GetPracticeFocusLabel(practiceFocus);
        var backBounds = GetBackButtonBounds();
        var previousMonthBounds = GetPreviousMonthBounds();
        var nextMonthBounds = GetNextMonthBounds();
        var monthLabelBounds = GetMonthLabelBounds();
        var practiceFocusLabelBounds = GetPracticeFocusLabelBounds();
        var practiceFocusLeftBounds = GetPracticeFocusLeftBounds();
        var practiceFocusValueBounds = GetPracticeFocusValueBounds();
        var practiceFocusRightBounds = GetPracticeFocusRightBounds();
        var simSeasonBounds = GetSimSeasonButtonBounds();

        uiRenderer.DrawText("Schedule / Training", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, 280, 22), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Franchise Date: {_franchiseSession.GetCurrentFranchiseDate():yyyy-MM-dd}", new Rectangle(LayoutMargin, 108, 320, 20), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds("Games in blue; practices and recovery in green.", new Rectangle(LayoutMargin, 130, 360, 28), Color.White, uiRenderer.UiSmallFont, 2);

        uiRenderer.DrawButton(_previousMonthButton.Label, previousMonthBounds, previousMonthBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawTextInBounds(_visibleMonth.ToString("MMMM yyyy"), monthLabelBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawButton(_nextMonthButton.Label, nextMonthBounds, nextMonthBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);

        uiRenderer.DrawTextInBounds(selectedPracticePlan != null ? "Selected Day Plan" : "Default Practice Focus", practiceFocusLabelBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawButton(_practiceFocusPreviousButton.Label, practiceFocusLeftBounds, practiceFocusLeftBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(practiceFocusLabel, practiceFocusValueBounds, practiceFocusValueBounds.Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
        uiRenderer.DrawButton(_practiceFocusNextButton.Label, practiceFocusRightBounds, practiceFocusRightBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_simSeasonButton.Label, simSeasonBounds, simSeasonBounds.Contains(mousePosition) ? Color.DarkRed : Color.Firebrick, Color.White);

        if (teamGames.Count == 0)
        {
            uiRenderer.DrawText("No schedule data found for the selected team.", new Vector2(100, 190), Color.White, uiRenderer.ScoreboardFont);
        }
        else
        {
            DrawMonthCalendar(uiRenderer, teamGames);
            DrawSelectedDayPanel(uiRenderer, teamGames, practiceFocusLabel, selectedPracticePlan != null);
        }

        uiRenderer.DrawButton(_backButton.Label, backBounds, backBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);

        if (_showSeasonSimConfirmation)
        {
            DrawSeasonSimConfirmation(uiRenderer, mousePosition);
        }
    }

    private void ChangeVisibleMonth(int monthOffset)
    {
        _visibleMonth = ClampVisibleMonth(_visibleMonth.AddMonths(monthOffset));
        if (_selectedDate.Month != _visibleMonth.Month || _selectedDate.Year != _visibleMonth.Year)
        {
            var seasonStart = GetCalendarStartDate();
            var seasonEnd = GetCalendarEndDate();
            var firstOfMonth = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
            _selectedDate = firstOfMonth < seasonStart
                ? seasonStart
                : firstOfMonth > seasonEnd
                    ? seasonEnd
                    : firstOfMonth;
        }
    }

    private void ChangeSelectedDayPracticeFocus(int direction)
    {
        var teamGames = GetScheduleRows();
        if (GetPracticePlan(_selectedDate.Date, teamGames) != null)
        {
            _statusMessage = _franchiseSession.CyclePracticeFocus(direction, _selectedDate.Date);
            return;
        }

        var defaultMessage = _franchiseSession.CyclePracticeFocus(direction);
        _statusMessage = GetGamesForDate(_selectedDate.Date, teamGames).Count > 0
            ? $"{_selectedDate:ddd, MMM d} is a game day, so {defaultMessage.ToLowerInvariant()}"
            : $"No team workout is scheduled for {_selectedDate:ddd, MMM d}, so {defaultMessage.ToLowerInvariant()}";
    }

    private void PromptSimToSeasonEnd()
    {
        _showSeasonSimConfirmation = true;
        _statusMessage = "Confirm the season sim to fast-forward through the rest of the regular season.";
    }

    private void SimToSeasonEnd()
    {
        _showSeasonSimConfirmation = false;
        _franchiseSession.SimulateToEndOfSeason(out var message);
        _statusMessage = message;
        SyncCalendarToFranchiseDate();
    }

    private void CancelSeasonSim()
    {
        _showSeasonSimConfirmation = false;
        _statusMessage = "Season sim canceled.";
    }

    private void SyncCalendarToFranchiseDate()
    {
        var franchiseDate = _franchiseSession.GetCurrentFranchiseDate().Date;
        var seasonStart = GetCalendarStartDate();
        var seasonEnd = GetCalendarEndDate();
        if (franchiseDate < seasonStart)
        {
            franchiseDate = seasonStart;
        }
        else if (franchiseDate > seasonEnd)
        {
            franchiseDate = seasonEnd;
        }

        _visibleMonth = ClampVisibleMonth(new DateTime(franchiseDate.Year, franchiseDate.Month, 1));
        _selectedDate = franchiseDate;
    }

    private bool TrySelectCalendarDay(Point mousePosition)
    {
        var gridStartDate = GetGridStartDate();
        var seasonStart = GetCalendarStartDate();
        var seasonEnd = GetCalendarEndDate();

        for (var i = 0; i < 42; i++)
        {
            var bounds = GetDayCellBounds(i);
            if (!bounds.Contains(mousePosition))
            {
                continue;
            }

            var selectedDate = gridStartDate.AddDays(i).Date;
            if (selectedDate < seasonStart || selectedDate > seasonEnd)
            {
                return false;
            }

            _selectedDate = selectedDate;
            if (selectedDate.Month != _visibleMonth.Month || selectedDate.Year != _visibleMonth.Year)
            {
                _visibleMonth = ClampVisibleMonth(new DateTime(selectedDate.Year, selectedDate.Month, 1));
            }

            return true;
        }

        return false;
    }

    private void DrawSeasonSimConfirmation(UiRenderer uiRenderer, Point mousePosition)
    {
        var panelBounds = GetSeasonSimPromptBounds();
        var confirmBounds = GetConfirmSimSeasonBounds();
        var cancelBounds = GetCancelSimSeasonBounds();

        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(28, 32, 40), Color.White);
        uiRenderer.DrawTextInBounds("Sim entire season?", new Rectangle(panelBounds.X + 12, panelBounds.Y + 10, panelBounds.Width - 24, 24), Color.Gold, uiRenderer.UiMediumFont, centerHorizontally: true);
        uiRenderer.DrawWrappedTextInBounds("This will fast-forward every remaining regular-season day, including scheduled games, practices, and recovery sessions.", new Rectangle(panelBounds.X + 18, panelBounds.Y + 42, panelBounds.Width - 36, 48), Color.White, uiRenderer.UiSmallFont, 3);
        uiRenderer.DrawButton(_confirmSimSeasonButton.Label, confirmBounds, confirmBounds.Contains(mousePosition) ? Color.DarkRed : Color.Firebrick, Color.White);
        uiRenderer.DrawButton(_cancelSimSeasonButton.Label, cancelBounds, cancelBounds.Contains(mousePosition) ? Color.DarkSlateGray : Color.SlateGray, Color.White);
    }

    private void DrawMonthCalendar(UiRenderer uiRenderer, IReadOnlyList<ScheduleDisplayRow> teamGames)
    {
        string[] dayHeaders = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
        for (var column = 0; column < dayHeaders.Length; column++)
        {
            var headerCellBounds = GetDayCellBounds(column);
            var headerBounds = new Rectangle(headerCellBounds.X, CalendarTop - 28, headerCellBounds.Width, 22);
            uiRenderer.DrawTextInBounds(dayHeaders[column], headerBounds, Color.Gold, uiRenderer.UiMediumFont, centerHorizontally: true);
        }

        var gridStartDate = GetGridStartDate();
        var seasonStart = GetCalendarStartDate();
        var seasonEnd = GetCalendarEndDate();

        for (var index = 0; index < 42; index++)
        {
            var date = gridStartDate.AddDays(index).Date;
            var bounds = GetDayCellBounds(index);
            var isInSeasonRange = date >= seasonStart && date <= seasonEnd;
            var games = isInSeasonRange ? GetGamesForDate(date, teamGames) : [];
            var practicePlan = isInSeasonRange ? GetPracticePlan(date, teamGames) : null;
            var isCurrentMonth = date.Month == _visibleMonth.Month && date.Year == _visibleMonth.Year;
            var isSelected = isInSeasonRange && date == _selectedDate.Date;
            var isToday = isInSeasonRange && date == _franchiseSession.GetCurrentFranchiseDate().Date;
            var isHovered = isInSeasonRange && bounds.Contains(Mouse.GetState().Position);
            var backgroundColor = isInSeasonRange
                ? GetDayBackgroundColor(games.Count > 0, practicePlan != null, isCurrentMonth, isSelected, isHovered)
                : new Color(44, 44, 44);

            uiRenderer.DrawButton(string.Empty, bounds, backgroundColor, Color.White);
            if (!isInSeasonRange)
            {
                continue;
            }

            uiRenderer.DrawTextInBounds(date.Day.ToString(), new Rectangle(bounds.X + 4, bounds.Y + 2, 30, 20), isToday ? Color.Gold : (isCurrentMonth ? Color.White : Color.LightGray), uiRenderer.UiMediumFont);

            if (games.Count > 0)
            {
                DrawCalendarCellLine(uiRenderer, Truncate(BuildGameCellLabel(games), 12), bounds, 0, Color.White);
                var scoreLine = games.Count > 1 ? $"{games.Count} G" : (games[0].Score == "-" ? "Sched" : $"F {games[0].Score}");
                DrawCalendarCellLine(uiRenderer, Truncate(scoreLine, 12), bounds, 1, Color.White);
            }
            else if (practicePlan != null)
            {
                DrawCalendarCellLine(uiRenderer, Truncate(practicePlan.CellLabel, 12), bounds, 0, Color.White);
                DrawCalendarCellLine(uiRenderer, Truncate(practicePlan.Subtitle, 12), bounds, 1, Color.White);
            }
        }
    }

    private void DrawSelectedDayPanel(UiRenderer uiRenderer, IReadOnlyList<ScheduleDisplayRow> teamGames, string practiceFocusLabel, bool isSelectedPracticeDay)
    {
        var panelBounds = GetSelectedDayPanelBounds();
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(38, 48, 56), Color.White);

        var leftPanelWidth = (int)Math.Floor(panelBounds.Width * 0.56f);
        var rightPanelX = panelBounds.X + leftPanelWidth + 18;
        var rightPanelWidth = panelBounds.Right - rightPanelX - 14;

        uiRenderer.DrawTextInBounds($"Selected Day: {_selectedDate:dddd, MMMM d}", new Rectangle(panelBounds.X + 12, panelBounds.Y + 8, leftPanelWidth - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(isSelectedPracticeDay ? $"Plan for This Day: {practiceFocusLabel}" : $"Default Plan: {practiceFocusLabel}", new Rectangle(rightPanelX, panelBounds.Y + 8, rightPanelWidth, 18), Color.White, uiRenderer.UiSmallFont);

        var lines = BuildSelectedDayLines(teamGames);
        uiRenderer.DrawWrappedTextInBounds(string.Join(" ", lines.Take(2)), new Rectangle(panelBounds.X + 14, panelBounds.Y + 32, leftPanelWidth - 26, panelBounds.Height - 42), Color.White, uiRenderer.UiSmallFont, 3);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(rightPanelX, panelBounds.Y + 32, rightPanelWidth, panelBounds.Height - 42), Color.White, uiRenderer.UiSmallFont, 3);
    }

    private List<string> BuildSelectedDayLines(IReadOnlyList<ScheduleDisplayRow> teamGames)
    {
        var games = GetGamesForDate(_selectedDate.Date, teamGames);
        if (games.Count > 0)
        {
            return games
                .SelectMany(game => new[]
                {
                    $"{(game.IsHomeGame ? "vs" : "@")} {game.Opponent}",
                    $"{(game.IsHomeGame ? "Home" : "Away")} | Gm {game.GameNumber ?? 1} | {(game.Score == "-" ? "Sched" : $"Score {game.Score}")}"
                })
                .ToList();
        }

        var practicePlan = GetPracticePlan(_selectedDate.Date, teamGames);
        if (practicePlan != null)
        {
            return
            [
                practicePlan.Title,
                practicePlan.Details
            ];
        }

        return ["Open day: no scheduled game or full-team practice is listed for this date."];
    }

    private PracticePlan? GetPracticePlan(DateTime date, IReadOnlyList<ScheduleDisplayRow> teamGames)
    {
        if (teamGames.Count == 0 || GetGamesForDate(date, teamGames).Count > 0)
        {
            return null;
        }

        var firstGameDate = teamGames.Min(game => game.Date.Date);
        var lastGameDate = teamGames.Max(game => game.Date.Date);
        var springTrainingStart = _franchiseSession.GetSpringTrainingStartDate().Date;
        if (date.Date < springTrainingStart || date.Date > lastGameDate)
        {
            return null;
        }

        var practiceFocus = _franchiseSession.GetPracticeFocus(date);
        var practiceFocusLabel = FranchiseSession.GetPracticeFocusLabel(practiceFocus);
        var hasCustomPracticeFocus = _franchiseSession.HasCustomPracticeFocus(date);

        if (date.Date < firstGameDate)
        {
            return new PracticePlan(
                "Spring Work",
                practiceFocusLabel,
                $"Spring Training - {practiceFocusLabel}",
                $"Preseason reps centered on {practiceFocusLabel.ToLowerInvariant()} and team conditioning.",
                new Color(68, 104, 72));
        }

        var previousGame = teamGames.Where(game => game.Date.Date < date.Date).OrderByDescending(game => game.Date).FirstOrDefault();
        var nextGame = teamGames.Where(game => game.Date.Date > date.Date).OrderBy(game => game.Date).FirstOrDefault();
        var daysSinceLastGame = previousGame == null ? 99 : (date.Date - previousGame.Date.Date).Days;
        var daysUntilNextGame = nextGame == null ? 99 : (nextGame.Date.Date - date.Date).Days;

        if (practiceFocus == TeamPracticeFocus.Recovery || (!hasCustomPracticeFocus && daysSinceLastGame == 1 && daysUntilNextGame == 1))
        {
            return new PracticePlan(
                "Recovery",
                "Light day",
                "Recovery / Film Day",
                "The club is taking a lighter MLB-style workload today with treatment, film review, and maintenance work.",
                new Color(74, 110, 74));
        }

        var lightWorkout = daysSinceLastGame == 1 || daysUntilNextGame == 1;
        return new PracticePlan(
            lightWorkout ? "Practice" : "Full Practice",
            practiceFocusLabel,
            lightWorkout ? $"Light {practiceFocusLabel} Practice" : $"{practiceFocusLabel} Team Practice",
            FranchiseSession.GetPracticeFocusDescription(practiceFocus, lightWorkout),
            lightWorkout ? new Color(64, 100, 68) : new Color(54, 92, 60));
    }

    private static Color GetDayBackgroundColor(bool hasGame, bool hasPractice, bool isCurrentMonth, bool isSelected, bool isHovered)
    {
        if (!isCurrentMonth)
        {
            return isSelected ? new Color(115, 96, 30) : new Color(54, 54, 54);
        }

        if (hasGame)
        {
            return isSelected ? Color.CornflowerBlue : (isHovered ? Color.SteelBlue : Color.DarkSlateBlue);
        }

        if (hasPractice)
        {
            return isSelected ? Color.ForestGreen : (isHovered ? Color.OliveDrab : Color.DarkOliveGreen);
        }

        return isSelected ? new Color(120, 96, 32) : (isHovered ? Color.DimGray : new Color(76, 76, 76));
    }

    private void DrawCalendarCellLine(UiRenderer uiRenderer, string text, Rectangle bounds, int lineIndex, Color color)
    {
        var topOffset = Math.Clamp(bounds.Height / 3, 20, 30);
        var lineHeight = Math.Clamp(bounds.Height / 4, 16, 22);
        var y = bounds.Y + topOffset + lineIndex * (lineHeight + 2);
        var lineBounds = new Rectangle(bounds.X + 4, y, bounds.Width - 8, lineHeight);
        uiRenderer.DrawTextInBounds(text, lineBounds, color, uiRenderer.UiSmallFont, centerHorizontally: true);
    }

    private string BuildGameCellLabel(IReadOnlyList<ScheduleDisplayRow> games)
    {
        if (games.Count > 1)
        {
            var firstGame = games[0];
            return $"DH {(firstGame.IsHomeGame ? "vs" : "@")} {GetTeamAbbreviation(firstGame.Opponent)}";
        }

        var game = games[0];
        return $"{(game.IsHomeGame ? "vs" : "@")} {GetTeamAbbreviation(game.Opponent)}";
    }
    private string GetTeamAbbreviation(string teamName)
    {
        var team = _leagueData.Teams.FirstOrDefault(candidate => string.Equals(candidate.Name, teamName, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(team?.Abbreviation)
            ? team.Abbreviation
            : Truncate(teamName, 3).ToUpperInvariant();
    }

    private List<ScheduleDisplayRow> GetGamesForDate(DateTime date, IReadOnlyList<ScheduleDisplayRow> teamGames)
    {
        return teamGames
            .Where(game => game.Date.Date == date.Date)
            .OrderBy(game => game.GameNumber ?? 1)
            .ToList();
    }

    private DateTime GetGridStartDate()
    {
        var monthStart = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
        return monthStart.AddDays(-(int)monthStart.DayOfWeek).Date;
    }

    private DateTime GetCalendarStartDate()
    {
        return _franchiseSession.GetSeasonCalendarStartDate().Date;
    }

    private DateTime GetCalendarEndDate()
    {
        return _franchiseSession.GetSeasonCalendarEndDate().Date;
    }

    private DateTime ClampVisibleMonth(DateTime month)
    {
        var candidate = new DateTime(month.Year, month.Month, 1);
        var minMonth = new DateTime(GetCalendarStartDate().Year, GetCalendarStartDate().Month, 1);
        var maxMonth = new DateTime(GetCalendarEndDate().Year, GetCalendarEndDate().Month, 1);

        if (candidate < minMonth)
        {
            return minMonth;
        }

        if (candidate > maxMonth)
        {
            return maxMonth;
        }

        return candidate;
    }

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private Rectangle GetPreviousMonthBounds() => new(Math.Max(LayoutMargin + 720, _viewport.X - 420), 40, 52, 36);

    private Rectangle GetNextMonthBounds() => new(Math.Max(LayoutMargin + 980, _viewport.X - 160), 40, 52, 36);

    private Rectangle GetMonthLabelBounds()
    {
        var previousBounds = GetPreviousMonthBounds();
        var nextBounds = GetNextMonthBounds();
        return new Rectangle(previousBounds.Right + 12, 42, Math.Max(160, nextBounds.X - previousBounds.Right - 24), 30);
    }

    private Rectangle GetPracticeFocusLeftBounds()
    {
        var centerX = _viewport.X / 2;
        return new Rectangle(centerX - 156, 100, 44, 36);
    }

    private Rectangle GetPracticeFocusValueBounds()
    {
        var centerX = _viewport.X / 2;
        return new Rectangle(centerX - 104, 100, 208, 36);
    }

    private Rectangle GetPracticeFocusRightBounds()
    {
        var centerX = _viewport.X / 2;
        return new Rectangle(centerX + 112, 100, 44, 36);
    }

    private Rectangle GetPracticeFocusLabelBounds()
    {
        var valueBounds = GetPracticeFocusValueBounds();
        return new Rectangle(valueBounds.X - 80, 78, valueBounds.Width + 160, 18);
    }

    private Rectangle GetSimSeasonButtonBounds()
    {
        return new Rectangle(Math.Max(LayoutMargin + 840, _viewport.X - 220), 144, 160, 36);
    }

    private Rectangle GetSeasonSimPromptBounds()
    {
        var width = Math.Min(560, Math.Max(420, _viewport.X - 220));
        var height = 138;
        return new Rectangle((_viewport.X - width) / 2, Math.Max(170, (_viewport.Y - height) / 2), width, height);
    }

    private Rectangle GetConfirmSimSeasonBounds()
    {
        var panelBounds = GetSeasonSimPromptBounds();
        return new Rectangle(panelBounds.X + 56, panelBounds.Bottom - 44, 180, 30);
    }

    private Rectangle GetCancelSimSeasonBounds()
    {
        var panelBounds = GetSeasonSimPromptBounds();
        return new Rectangle(panelBounds.Right - 236, panelBounds.Bottom - 44, 180, 30);
    }

    private int GetCellWidth()
    {
        var availableWidth = Math.Max(980, _viewport.X - (LayoutMargin * 2));
        return Math.Max(140, (availableWidth - (CalendarGap * 6)) / 7);
    }

    private int GetCellHeight()
    {
        var availableHeight = Math.Max(336, _viewport.Y - CalendarTop - GetSelectedDayPanelHeight() - 48);
        return Math.Clamp((availableHeight - (CalendarGap * 5)) / 6, 56, 86);
    }

    private int GetSelectedDayPanelHeight()
    {
        return Math.Clamp((int)Math.Round(_viewport.Y * 0.17), 110, 144);
    }

    private Rectangle GetSelectedDayPanelBounds()
    {
        var top = CalendarTop + (GetCellHeight() * 6) + (CalendarGap * 5) + 18;
        return new Rectangle(LayoutMargin, top, Math.Max(1000, _viewport.X - (LayoutMargin * 2)), GetSelectedDayPanelHeight());
    }

    private Rectangle GetDayCellBounds(int index)
    {
        var column = index % 7;
        var row = index / 7;
        var x = LayoutMargin + column * (GetCellWidth() + CalendarGap);
        var y = CalendarTop + row * (GetCellHeight() + CalendarGap);
        return new Rectangle(x, y, GetCellWidth(), GetCellHeight());
    }

    private List<ScheduleDisplayRow> GetScheduleRows()
    {
        if (_franchiseSession.SelectedTeam == null)
        {
            return new List<ScheduleDisplayRow>();
        }

        return _leagueData.Schedule
            .Where(game => string.Equals(game.HomeTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(game.AwayTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(game => game.Date)
            .ThenBy(game => game.GameNumber)
            .Select(game => new ScheduleDisplayRow(
                game.Date,
                string.Equals(game.HomeTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase) ? game.AwayTeamName : game.HomeTeamName,
                string.Equals(game.HomeTeamName, _franchiseSession.SelectedTeam.Name, StringComparison.OrdinalIgnoreCase),
                game.GameNumber,
                _franchiseSession.GetScheduledGameScore(game)))
            .ToList();
    }

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

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record ScheduleDisplayRow(DateTime Date, string Opponent, bool IsHomeGame, int? GameNumber, string Score);

    private sealed record PracticePlan(string CellLabel, string Subtitle, string Title, string Details, Color CellColor);
}
