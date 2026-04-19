using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class TrainingReportsScreen : GameScreen
{
    private enum ReportsViewMode
    {
        Reports,
        History
    }

    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _reportsTabButton;
    private readonly ButtonControl _historyTabButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private int _selectedIndex;
    private int _pageIndex;
    private ReportsViewMode _viewMode = ReportsViewMode.Reports;

    public TrainingReportsScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _reportsTabButton = new ButtonControl { Label = "Reports", OnClick = () => SetViewMode(ReportsViewMode.Reports) };
        _historyTabButton = new ButtonControl { Label = "Recent Classes", OnClick = () => SetViewMode(ReportsViewMode.History) };
        _previousPageButton = new ButtonControl { Label = "<" };
        _nextPageButton = new ButtonControl { Label = ">" };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _selectedIndex = 0;
        _pageIndex = 0;
        _viewMode = ReportsViewMode.Reports;
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
            if (GetBackButtonBounds().Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetReportsTabBounds().Contains(mousePosition))
            {
                _reportsTabButton.Click();
            }
            else if (GetHistoryTabBounds().Contains(mousePosition))
            {
                _historyTabButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(mousePosition))
            {
                _pageIndex = Math.Max(0, _pageIndex - 1);
            }
            else if (GetNextPageBounds().Contains(mousePosition))
            {
                var itemCount = _viewMode == ReportsViewMode.Reports
                    ? _franchiseSession.GetTrainingReportsForCurrentSeason().Count
                    : _franchiseSession.GetRecentDraftClasses().Count;
                var maxPage = Math.Max(0, (itemCount - 1) / GetPageSize());
                _pageIndex = Math.Min(maxPage, _pageIndex + 1);
            }
            else if (_viewMode == ReportsViewMode.Reports && TrySelectReport(mousePosition))
            {
            }
            else if (_viewMode == ReportsViewMode.History && TrySelectDraftClass(mousePosition))
            {
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);
        var reports = _franchiseSession.GetTrainingReportsForCurrentSeason();
        var recentClasses = _franchiseSession.GetRecentDraftClasses();
        EnsureSelectionIsValid(_viewMode == ReportsViewMode.Reports ? reports.Count : recentClasses.Count);

        var mousePosition = Mouse.GetState().Position;
        var seasonYear = _franchiseSession.GetCurrentTrainingReportSeason();

        uiRenderer.DrawText("Reports", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"{seasonYear} Season | {_franchiseSession.SelectedTeamName}", new Rectangle(168, 82, 420, 22), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_viewMode == ReportsViewMode.Reports
            ? "Practice updates, offseason summaries, trades, and contract activity for the current cycle are saved here."
            : "Recent draft classes are archived here so you can review how each class is progressing through the organization.", new Rectangle(48, 112, Math.Max(560, _viewport.X - 96), 40), Color.White, uiRenderer.UiSmallFont, 2);

        var listBounds = GetListPanelBounds();
        var detailBounds = GetDetailPanelBounds();
        uiRenderer.DrawButton(string.Empty, listBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, detailBounds, new Color(38, 48, 56), Color.White);
        DrawTabs(uiRenderer, mousePosition);

        uiRenderer.DrawTextInBounds(_viewMode == ReportsViewMode.Reports ? "Saved Reports" : "Draft Classes", new Rectangle(listBounds.X + 12, listBounds.Y + 8, listBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(_viewMode == ReportsViewMode.Reports ? "Selected Report" : "Class Detail", new Rectangle(detailBounds.X + 12, detailBounds.Y + 8, detailBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);

        if (_viewMode == ReportsViewMode.Reports && reports.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds("No reports are saved right now. Practice days and offseason rollover will add items here automatically.", new Rectangle(listBounds.X + 12, listBounds.Y + 34, listBounds.Width - 24, listBounds.Height - 46), Color.White, uiRenderer.UiSmallFont, 5);
        }
        else if (_viewMode == ReportsViewMode.History && recentClasses.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds("No recent draft classes are archived yet. Complete a draft and sign that class to start building history.", new Rectangle(listBounds.X + 12, listBounds.Y + 34, listBounds.Width - 24, listBounds.Height - 46), Color.White, uiRenderer.UiSmallFont, 5);
        }
        else if (_viewMode == ReportsViewMode.Reports)
        {
            DrawReportList(uiRenderer, reports, mousePosition);
            DrawSelectedReport(uiRenderer, reports[_selectedIndex]);
        }
        else
        {
            DrawDraftClassList(uiRenderer, recentClasses, mousePosition);
            DrawSelectedDraftClass(uiRenderer, recentClasses[_selectedIndex]);
        }

        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private void DrawTabs(UiRenderer uiRenderer, Point mousePosition)
    {
        var reportsBounds = GetReportsTabBounds();
        var historyBounds = GetHistoryTabBounds();
        var reportsActive = _viewMode == ReportsViewMode.Reports;
        var historyActive = _viewMode == ReportsViewMode.History;
        uiRenderer.DrawButton(_reportsTabButton.Label, reportsBounds, reportsActive ? Color.DarkOliveGreen : (reportsBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray), Color.White);
        uiRenderer.DrawButton(_historyTabButton.Label, historyBounds, historyActive ? Color.DarkOliveGreen : (historyBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray), Color.White);
    }

    private void DrawReportList(UiRenderer uiRenderer, IReadOnlyList<TrainingReportView> reports, Point mousePosition)
    {
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleReports = reports.Skip(startIndex).Take(pageSize).ToList();

        for (var i = 0; i < visibleReports.Count; i++)
        {
            var reportIndex = startIndex + i;
            var bounds = GetReportRowBounds(i);
            var isSelected = reportIndex == _selectedIndex;
            var isHovered = bounds.Contains(mousePosition);
            var background = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DimGray : new Color(54, 62, 70));
            uiRenderer.DrawButton(string.Empty, bounds, background, Color.White);

            var rowLabel = $"{visibleReports[i].ReportDate:MMM d} - {visibleReports[i].FocusLabel}";
            uiRenderer.DrawTextInBounds(rowLabel, new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 16, 16), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(visibleReports[i].Title, new Rectangle(bounds.X + 8, bounds.Y + 20, bounds.Width - 16, 18), Color.White, uiRenderer.UiSmallFont);
        }

        var maxPage = Math.Max(0, (reports.Count - 1) / pageSize);
        uiRenderer.DrawButton(_previousPageButton.Label, GetPreviousPageBounds(), _pageIndex > 0 && GetPreviousPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, GetNextPageBounds(), _pageIndex < maxPage && GetNextPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawTextInBounds($"Page {_pageIndex + 1}/{maxPage + 1}", new Rectangle(GetPreviousPageBounds().Right + 8, GetPreviousPageBounds().Y + 4, 110, 20), Color.White, uiRenderer.UiSmallFont);
    }

    private void DrawDraftClassList(UiRenderer uiRenderer, IReadOnlyList<DraftClassHistoryView> classes, Point mousePosition)
    {
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleClasses = classes.Skip(startIndex).Take(pageSize).ToList();

        for (var i = 0; i < visibleClasses.Count; i++)
        {
            var classIndex = startIndex + i;
            var bounds = GetReportRowBounds(i);
            var isSelected = classIndex == _selectedIndex;
            var background = isSelected ? Color.DarkOliveGreen : (bounds.Contains(mousePosition) ? Color.DimGray : new Color(54, 62, 70));
            uiRenderer.DrawButton(string.Empty, bounds, background, Color.White);

            var classView = visibleClasses[i];
            uiRenderer.DrawTextInBounds(classView.Title, new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 16, 16), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{classView.TotalPlayers} player(s) | {classView.FortyManCount} on 40-man", new Rectangle(bounds.X + 8, bounds.Y + 20, bounds.Width - 16, 18), Color.White, uiRenderer.UiSmallFont);
        }

        var maxPage = Math.Max(0, (classes.Count - 1) / pageSize);
        uiRenderer.DrawButton(_previousPageButton.Label, GetPreviousPageBounds(), _pageIndex > 0 && GetPreviousPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, GetNextPageBounds(), _pageIndex < maxPage && GetNextPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawTextInBounds($"Page {_pageIndex + 1}/{maxPage + 1}", new Rectangle(GetPreviousPageBounds().Right + 8, GetPreviousPageBounds().Y + 4, 110, 20), Color.White, uiRenderer.UiSmallFont);
    }

    private void DrawSelectedReport(UiRenderer uiRenderer, TrainingReportView report)
    {
        var contentBounds = new Rectangle(GetDetailPanelBounds().X + 12, GetDetailPanelBounds().Y + 34, GetDetailPanelBounds().Width - 24, GetDetailPanelBounds().Height - 46);
        uiRenderer.DrawTextInBounds(report.Title, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Date: {report.ReportDate:dddd, MMM d, yyyy} | Focus: {report.FocusLabel}", new Rectangle(contentBounds.X, contentBounds.Y + 22, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(report.Summary, new Rectangle(contentBounds.X, contentBounds.Y + 46, contentBounds.Width, 42), Color.White, uiRenderer.UiSmallFont, 3);

        var notesY = contentBounds.Y + 92;
        foreach (var note in report.CoachNotes.Take(8))
        {
            uiRenderer.DrawWrappedTextInBounds($"• {note}", new Rectangle(contentBounds.X, notesY, contentBounds.Width, 48), Color.White, uiRenderer.UiSmallFont, 3);
            notesY += 50;
        }
    }

    private void DrawSelectedDraftClass(UiRenderer uiRenderer, DraftClassHistoryView draftClass)
    {
        var contentBounds = new Rectangle(GetDetailPanelBounds().X + 12, GetDetailPanelBounds().Y + 34, GetDetailPanelBounds().Width - 24, GetDetailPanelBounds().Height - 46);
        uiRenderer.DrawTextInBounds(draftClass.Title, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Signed: {draftClass.TotalPlayers} | 40-Man: {draftClass.FortyManCount} | Depth: {draftClass.OrganizationCount} | Affiliate: {draftClass.AffiliateCount}", new Rectangle(contentBounds.X, contentBounds.Y + 22, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(draftClass.Summary, new Rectangle(contentBounds.X, contentBounds.Y + 46, contentBounds.Width, 42), Color.White, uiRenderer.UiSmallFont, 3);

        var playersY = contentBounds.Y + 96;
        foreach (var player in draftClass.Players.Take(9))
        {
            var detail = $"{player.PlayerName} | {player.PrimaryPosition}/{player.SecondaryPosition}".TrimEnd('/');
            var status = $"Age {player.Age} | OVR {player.OverallRating} | POT {player.PotentialRating} | {player.AssignmentLabel}";
            uiRenderer.DrawTextInBounds(detail, new Rectangle(contentBounds.X, playersY, contentBounds.Width, 16), player.IsOnFortyMan ? Color.Gold : Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(status, new Rectangle(contentBounds.X + 8, playersY + 16, contentBounds.Width - 8, 16), Color.White, uiRenderer.UiSmallFont);
            playersY += 40;
        }
    }

    private bool TrySelectReport(Point mousePosition)
    {
        var reports = _franchiseSession.GetTrainingReportsForCurrentSeason();
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleCount = Math.Min(pageSize, Math.Max(0, reports.Count - startIndex));

        for (var i = 0; i < visibleCount; i++)
        {
            if (!GetReportRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedIndex = startIndex + i;
            return true;
        }

        return false;
    }

    private bool TrySelectDraftClass(Point mousePosition)
    {
        var classes = _franchiseSession.GetRecentDraftClasses();
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleCount = Math.Min(pageSize, Math.Max(0, classes.Count - startIndex));

        for (var i = 0; i < visibleCount; i++)
        {
            if (!GetReportRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedIndex = startIndex + i;
            return true;
        }

        return false;
    }

    private void EnsureSelectionIsValid(int reportCount)
    {
        if (reportCount <= 0)
        {
            _selectedIndex = 0;
            _pageIndex = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, reportCount - 1);
        _pageIndex = Math.Clamp(_pageIndex, 0, Math.Max(0, (reportCount - 1) / GetPageSize()));
    }

    private void SetViewMode(ReportsViewMode mode)
    {
        if (_viewMode == mode)
        {
            return;
        }

        _viewMode = mode;
        _selectedIndex = 0;
        _pageIndex = 0;
    }

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private Rectangle GetReportsTabBounds() => new(48, 154, 110, 28);

    private Rectangle GetHistoryTabBounds()
    {
        var reportsTabBounds = GetReportsTabBounds();
        return new Rectangle(reportsTabBounds.Right + 10, reportsTabBounds.Y, 150, reportsTabBounds.Height);
    }

    private Rectangle GetListPanelBounds() => new(48, 190, Math.Clamp(_viewport.X / 3, 320, 420), Math.Max(330, _viewport.Y - 250));

    private Rectangle GetDetailPanelBounds()
    {
        var listBounds = GetListPanelBounds();
        return new Rectangle(listBounds.Right + 18, listBounds.Y, Math.Max(520, _viewport.X - listBounds.Right - 66), listBounds.Height);
    }

    private int GetPageSize()
    {
        return Math.Max(4, (GetListPanelBounds().Height - 88) / 46);
    }

    private Rectangle GetReportRowBounds(int visibleIndex)
    {
        var listBounds = GetListPanelBounds();
        return new Rectangle(listBounds.X + 10, listBounds.Y + 34 + (visibleIndex * 46), listBounds.Width - 20, 40);
    }

    private Rectangle GetPreviousPageBounds()
    {
        var listBounds = GetListPanelBounds();
        return new Rectangle(listBounds.X + 10, listBounds.Bottom - 40, 34, 28);
    }

    private Rectangle GetNextPageBounds()
    {
        var previous = GetPreviousPageBounds();
        return new Rectangle(previous.Right + 126, previous.Y, 34, 28);
    }
}
