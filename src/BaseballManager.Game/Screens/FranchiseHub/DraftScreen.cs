using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using BaseballManager.Game.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class DraftScreen : GameScreen
{
    private enum DraftSortMode
    {
        Report,
        Potential,
        Position
    }

    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _startDraftButton;
    private readonly ButtonControl _nextPickButton;
    private readonly ButtonControl _simRoundButton;
    private readonly ButtonControl _simDraftButton;
    private readonly ButtonControl _makePickButton;
    private readonly ButtonControl _sortButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private readonly PlayerContextOverlay _playerContextOverlay = new();
    private int _selectedIndex;
    private int _pageIndex;
    private DraftSortMode _sortMode = DraftSortMode.Report;
    private string _statusMessage = "Finish the regular season, then start the draft and scout the board before making your selections.";

    public DraftScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl { Label = "Back", OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen)) };
        _startDraftButton = new ButtonControl { Label = "Start Draft", OnClick = StartDraft };
        _nextPickButton = new ButtonControl { Label = "Next Pick", OnClick = AdvanceToNextUserPick };
        _simRoundButton = new ButtonControl { Label = "Sim Round", OnClick = SimRound };
        _simDraftButton = new ButtonControl { Label = "Sim Draft", OnClick = SimDraft };
        _makePickButton = new ButtonControl { Label = "Draft Player", OnClick = MakePick };
        _sortButton = new ButtonControl { Label = "Sort: Report", OnClick = CycleSortMode };
        _previousPageButton = new ButtonControl { Label = "Prev", OnClick = () => _pageIndex = Math.Max(0, _pageIndex - 1) };
        _nextPageButton = new ButtonControl { Label = "Next", OnClick = () => _pageIndex++ };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _selectedIndex = 0;
        _pageIndex = 0;
        _playerContextOverlay.Close();
        _statusMessage = _franchiseSession.HasActiveDraft()
            ? "Review the board, draft manually when you are on the clock, or use Next Pick, Sim Round, and Sim Draft to fast-forward the room."
            : _franchiseSession.CanStartDraft()
                ? "The season is complete. Start the draft to build your next prospect class."
            : _franchiseSession.GetDraftOrganizationPlayers().Count > 0
                ? "Review your drafted classes here. Use the roster menu to manage 40-man and affiliate assignments, then return to this screen when a new draft opens."
                : _franchiseSession.GetNextSeasonBlockerMessage();
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;
        var hasActiveDraft = _franchiseSession.HasActiveDraft();
        var hasOrganizationPlayers = _franchiseSession.GetDraftOrganizationPlayers().Count > 0;
        if (_ignoreClicksUntilRelease)
        {
            if (currentMouseState.LeftButton == ButtonState.Released)
            {
                _ignoreClicksUntilRelease = false;
            }

            _previousMouseState = currentMouseState;
            return;
        }

        var isLeftPress = _previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed;
        var isRightPress = _previousMouseState.RightButton == ButtonState.Released && currentMouseState.RightButton == ButtonState.Pressed;

        if (isLeftPress)
        {
            var mousePosition = currentMouseState.Position;
            if (_playerContextOverlay.HandleLeftClick(mousePosition, _viewport, out var contextAction))
            {
                if (contextAction.HasValue)
                {
                    ExecuteContextAction(contextAction.Value);
                }

                _previousMouseState = currentMouseState;
                return;
            }

            if (GetBackButtonBounds().Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetStartDraftButtonBounds().Contains(mousePosition) && !hasActiveDraft && _franchiseSession.CanStartDraft())
            {
                _startDraftButton.Click();
            }
            else if (GetNextPickButtonBounds().Contains(mousePosition) && hasActiveDraft)
            {
                _nextPickButton.Click();
            }
            else if (GetSimRoundButtonBounds().Contains(mousePosition) && hasActiveDraft)
            {
                _simRoundButton.Click();
            }
            else if (GetSimDraftButtonBounds().Contains(mousePosition) && hasActiveDraft)
            {
                _simDraftButton.Click();
            }
            else if (GetMakePickButtonBounds().Contains(mousePosition) && _franchiseSession.HasActiveDraft())
            {
                _makePickButton.Click();
            }
            else if (GetSortButtonBounds().Contains(mousePosition) && _franchiseSession.HasActiveDraft())
            {
                _sortButton.Click();
            }
            else if (GetSortButtonBounds().Contains(mousePosition) && !hasActiveDraft && hasOrganizationPlayers)
            {
                _sortButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(mousePosition) && (hasActiveDraft || hasOrganizationPlayers))
            {
                _previousPageButton.Click();
            }
            else if (GetNextPageBounds().Contains(mousePosition) && (hasActiveDraft || hasOrganizationPlayers))
            {
                _nextPageButton.Click();
            }
            else if (hasActiveDraft)
            {
                TrySelectProspect(mousePosition);
            }
            else if (hasOrganizationPlayers)
            {
                TrySelectDraftedPlayer(mousePosition);
            }
        }

        if (isRightPress)
        {
            var mousePosition = currentMouseState.Position;
            if (!TryOpenPlayerContextMenu(mousePosition))
            {
                _playerContextOverlay.Close();
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);
        var mousePosition = Mouse.GetState().Position;
        var board = _franchiseSession.GetDraftBoard();
        var sortedProspects = GetSortedProspects(board.AvailableProspects);
        var organizationPlayers = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
        EnsureSelectionIsValid(board.HasActiveDraft ? sortedProspects.Count : organizationPlayers.Count);

        uiRenderer.DrawText("Amateur Draft", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, 360, 22), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(48, 112, Math.Max(640, _viewport.X - 96), 42), Color.White, uiRenderer.UiSmallFont, 2);

        var boardBounds = GetProspectPanelBounds();
        var summaryBounds = GetSummaryPanelBounds();
        uiRenderer.DrawButton(string.Empty, boardBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, summaryBounds, new Color(38, 48, 56), Color.White);

        uiRenderer.DrawTextInBounds(board.HasActiveDraft ? "Available Prospects" : "Drafted Players", new Rectangle(boardBounds.X + 12, boardBounds.Y + 8, boardBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(board.HasActiveDraft ? "Draft Summary" : "Player Summary", new Rectangle(summaryBounds.X + 12, summaryBounds.Y + 8, summaryBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);

        if (!board.HasActiveDraft)
        {
            if (organizationPlayers.Count > 0)
            {
                DrawOrganizationList(uiRenderer, organizationPlayers, mousePosition);
                DrawOrganizationSummaryPanel(uiRenderer, organizationPlayers);
            }
            else
            {
                uiRenderer.DrawWrappedTextInBounds(
                    _franchiseSession.CanStartDraft()
                        ? "Your draft room is ready. Press Start Draft to generate a prospect pool and begin the board."
                        : _franchiseSession.GetNextSeasonBlockerMessage(),
                    new Rectangle(boardBounds.X + 12, boardBounds.Y + 40, boardBounds.Width - 24, 80),
                    Color.White,
                    uiRenderer.UiSmallFont,
                    4);
            }
        }
        else
        {
            DrawProspectList(uiRenderer, sortedProspects, mousePosition);
            DrawSummaryPanel(uiRenderer, board, sortedProspects);
        }

        DrawButtons(uiRenderer, mousePosition, board);
        _playerContextOverlay.Draw(uiRenderer, mousePosition, _viewport);
    }

    private void DrawProspectList(UiRenderer uiRenderer, IReadOnlyList<DraftProspectView> prospects, Point mousePosition)
    {
        if (prospects.Count == 0)
        {
            uiRenderer.DrawTextInBounds("No prospects remain on the board.", new Rectangle(GetProspectPanelBounds().X + 12, GetProspectPanelBounds().Y + 42, GetProspectPanelBounds().Width - 24, 20), Color.White, uiRenderer.UiSmallFont);
            return;
        }

        var pageSize = GetPageSize();
        var maxPage = Math.Max(0, (int)Math.Ceiling(prospects.Count / (double)pageSize) - 1);
        _pageIndex = Math.Clamp(_pageIndex, 0, maxPage);
        var startIndex = _pageIndex * pageSize;
        var visibleProspects = prospects.Skip(startIndex).Take(pageSize).ToList();

        for (var i = 0; i < visibleProspects.Count; i++)
        {
            var absoluteIndex = startIndex + i;
            var row = visibleProspects[i];
            var bounds = GetProspectRowBounds(i);
            var isSelected = absoluteIndex == _selectedIndex;
            var background = isSelected ? Color.DarkOliveGreen : (bounds.Contains(mousePosition) ? Color.DimGray : new Color(54, 62, 70));
            uiRenderer.DrawButton(string.Empty, bounds, background, Color.White);
            uiRenderer.DrawTextInBounds($"{row.PlayerName} | {row.PrimaryPosition}/{row.SecondaryPosition}".TrimEnd('/'), new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 180, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{row.PotentialSummary} | Age {row.Age}", new Rectangle(bounds.Right - 224, bounds.Y + 4, 216, 16), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds($"{row.Source} | {row.ScoutSummary}", new Rectangle(bounds.X + 8, bounds.Y + 22, bounds.Width - 16, 14), Color.White, uiRenderer.UiSmallFont);
        }

        uiRenderer.DrawButton(_previousPageButton.Label, GetPreviousPageBounds(), _pageIndex > 0 && GetPreviousPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, GetNextPageBounds(), _pageIndex < maxPage && GetNextPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawTextInBounds($"Page {_pageIndex + 1}/{maxPage + 1}", new Rectangle(GetPreviousPageBounds().Right + 8, GetPreviousPageBounds().Y + 4, 112, 18), Color.White, uiRenderer.UiSmallFont);
    }

    private void DrawSummaryPanel(UiRenderer uiRenderer, DraftBoardView board, IReadOnlyList<DraftProspectView> prospects)
    {
        var summaryBounds = GetSummaryPanelBounds();
        var contentBounds = new Rectangle(summaryBounds.X + 12, summaryBounds.Y + 36, summaryBounds.Width - 24, summaryBounds.Height - 48);
        var teamOnClock = string.IsNullOrWhiteSpace(board.CurrentTeamName) ? "Draft Complete" : board.CurrentTeamName;
        var selectedProspect = prospects.Count == 0 || _selectedIndex < 0 || _selectedIndex >= prospects.Count ? null : prospects[_selectedIndex];
        uiRenderer.DrawTextInBounds($"Round {board.CurrentRound} | Pick {board.CurrentPickNumber}", new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"On The Clock: {teamOnClock}", new Rectangle(contentBounds.X, contentBounds.Y + 24, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds($"Order: {string.Join(" -> ", board.CurrentRoundOrder)}", new Rectangle(contentBounds.X, contentBounds.Y + 48, contentBounds.Width, 54), Color.White, uiRenderer.UiSmallFont, 3);

        if (selectedProspect != null)
        {
            uiRenderer.DrawTextInBounds(selectedProspect.PlayerName, new Rectangle(contentBounds.X, contentBounds.Y + 112, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{selectedProspect.PrimaryPosition}/{selectedProspect.SecondaryPosition} | {selectedProspect.Source}", new Rectangle(contentBounds.X, contentBounds.Y + 136, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds(selectedProspect.ScoutSummary, new Rectangle(contentBounds.X, contentBounds.Y + 160, contentBounds.Width, 44), Color.White, uiRenderer.UiSmallFont, 2);
            uiRenderer.DrawTextInBounds($"Ceiling: {selectedProspect.PotentialSummary}", new Rectangle(contentBounds.X, contentBounds.Y + 208, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"Source: {selectedProspect.SourceTeamName}", new Rectangle(contentBounds.X, contentBounds.Y + 232, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawWrappedTextInBounds(selectedProspect.SourceStatsSummary, new Rectangle(contentBounds.X, contentBounds.Y + 256, contentBounds.Width, 42), Color.Gold, uiRenderer.UiSmallFont, 2);
        }

        uiRenderer.DrawTextInBounds("Recent Picks", new Rectangle(contentBounds.X, contentBounds.Bottom - 208, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        var picksY = contentBounds.Bottom - 184;
        foreach (var pick in board.RecentPicks.Take(8))
        {
            uiRenderer.DrawTextInBounds($"#{pick.OverallPickNumber} R{pick.RoundNumber}-{pick.PickNumberInRound}: {pick.TeamName}", new Rectangle(contentBounds.X, picksY, contentBounds.Width, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{pick.PlayerName} ({pick.PrimaryPosition})", new Rectangle(contentBounds.X + 8, picksY + 16, contentBounds.Width - 8, 16), pick.IsUserPick ? Color.Gold : Color.White, uiRenderer.UiSmallFont);
            picksY += 38;
        }
    }

    private void DrawOrganizationList(UiRenderer uiRenderer, IReadOnlyList<DraftOrganizationPlayerView> players, Point mousePosition)
    {
        var pageSize = GetPageSize();
        var maxPage = Math.Max(0, (int)Math.Ceiling(players.Count / (double)pageSize) - 1);
        _pageIndex = Math.Clamp(_pageIndex, 0, maxPage);
        var startIndex = _pageIndex * pageSize;
        var visiblePlayers = players.Skip(startIndex).Take(pageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var absoluteIndex = startIndex + i;
            var row = visiblePlayers[i];
            var bounds = GetProspectRowBounds(i);
            var isSelected = absoluteIndex == _selectedIndex;
            var background = isSelected ? Color.DarkOliveGreen : (bounds.Contains(mousePosition) ? Color.DimGray : new Color(54, 62, 70));
            uiRenderer.DrawButton(string.Empty, bounds, background, Color.White);
            uiRenderer.DrawTextInBounds($"{row.PlayerName} | {row.PrimaryPosition}/{row.SecondaryPosition}".TrimEnd('/'), new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 220, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(row.PotentialSummary, new Rectangle(bounds.Right - 212, bounds.Y + 4, 204, 16), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds($"{row.Source} | {row.SourceTeamName}", new Rectangle(bounds.X + 8, bounds.Y + 22, bounds.Width - 16, 14), Color.White, uiRenderer.UiSmallFont);
        }

        uiRenderer.DrawButton(_previousPageButton.Label, GetPreviousPageBounds(), _pageIndex > 0 && GetPreviousPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, GetNextPageBounds(), _pageIndex < maxPage && GetNextPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawTextInBounds($"Page {_pageIndex + 1}/{maxPage + 1}", new Rectangle(GetPreviousPageBounds().Right + 8, GetPreviousPageBounds().Y + 4, 112, 18), Color.White, uiRenderer.UiSmallFont);
    }

    private void DrawOrganizationSummaryPanel(UiRenderer uiRenderer, IReadOnlyList<DraftOrganizationPlayerView> players)
    {
        var summaryBounds = GetSummaryPanelBounds();
        var contentBounds = new Rectangle(summaryBounds.X + 12, summaryBounds.Y + 36, summaryBounds.Width - 24, summaryBounds.Height - 48);
        var selectedPlayer = players.Count == 0 || _selectedIndex < 0 || _selectedIndex >= players.Count ? null : players[_selectedIndex];

        if (selectedPlayer == null)
        {
            uiRenderer.DrawWrappedTextInBounds("No drafted players are available to review right now.", contentBounds, Color.White, uiRenderer.UiSmallFont, 3);
            return;
        }

        uiRenderer.DrawTextInBounds(selectedPlayer.PlayerName, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"{selectedPlayer.PrimaryPosition}/{selectedPlayer.SecondaryPosition} | {selectedPlayer.Source}", new Rectangle(contentBounds.X, contentBounds.Y + 24, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(selectedPlayer.ScoutSummary, new Rectangle(contentBounds.X, contentBounds.Y + 52, contentBounds.Width, 44), Color.White, uiRenderer.UiSmallFont, 2);
        uiRenderer.DrawTextInBounds($"Ceiling: {selectedPlayer.PotentialSummary}", new Rectangle(contentBounds.X, contentBounds.Y + 102, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Source: {selectedPlayer.SourceTeamName} ({selectedPlayer.Source})", new Rectangle(contentBounds.X, contentBounds.Y + 126, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(selectedPlayer.SourceStatsSummary, new Rectangle(contentBounds.X, contentBounds.Y + 150, contentBounds.Width, 42), Color.Gold, uiRenderer.UiSmallFont, 2);
        uiRenderer.DrawWrappedTextInBounds("Use the roster menu to place drafted players on the 40-man roster, keep them in the organization, or move them to the affiliate before starting next season.", new Rectangle(contentBounds.X, contentBounds.Bottom - 76, contentBounds.Width, 60), Color.Gold, uiRenderer.UiSmallFont, 3);
    }

    private void DrawButtons(UiRenderer uiRenderer, Point mousePosition, DraftBoardView board)
    {
        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);

        var canStartDraft = !board.HasActiveDraft && _franchiseSession.CanStartDraft();
        var canSimDraft = board.HasActiveDraft && !board.IsComplete;
        var organizationPlayers = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
        var showDraftComplete = !board.HasActiveDraft && !canStartDraft && organizationPlayers.Count > 0;
        uiRenderer.DrawButton(
            showDraftComplete ? "Draft Complete" : _startDraftButton.Label,
            GetStartDraftButtonBounds(),
            canStartDraft && GetStartDraftButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canStartDraft ? Color.SlateBlue : new Color(76, 76, 76)),
            canStartDraft ? Color.White : new Color(188, 188, 188));

        var userOnClock = board.HasActiveDraft && string.Equals(board.CurrentTeamName, _franchiseSession.SelectedTeamName, StringComparison.OrdinalIgnoreCase) && !board.IsComplete;
        var canNextPick = board.HasActiveDraft && !userOnClock && board.AvailableProspects.Count > 0;
        var canSimRound = board.HasActiveDraft && board.AvailableProspects.Count > 0;
        var canDraft = board.HasActiveDraft && userOnClock && board.AvailableProspects.Count > 0;

        uiRenderer.DrawButton(
            _nextPickButton.Label,
            GetNextPickButtonBounds(),
            canNextPick && GetNextPickButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canNextPick ? Color.SlateBlue : new Color(76, 76, 76)),
            canNextPick ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(
            _simRoundButton.Label,
            GetSimRoundButtonBounds(),
            canSimRound && GetSimRoundButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canSimRound ? Color.SlateBlue : new Color(76, 76, 76)),
            canSimRound ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(
            _simDraftButton.Label,
            GetSimDraftButtonBounds(),
            canSimDraft && GetSimDraftButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canSimDraft ? Color.SlateBlue : new Color(76, 76, 76)),
            canSimDraft ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_makePickButton.Label, GetMakePickButtonBounds(), canDraft && GetMakePickButtonBounds().Contains(mousePosition) ? Color.DarkOliveGreen : (canDraft ? Color.OliveDrab : new Color(76, 76, 76)), canDraft ? Color.White : new Color(188, 188, 188));
        var canSort = board.HasActiveDraft || organizationPlayers.Count > 0;
        uiRenderer.DrawButton(GetSortLabel(), GetSortButtonBounds(), canSort && GetSortButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
    }

    private IReadOnlyList<DraftProspectView> GetSortedProspects(IReadOnlyList<DraftProspectView> prospects)
    {
        return _sortMode switch
        {
            DraftSortMode.Potential => prospects.OrderByDescending(prospect => prospect.PotentialRank).ThenByDescending(prospect => prospect.ScoutRank).ThenBy(prospect => prospect.PlayerName).ToList(),
            DraftSortMode.Position => prospects.OrderBy(prospect => prospect.PrimaryPosition).ThenByDescending(prospect => prospect.ScoutRank).ThenBy(prospect => prospect.PlayerName).ToList(),
            _ => prospects.OrderByDescending(prospect => prospect.ScoutRank).ThenByDescending(prospect => prospect.PotentialRank).ThenBy(prospect => prospect.PlayerName).ToList()
        };
    }

    private IReadOnlyList<DraftOrganizationPlayerView> GetSortedOrganizationPlayers(IReadOnlyList<DraftOrganizationPlayerView> players)
    {
        return _sortMode switch
        {
            DraftSortMode.Potential => players.OrderByDescending(player => player.PotentialRank).ThenByDescending(player => player.ScoutRank).ThenBy(player => player.PlayerName).ToList(),
            DraftSortMode.Position => players.OrderBy(player => player.PrimaryPosition).ThenByDescending(player => player.ScoutRank).ThenBy(player => player.PlayerName).ToList(),
            _ => players.OrderByDescending(player => player.RequiresRosterDecision).ThenByDescending(player => player.ScoutRank).ThenBy(player => player.PlayerName).ToList()
        };
    }

    private void CycleSortMode()
    {
        _sortMode = _sortMode switch
        {
            DraftSortMode.Report => DraftSortMode.Potential,
            DraftSortMode.Potential => DraftSortMode.Position,
            _ => DraftSortMode.Report
        };
    }

    private string GetSortLabel()
    {
        return _sortMode switch
        {
            DraftSortMode.Potential => "Sort: POT",
            DraftSortMode.Position => "Sort: POS",
            _ => "Sort: Report"
        };
    }

    private void StartDraft()
    {
        if (_franchiseSession.StartDraft(out var message))
        {
            _selectedIndex = 0;
            _pageIndex = 0;
        }

        _statusMessage = message;
    }

    private void SimDraft()
    {
        _franchiseSession.AutoDraft(out var message);
        _statusMessage = message;
    }

    private void AdvanceToNextUserPick()
    {
        _franchiseSession.AdvanceDraftToNextUserPick(out var message);
        _statusMessage = message;
    }

    private void SimRound()
    {
        _franchiseSession.SimDraftRound(out var message);
        _statusMessage = message;
    }

    private void MakePick()
    {
        var board = _franchiseSession.GetDraftBoard();
        var prospects = GetSortedProspects(board.AvailableProspects);
        if (_selectedIndex < 0 || _selectedIndex >= prospects.Count)
        {
            _statusMessage = "Select a prospect before making your pick.";
            return;
        }

        _franchiseSession.MakeDraftPick(prospects[_selectedIndex].PlayerId, out var message);
        _statusMessage = message;
    }

    private bool TrySelectProspect(Point mousePosition)
    {
        var prospects = GetSortedProspects(_franchiseSession.GetDraftBoard().AvailableProspects);
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleCount = Math.Min(pageSize, Math.Max(0, prospects.Count - startIndex));

        for (var i = 0; i < visibleCount; i++)
        {
            if (!GetProspectRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedIndex = startIndex + i;
            return true;
        }

        return false;
    }

    private bool TrySelectDraftedPlayer(Point mousePosition)
    {
        var players = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleCount = Math.Min(pageSize, Math.Max(0, players.Count - startIndex));

        for (var i = 0; i < visibleCount; i++)
        {
            if (!GetProspectRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedIndex = startIndex + i;
            return true;
        }

        return false;
    }

    private bool TryOpenPlayerContextMenu(Point mousePosition)
    {
        var board = _franchiseSession.GetDraftBoard();
        if (board.HasActiveDraft)
        {
            var prospects = GetSortedProspects(board.AvailableProspects);
            var pageSize = GetPageSize();
            var startIndex = _pageIndex * pageSize;
            var visibleCount = Math.Min(pageSize, Math.Max(0, prospects.Count - startIndex));
            for (var i = 0; i < visibleCount; i++)
            {
                if (!GetProspectRowBounds(i).Contains(mousePosition))
                {
                    continue;
                }

                var prospect = prospects[startIndex + i];
                _selectedIndex = startIndex + i;
                _playerContextOverlay.Open(
                    mousePosition,
                    prospect.PlayerName,
                    [new PlayerContextActionView(PlayerContextAction.OpenProfile, "Profile")],
                    [],
                    BuildDraftProspectProfile(prospect));
                return true;
            }

            return false;
        }

        var players = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
        var orgRosterById = _franchiseSession.GetSelectedTeamOrganizationRoster().ToDictionary(player => player.PlayerId, player => player);
        var pageSizeForPlayers = GetPageSize();
        var startPlayerIndex = _pageIndex * pageSizeForPlayers;
        var visiblePlayerCount = Math.Min(pageSizeForPlayers, Math.Max(0, players.Count - startPlayerIndex));
        for (var i = 0; i < visiblePlayerCount; i++)
        {
            if (!GetProspectRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            var player = players[startPlayerIndex + i];
            _selectedIndex = startPlayerIndex + i;
            orgRosterById.TryGetValue(player.PlayerId, out var rosterPlayer);
            var rosterActions = new List<PlayerContextActionView>
            {
                new(PlayerContextAction.AssignToFortyMan, "Add To 40-Man", rosterPlayer?.CanAssignToFortyMan == true),
                new(PlayerContextAction.AssignToTripleA, "Send To AAA", rosterPlayer != null && rosterPlayer.AffiliateLevel != MinorLeagueAffiliateLevel.TripleA),
                new(PlayerContextAction.AssignToDoubleA, "Send To AA", rosterPlayer != null && rosterPlayer.AffiliateLevel != MinorLeagueAffiliateLevel.DoubleA),
                new(PlayerContextAction.AssignToSingleA, "Send To A", rosterPlayer != null && rosterPlayer.AffiliateLevel != MinorLeagueAffiliateLevel.SingleA),
                new(PlayerContextAction.RemoveFromFortyMan, "Remove From 40-Man", rosterPlayer?.IsOnFortyMan == true),
                new(PlayerContextAction.ReleasePlayer, "Release", rosterPlayer?.CanRelease == true)
            };
            _playerContextOverlay.Open(
                mousePosition,
                player.PlayerName,
                [
                    new PlayerContextActionView(PlayerContextAction.OpenRosterAssignments, "Roster", rosterActions.Any(action => action.IsEnabled)),
                    new PlayerContextActionView(PlayerContextAction.OpenProfile, "Profile")
                ],
                rosterActions,
                _franchiseSession.GetPlayerProfile(player.PlayerId));
            return true;
        }

        return false;
    }

    private void ExecuteContextAction(PlayerContextAction action)
    {
        var board = _franchiseSession.GetDraftBoard();
        if (!board.HasActiveDraft)
        {
            var players = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
            if (_selectedIndex < 0 || _selectedIndex >= players.Count)
            {
                return;
            }

            var playerId = players[_selectedIndex].PlayerId;
            switch (action)
            {
                case PlayerContextAction.AssignToFortyMan:
                    _franchiseSession.AssignDraftPlayerTo40Man(playerId, out _statusMessage);
                    break;
                case PlayerContextAction.AssignToTripleA:
                    _franchiseSession.AssignDraftPlayerToAffiliate(playerId, MinorLeagueAffiliateLevel.TripleA, out _statusMessage);
                    break;
                case PlayerContextAction.AssignToDoubleA:
                    _franchiseSession.AssignDraftPlayerToAffiliate(playerId, MinorLeagueAffiliateLevel.DoubleA, out _statusMessage);
                    break;
                case PlayerContextAction.AssignToSingleA:
                    _franchiseSession.AssignDraftPlayerToAffiliate(playerId, MinorLeagueAffiliateLevel.SingleA, out _statusMessage);
                    break;
                case PlayerContextAction.RemoveFromFortyMan:
                    _franchiseSession.RemoveSelectedTeamPlayerFromFortyMan(playerId, out _statusMessage);
                    break;
                case PlayerContextAction.ReleasePlayer:
                    _franchiseSession.ReleaseSelectedTeamPlayer(playerId, out _statusMessage);
                    break;
            }
        }
    }

    private static PlayerProfileView BuildDraftProspectProfile(DraftProspectView prospect)
    {
        return new PlayerProfileView(
            prospect.PlayerName,
            $"{prospect.PrimaryPosition}/{prospect.SecondaryPosition} | Age {prospect.Age} | {prospect.Source}",
            [
                $"Scout rank {prospect.ScoutRank} | Potential rank {prospect.PotentialRank}",
                $"Ceiling: {prospect.PotentialSummary}",
                $"Source team: {prospect.SourceTeamName}",
                prospect.ScoutSummary
            ],
            [
                prospect.Summary,
                prospect.SourceStatsSummary
            ]);
    }

    private void EnsureSelectionIsValid(int count)
    {
        if (count <= 0)
        {
            _selectedIndex = 0;
            _pageIndex = 0;
            return;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, count - 1);
        _pageIndex = Math.Clamp(_pageIndex, 0, Math.Max(0, (int)Math.Ceiling(count / (double)GetPageSize()) - 1));
    }

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private Rectangle GetStartDraftButtonBounds() => new(48, _viewport.Y - 58, 120, 34);

    private Rectangle GetNextPickButtonBounds() => new(180, _viewport.Y - 58, 120, 34);

    private Rectangle GetSimRoundButtonBounds() => new(312, _viewport.Y - 58, 120, 34);

    private Rectangle GetSimDraftButtonBounds() => new(444, _viewport.Y - 58, 120, 34);

    private Rectangle GetMakePickButtonBounds() => new(576, _viewport.Y - 58, 120, 34);

    private Rectangle GetSortButtonBounds() => new(708, _viewport.Y - 58, 110, 34);

    private Rectangle GetProspectPanelBounds() => new(48, 168, Math.Max(640, _viewport.X - 420), Math.Max(360, _viewport.Y - 250));

    private Rectangle GetSummaryPanelBounds()
    {
        var prospectBounds = GetProspectPanelBounds();
        return new Rectangle(prospectBounds.Right + 18, prospectBounds.Y, Math.Max(260, _viewport.X - prospectBounds.Right - 66), prospectBounds.Height);
    }

    private int GetPageSize()
    {
        return Math.Max(6, (GetProspectPanelBounds().Height - 92) / 44);
    }

    private Rectangle GetProspectRowBounds(int visibleIndex)
    {
        var panelBounds = GetProspectPanelBounds();
        return new Rectangle(panelBounds.X + 10, panelBounds.Y + 36 + (visibleIndex * 44), panelBounds.Width - 20, 38);
    }

    private Rectangle GetPreviousPageBounds()
    {
        var panelBounds = GetProspectPanelBounds();
        return new Rectangle(panelBounds.X + 10, panelBounds.Bottom - 40, 60, 28);
    }

    private Rectangle GetNextPageBounds()
    {
        var previous = GetPreviousPageBounds();
        return new Rectangle(previous.Right + 126, previous.Y, 60, 28);
    }
}
