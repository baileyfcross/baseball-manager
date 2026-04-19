using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class DraftScreen : GameScreen
{
    private enum PostDraftListMode
    {
        DraftedPlayers,
        FortyManRoster
    }

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
    private readonly ButtonControl _advancePickButton;
    private readonly ButtonControl _listModeButton;
    private readonly ButtonControl _simToNextPickButton;
    private readonly ButtonControl _makePickButton;
    private readonly ButtonControl _sortButton;
    private readonly ButtonControl _assign40ManButton;
    private readonly ButtonControl _assignAffiliateButton;
    private readonly ButtonControl _releasePlayerButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private int _selectedIndex;
    private int _pageIndex;
    private PostDraftListMode _postDraftListMode = PostDraftListMode.DraftedPlayers;
    private DraftSortMode _sortMode = DraftSortMode.Report;
    private string _statusMessage = "Finish the regular season, then start the draft and scout the board before making assignments to the 40-man or affiliate pipeline.";

    public DraftScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl { Label = "Back", OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen)) };
        _startDraftButton = new ButtonControl { Label = "Start Draft", OnClick = StartDraft };
        _advancePickButton = new ButtonControl { Label = "Advance Pick", OnClick = AdvancePick };
        _listModeButton = new ButtonControl { Label = "View: Drafted", OnClick = TogglePostDraftListMode };
        _simToNextPickButton = new ButtonControl { Label = "To My Pick", OnClick = SimToMyPick };
        _makePickButton = new ButtonControl { Label = "Draft Player", OnClick = MakePick };
        _sortButton = new ButtonControl { Label = "Sort: Report", OnClick = CycleSortMode };
        _assign40ManButton = new ButtonControl { Label = "Add To 40-Man", OnClick = AssignTo40Man };
        _assignAffiliateButton = new ButtonControl { Label = "Send To Affiliate", OnClick = AssignToAffiliate };
        _releasePlayerButton = new ButtonControl { Label = "Release Player", OnClick = ReleaseSelectedPlayer };
        _previousPageButton = new ButtonControl { Label = "Prev", OnClick = () => _pageIndex = Math.Max(0, _pageIndex - 1) };
        _nextPageButton = new ButtonControl { Label = "Next", OnClick = () => _pageIndex++ };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _selectedIndex = 0;
        _pageIndex = 0;
        _postDraftListMode = PostDraftListMode.DraftedPlayers;
        _statusMessage = _franchiseSession.HasActiveDraft()
            ? "Review the scout reports, use To My Pick to fast-forward, and draft when your club is on the clock."
            : _franchiseSession.GetDraftOrganizationPlayers().Count > 0
                ? "Place drafted players on the 40-man roster or send them to the affiliate before moving on to next season."
            : _franchiseSession.CanStartDraft()
                ? "The season is complete. Start the draft to build your next prospect class."
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

        if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePosition = currentMouseState.Position;
            if (GetBackButtonBounds().Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetStartDraftButtonBounds().Contains(mousePosition) && !_franchiseSession.HasActiveDraft())
            {
                _startDraftButton.Click();
            }
            else if (GetAdvancePickButtonBounds().Contains(mousePosition) && _franchiseSession.HasActiveDraft())
            {
                _advancePickButton.Click();
            }
            else if (GetStartDraftButtonBounds().Contains(mousePosition) && !_franchiseSession.HasActiveDraft() && hasOrganizationPlayers)
            {
                _listModeButton.Click();
            }
            else if (GetSimToNextPickButtonBounds().Contains(mousePosition) && _franchiseSession.HasActiveDraft())
            {
                _simToNextPickButton.Click();
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
            else if (GetAssign40ManBounds().Contains(mousePosition) && !_franchiseSession.HasActiveDraft())
            {
                _assign40ManButton.Click();
            }
            else if (GetAssignAffiliateBounds().Contains(mousePosition) && !_franchiseSession.HasActiveDraft())
            {
                _assignAffiliateButton.Click();
            }
            else if (GetReleasePlayerBounds().Contains(mousePosition) && !_franchiseSession.HasActiveDraft())
            {
                _releasePlayerButton.Click();
            }
            else if (hasActiveDraft)
            {
                TrySelectProspect(mousePosition);
            }
            else
            {
                TrySelectOrganizationPlayer(mousePosition);
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
        var fortyManPlayers = _franchiseSession.GetDraftFortyManRoster();
        var postDraftCount = _postDraftListMode == PostDraftListMode.FortyManRoster ? fortyManPlayers.Count : organizationPlayers.Count;
        EnsureSelectionIsValid(board.HasActiveDraft ? sortedProspects.Count : postDraftCount);

        uiRenderer.DrawText("Amateur Draft", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, 360, 22), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(48, 112, Math.Max(640, _viewport.X - 96), 42), Color.White, uiRenderer.UiSmallFont, 2);

        var boardBounds = GetProspectPanelBounds();
        var summaryBounds = GetSummaryPanelBounds();
        uiRenderer.DrawButton(string.Empty, boardBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, summaryBounds, new Color(38, 48, 56), Color.White);

        uiRenderer.DrawTextInBounds(board.HasActiveDraft ? "Available Prospects" : (_postDraftListMode == PostDraftListMode.FortyManRoster ? "40-Man Roster" : "Drafted Players"), new Rectangle(boardBounds.X + 12, boardBounds.Y + 8, boardBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(board.HasActiveDraft ? "Draft Summary" : (_postDraftListMode == PostDraftListMode.FortyManRoster ? "Roster Space" : "Roster Decisions"), new Rectangle(summaryBounds.X + 12, summaryBounds.Y + 8, summaryBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);

        if (!board.HasActiveDraft)
        {
            if (_postDraftListMode == PostDraftListMode.FortyManRoster && fortyManPlayers.Count > 0)
            {
                DrawFortyManList(uiRenderer, fortyManPlayers, mousePosition);
                DrawFortyManSummaryPanel(uiRenderer, fortyManPlayers);
            }
            else if (organizationPlayers.Count > 0)
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
            uiRenderer.DrawTextInBounds($"{row.PlayerName} | {row.PrimaryPosition}/{row.SecondaryPosition}".TrimEnd('/'), new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 210, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(row.AssignmentLabel, new Rectangle(bounds.Right - 202, bounds.Y + 4, 194, 16), row.RequiresRosterDecision ? Color.Gold : Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds($"{row.PotentialSummary} | Options {row.MinorLeagueOptionsRemaining}", new Rectangle(bounds.X + 8, bounds.Y + 22, bounds.Width - 16, 14), Color.White, uiRenderer.UiSmallFont);
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
            uiRenderer.DrawWrappedTextInBounds("No drafted players need review right now.", contentBounds, Color.White, uiRenderer.UiSmallFont, 3);
            return;
        }

        uiRenderer.DrawTextInBounds(selectedPlayer.PlayerName, new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"{selectedPlayer.PrimaryPosition}/{selectedPlayer.SecondaryPosition} | {selectedPlayer.AssignmentLabel}", new Rectangle(contentBounds.X, contentBounds.Y + 24, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(selectedPlayer.ScoutSummary, new Rectangle(contentBounds.X, contentBounds.Y + 52, contentBounds.Width, 44), Color.White, uiRenderer.UiSmallFont, 2);
        uiRenderer.DrawTextInBounds($"Ceiling: {selectedPlayer.PotentialSummary}", new Rectangle(contentBounds.X, contentBounds.Y + 102, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Source: {selectedPlayer.SourceTeamName} ({selectedPlayer.Source})", new Rectangle(contentBounds.X, contentBounds.Y + 126, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(selectedPlayer.SourceStatsSummary, new Rectangle(contentBounds.X, contentBounds.Y + 150, contentBounds.Width, 42), Color.Gold, uiRenderer.UiSmallFont, 2);
        uiRenderer.DrawTextInBounds($"Options Remaining: {selectedPlayer.MinorLeagueOptionsRemaining}", new Rectangle(contentBounds.X, contentBounds.Y + 200, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        if (selectedPlayer.RequiresRosterDecision)
        {
            uiRenderer.DrawWrappedTextInBounds("This player must be placed on the 40-man or sent to the affiliate before you can start next season.", new Rectangle(contentBounds.X, contentBounds.Y + 228, contentBounds.Width, 60), Color.Gold, uiRenderer.UiSmallFont, 3);
        }

        if (_franchiseSession.IsSelectedTeam40ManFull())
        {
            uiRenderer.DrawWrappedTextInBounds("Tip: your 40-man is full. Switch to the 40-man list and remove a player to make space.", new Rectangle(contentBounds.X, contentBounds.Bottom - 72, contentBounds.Width, 56), Color.Gold, uiRenderer.UiSmallFont, 3);
        }
    }

    private void DrawFortyManList(UiRenderer uiRenderer, IReadOnlyList<DraftFortyManPlayerView> players, Point mousePosition)
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
            var background = isSelected ? Color.IndianRed : (bounds.Contains(mousePosition) ? Color.DimGray : new Color(54, 62, 70));
            uiRenderer.DrawButton(string.Empty, bounds, background, Color.White);
            uiRenderer.DrawTextInBounds($"{row.PlayerName} | {row.PrimaryPosition}/{row.SecondaryPosition}".TrimEnd('/'), new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 210, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(row.StatusLabel, new Rectangle(bounds.Right - 202, bounds.Y + 4, 194, 16), row.IsDraftedPlayer ? Color.Gold : Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            var optionLabel = row.IsDraftedPlayer ? $"Options {row.MinorLeagueOptionsRemaining}" : "Veteran roster spot";
            uiRenderer.DrawTextInBounds($"Age {row.Age} | {optionLabel}", new Rectangle(bounds.X + 8, bounds.Y + 22, bounds.Width - 16, 14), Color.White, uiRenderer.UiSmallFont);
        }

        uiRenderer.DrawButton(_previousPageButton.Label, GetPreviousPageBounds(), _pageIndex > 0 && GetPreviousPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, GetNextPageBounds(), _pageIndex < maxPage && GetNextPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawTextInBounds($"Page {_pageIndex + 1}/{maxPage + 1}", new Rectangle(GetPreviousPageBounds().Right + 8, GetPreviousPageBounds().Y + 4, 112, 18), Color.White, uiRenderer.UiSmallFont);
    }

    private void DrawFortyManSummaryPanel(UiRenderer uiRenderer, IReadOnlyList<DraftFortyManPlayerView> players)
    {
        var summaryBounds = GetSummaryPanelBounds();
        var contentBounds = new Rectangle(summaryBounds.X + 12, summaryBounds.Y + 36, summaryBounds.Width - 24, summaryBounds.Height - 48);
        var selectedPlayer = players.Count == 0 || _selectedIndex < 0 || _selectedIndex >= players.Count ? null : players[_selectedIndex];

        uiRenderer.DrawTextInBounds($"40-Man Spots Used: {_franchiseSession.GetSelectedTeam40ManCount()}/40", new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds("Tip: if the roster is full, remove someone here to free a 40-man spot for a drafted player.", new Rectangle(contentBounds.X, contentBounds.Y + 28, contentBounds.Width, 46), Color.Gold, uiRenderer.UiSmallFont, 2);

        if (selectedPlayer == null)
        {
            uiRenderer.DrawWrappedTextInBounds("No active 40-man players found for this team.", new Rectangle(contentBounds.X, contentBounds.Y + 88, contentBounds.Width, 42), Color.White, uiRenderer.UiSmallFont, 2);
            return;
        }

        uiRenderer.DrawTextInBounds(selectedPlayer.PlayerName, new Rectangle(contentBounds.X, contentBounds.Y + 96, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"{selectedPlayer.PrimaryPosition}/{selectedPlayer.SecondaryPosition} | Age {selectedPlayer.Age}", new Rectangle(contentBounds.X, contentBounds.Y + 120, contentBounds.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(selectedPlayer.StatusLabel, new Rectangle(contentBounds.X, contentBounds.Y + 144, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        if (selectedPlayer.IsDraftedPlayer)
        {
            uiRenderer.DrawTextInBounds($"Options Remaining: {selectedPlayer.MinorLeagueOptionsRemaining}", new Rectangle(contentBounds.X, contentBounds.Y + 168, contentBounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
        }

        uiRenderer.DrawWrappedTextInBounds("Releasing a player immediately opens a 40-man spot. Use this when you need room for a new draft pick.", new Rectangle(contentBounds.X, contentBounds.Bottom - 76, contentBounds.Width, 60), Color.White, uiRenderer.UiSmallFont, 3);
    }

    private void DrawButtons(UiRenderer uiRenderer, Point mousePosition, DraftBoardView board)
    {
        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);

        var canStartDraft = !board.HasActiveDraft && _franchiseSession.CanStartDraft();
        var organizationPlayers = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
        var fortyManPlayers = _franchiseSession.GetDraftFortyManRoster();
        var isPostDraftMode = !board.HasActiveDraft && organizationPlayers.Count > 0;
        _listModeButton.Label = _postDraftListMode == PostDraftListMode.FortyManRoster ? "View: 40-Man" : "View: Drafted";
        uiRenderer.DrawButton(
            isPostDraftMode ? _listModeButton.Label : _startDraftButton.Label,
            GetStartDraftButtonBounds(),
            (canStartDraft || isPostDraftMode) && GetStartDraftButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : ((canStartDraft || isPostDraftMode) ? Color.SlateBlue : new Color(76, 76, 76)),
            (canStartDraft || isPostDraftMode) ? Color.White : new Color(188, 188, 188));

        var userOnClock = board.HasActiveDraft && string.Equals(board.CurrentTeamName, _franchiseSession.SelectedTeamName, StringComparison.OrdinalIgnoreCase) && !board.IsComplete;
        var canAdvance = board.HasActiveDraft && !board.IsComplete && !userOnClock;
        var canDraft = board.HasActiveDraft && userOnClock && board.AvailableProspects.Count > 0;
        var hasSelectedOrganizationPlayer = !board.HasActiveDraft && _postDraftListMode == PostDraftListMode.DraftedPlayers && _selectedIndex >= 0 && _selectedIndex < organizationPlayers.Count;
        var hasSelectedFortyManPlayer = !board.HasActiveDraft && _postDraftListMode == PostDraftListMode.FortyManRoster && _selectedIndex >= 0 && _selectedIndex < fortyManPlayers.Count;

        uiRenderer.DrawButton(
            isPostDraftMode ? _releasePlayerButton.Label : _advancePickButton.Label,
            GetAdvancePickButtonBounds(),
            ((canAdvance) || hasSelectedFortyManPlayer) && GetAdvancePickButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (((canAdvance) || hasSelectedFortyManPlayer) ? Color.SlateBlue : new Color(76, 76, 76)),
            ((canAdvance) || hasSelectedFortyManPlayer) ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_simToNextPickButton.Label, GetSimToNextPickButtonBounds(), canAdvance && GetSimToNextPickButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canAdvance ? Color.SlateBlue : new Color(76, 76, 76)), canAdvance ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_makePickButton.Label, GetMakePickButtonBounds(), canDraft && GetMakePickButtonBounds().Contains(mousePosition) ? Color.DarkOliveGreen : (canDraft ? Color.OliveDrab : new Color(76, 76, 76)), canDraft ? Color.White : new Color(188, 188, 188));
        var canSort = board.HasActiveDraft || organizationPlayers.Count > 0 || fortyManPlayers.Count > 0;
        uiRenderer.DrawButton(GetSortLabel(), GetSortButtonBounds(), canSort && GetSortButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_assign40ManButton.Label, GetAssign40ManBounds(), hasSelectedOrganizationPlayer && GetAssign40ManBounds().Contains(mousePosition) ? Color.DarkOliveGreen : (hasSelectedOrganizationPlayer ? Color.OliveDrab : new Color(76, 76, 76)), hasSelectedOrganizationPlayer ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_assignAffiliateButton.Label, GetAssignAffiliateBounds(), hasSelectedOrganizationPlayer && GetAssignAffiliateBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (hasSelectedOrganizationPlayer ? Color.SlateBlue : new Color(76, 76, 76)), hasSelectedOrganizationPlayer ? Color.White : new Color(188, 188, 188));
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

    private void TogglePostDraftListMode()
    {
        _postDraftListMode = _postDraftListMode == PostDraftListMode.DraftedPlayers
            ? PostDraftListMode.FortyManRoster
            : PostDraftListMode.DraftedPlayers;
        _selectedIndex = 0;
        _pageIndex = 0;
    }

    private void AdvancePick()
    {
        _franchiseSession.AdvanceDraft(out var message);
        _statusMessage = message;
    }

    private void SimToMyPick()
    {
        _franchiseSession.AdvanceDraftToNextUserPick(out var message);
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

    private void AssignTo40Man()
    {
        var players = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
        if (_selectedIndex < 0 || _selectedIndex >= players.Count)
        {
            _statusMessage = "Select a drafted player before making a roster assignment.";
            return;
        }

        _franchiseSession.AssignDraftPlayerTo40Man(players[_selectedIndex].PlayerId, out var message);
        _statusMessage = message;
    }

    private void AssignToAffiliate()
    {
        var players = GetSortedOrganizationPlayers(_franchiseSession.GetDraftOrganizationPlayers());
        if (_selectedIndex < 0 || _selectedIndex >= players.Count)
        {
            _statusMessage = "Select a drafted player before making a roster assignment.";
            return;
        }

        _franchiseSession.AssignDraftPlayerToAffiliate(players[_selectedIndex].PlayerId, out var message);
        _statusMessage = message;
    }

    private void ReleaseSelectedPlayer()
    {
        var players = _franchiseSession.GetDraftFortyManRoster();
        if (_selectedIndex < 0 || _selectedIndex >= players.Count)
        {
            _statusMessage = "Select a 40-man player before removing someone to make space.";
            return;
        }

        _franchiseSession.ReleaseSelectedTeam40ManPlayer(players[_selectedIndex].PlayerId, out var message);
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

    private bool TrySelectOrganizationPlayer(Point mousePosition)
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

    private Rectangle GetStartDraftButtonBounds() => new(48, _viewport.Y - 58, 160, 34);

    private Rectangle GetAdvancePickButtonBounds() => new(220, _viewport.Y - 58, 170, 34);

    private Rectangle GetSimToNextPickButtonBounds() => new(402, _viewport.Y - 58, 150, 34);

    private Rectangle GetMakePickButtonBounds() => new(564, _viewport.Y - 58, 170, 34);

    private Rectangle GetSortButtonBounds() => new(746, _viewport.Y - 58, 140, 34);

    private Rectangle GetAssign40ManBounds() => new(898, _viewport.Y - 58, 160, 34);

    private Rectangle GetAssignAffiliateBounds() => new(1070, _viewport.Y - 58, 170, 34);

    private Rectangle GetReleasePlayerBounds() => GetAdvancePickButtonBounds();

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
