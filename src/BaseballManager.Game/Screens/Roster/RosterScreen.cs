using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
using BaseballManager.Game.UI.Layout;
using BaseballManager.Game.UI.Widgets;
using BaseballManager.Core.Economy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.Roster;

public sealed class RosterScreen : GameScreen
{
    private enum DetailPanelMode
    {
        Player,
        Counts
    }

    private enum RosterTeamFilterMode
    {
        All,
        FirstTeam,
        FortyMan,
        Affiliate,
        TripleA,
        DoubleA,
        SingleA
    }

    private enum RosterPositionFilterMode
    {
        All,
        Hitters,
        Pitchers,
        Catcher,
        FirstBase,
        SecondBase,
        ThirdBase,
        Shortstop,
        LeftField,
        CenterField,
        RightField,
        DesignatedHitter,
        StartingPitcher,
        ReliefPitcher
    }

    private enum RosterSortMode
    {
        Status,
        Name,
        Position
    }

    private readonly record struct DetailActionLayout(Rectangle DetailModeBounds, Rectangle CompositionModeBounds, Rectangle AutomationModeBounds);

    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _teamFilterButton;
    private readonly ButtonControl _positionFilterButton;
    private readonly ButtonControl _sortButton;
    private readonly ButtonControl _clearSelectionButton;
    private readonly ButtonControl _detailModeButton;
    private readonly ButtonControl _compositionModeButton;
    private readonly ButtonControl _minorLeagueAutomationButton;
    private readonly ButtonControl _returnToAutoButton;
    private readonly ButtonControl _assign40ManButton;
    private readonly ButtonControl _affiliateTargetButton;
    private readonly ButtonControl _assignAffiliateButton;
    private readonly ButtonControl _removeFromFortyManButton;
    private readonly ButtonControl _releaseButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private bool _showTeamFilterDropdown;
    private bool _showPositionFilterDropdown;
    private Point _viewport = new(1280, 720);
    private Guid? _selectedPlayerId;
    private readonly HashSet<Guid> _selectedPlayerIds = [];
    private readonly PlayerContextOverlay _playerContextOverlay = new();
    private int _pageIndex;
    private DetailPanelMode _detailPanelMode = DetailPanelMode.Player;
    private OrganizationRosterCompositionMode _compositionMode = OrganizationRosterCompositionMode.FirstTeam;
    private MinorLeagueAffiliateLevel _affiliateAssignmentTarget = MinorLeagueAffiliateLevel.TripleA;
    private RosterTeamFilterMode _teamFilterMode = RosterTeamFilterMode.All;
    private RosterPositionFilterMode _positionFilterMode = RosterPositionFilterMode.All;
    private RosterSortMode _sortMode = RosterSortMode.Status;
    private string _statusMessage = "Review player stats, add or remove players from the 40-man roster, and use AAA, AA, or A assignments to lock specific minor leaguers while AI manages everyone else.";
    private IReadOnlyList<OrganizationRosterPlayerView> _cachedOrganizationPlayers = [];
    private IReadOnlyList<OrganizationRosterPlayerView> _cachedVisiblePlayers = [];
    private IReadOnlyDictionary<Guid, Contract> _cachedContractsByPlayerId = new Dictionary<Guid, Contract>();
    private bool _organizationRosterDirty = true;
    private bool _visibleRosterDirty = true;
    private bool _contractsDirty = true;

    public RosterScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl { Label = "Back", OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen)) };
        _teamFilterButton = new ButtonControl { Label = "Team: All", OnClick = ToggleTeamFilterDropdown };
        _positionFilterButton = new ButtonControl { Label = "Pos: All", OnClick = TogglePositionFilterDropdown };
        _sortButton = new ButtonControl { Label = "Sort: Status", OnClick = CycleSort };
        _clearSelectionButton = new ButtonControl { Label = "Clear Select", OnClick = ClearSelection };
        _detailModeButton = new ButtonControl { Label = "View: Player", OnClick = ToggleDetailPanelMode };
        _compositionModeButton = new ButtonControl { Label = "Scope: First Team", OnClick = CycleCompositionMode };
        _minorLeagueAutomationButton = new ButtonControl { Label = "Minor AI: On", OnClick = ToggleMinorLeagueAutomation };
        _returnToAutoButton = new ButtonControl { Label = "Return To AI", OnClick = ReturnSelectedPlayersToAuto };
        _assign40ManButton = new ButtonControl { Label = "Add To 40-Man", OnClick = AssignToFortyMan };
        _affiliateTargetButton = new ButtonControl { Label = "Target: AAA", OnClick = CycleAffiliateAssignmentTarget };
        _assignAffiliateButton = new ButtonControl { Label = "Send To Affiliate", OnClick = AssignToAffiliate };
        _removeFromFortyManButton = new ButtonControl { Label = "Remove From 40-Man", OnClick = RemoveFromFortyMan };
        _releaseButton = new ButtonControl { Label = "Release", OnClick = ReleasePlayer };
        _previousPageButton = new ButtonControl { Label = "Prev", OnClick = () => _pageIndex = Math.Max(0, _pageIndex - 1) };
        _nextPageButton = new ButtonControl { Label = "Next", OnClick = () => _pageIndex++ };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _pageIndex = 0;
        _detailPanelMode = DetailPanelMode.Player;
        _compositionMode = OrganizationRosterCompositionMode.FirstTeam;
        _affiliateAssignmentTarget = MinorLeagueAffiliateLevel.TripleA;
        _teamFilterMode = RosterTeamFilterMode.All;
        _positionFilterMode = RosterPositionFilterMode.All;
        _sortMode = RosterSortMode.Status;
        _showTeamFilterDropdown = false;
        _showPositionFilterDropdown = false;
        _selectedPlayerId = null;
        _selectedPlayerIds.Clear();
        _playerContextOverlay.Close();
        _statusMessage = "Review player stats, add or remove players from the 40-man roster, and use AAA, AA, or A assignments to lock specific minor leaguers while AI manages everyone else.";
        InvalidateRosterCaches();
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;
        var players = GetVisiblePlayers();
        EnsureSelectionIsValid(players);

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
            else if (GetTeamFilterButtonBounds().Contains(mousePosition))
            {
                _teamFilterButton.Click();
            }
            else if (GetPositionFilterButtonBounds().Contains(mousePosition))
            {
                _positionFilterButton.Click();
            }
            else if (_showTeamFilterDropdown)
            {
                TrySelectTeamFilterOption(mousePosition);
            }
            else if (_showPositionFilterDropdown)
            {
                TrySelectPositionFilterOption(mousePosition);
            }
            else if (GetSortButtonBounds().Contains(mousePosition))
            {
                _sortButton.Click();
            }
            else if (GetClearSelectionBounds().Contains(mousePosition))
            {
                _clearSelectionButton.Click();
            }
            else if (GetDetailModeBounds().Contains(mousePosition))
            {
                _detailModeButton.Click();
            }
            else if (GetCompositionModeBounds().Contains(mousePosition))
            {
                _compositionModeButton.Click();
            }
            else if (GetMinorLeagueAutomationBounds().Contains(mousePosition))
            {
                _minorLeagueAutomationButton.Click();
            }
            else if (GetAffiliateTargetBounds().Contains(mousePosition))
            {
                _affiliateTargetButton.Click();
            }
            else if (GetReturnToAutoBounds().Contains(mousePosition))
            {
                _returnToAutoButton.Click();
            }
            else if (GetAssign40ManBounds().Contains(mousePosition))
            {
                _assign40ManButton.Click();
            }
            else if (GetAssignAffiliateBounds().Contains(mousePosition))
            {
                _assignAffiliateButton.Click();
            }
            else if (GetRemoveFromFortyManBounds().Contains(mousePosition))
            {
                _removeFromFortyManButton.Click();
            }
            else if (GetReleaseButtonBounds().Contains(mousePosition))
            {
                _releaseButton.Click();
            }
            else if (GetPreviousPageBounds().Contains(mousePosition))
            {
                _previousPageButton.Click();
            }
            else if (GetNextPageBounds().Contains(mousePosition))
            {
                var maxPage = Math.Max(0, (int)Math.Ceiling(players.Count / (double)GetPageSize()) - 1);
                if (_pageIndex < maxPage)
                {
                    _nextPageButton.Click();
                }
            }
            else
            {
                TrySelectPlayer(mousePosition, players);
            }
        }

        if (isRightPress)
        {
            var mousePosition = currentMouseState.Position;
            if (!TryOpenPlayerContextMenu(mousePosition, players))
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
        var players = GetVisiblePlayers();
        EnsureSelectionIsValid(players);
        var selectedPlayer = GetSelectedPlayer(players);
        var contractsByPlayerId = GetContractsByPlayerId();

        _teamFilterButton.Label = _showTeamFilterDropdown
            ? $"Team: {GetTeamFilterLabel(_teamFilterMode)} ^"
            : $"Team: {GetTeamFilterLabel(_teamFilterMode)} v";
        _positionFilterButton.Label = _showPositionFilterDropdown
            ? $"Pos: {GetPositionFilterLabel(_positionFilterMode)} ^"
            : $"Pos: {GetPositionFilterLabel(_positionFilterMode)} v";
        _sortButton.Label = $"Sort: {GetSortLabel(_sortMode)}";
        _detailModeButton.Label = _detailPanelMode == DetailPanelMode.Player ? "View: Player" : "View: Counts";
        _compositionModeButton.Label = $"Scope: {GetCompositionModeLabel(_compositionMode)}";
        _minorLeagueAutomationButton.Label = _franchiseSession.IsSelectedTeamMinorLeagueAutomationEnabled() ? "Minor AI: On" : "Minor AI: Off";
        var compactActionLabels = _viewport.X < 1380;
        _returnToAutoButton.Label = compactActionLabels ? "Return AI" : "Return To AI";
        _assign40ManButton.Label = compactActionLabels ? "Add 40-Man" : "Add To 40-Man";
        _affiliateTargetButton.Label = compactActionLabels
            ? $"Target {GetAffiliateTargetLabel(_affiliateAssignmentTarget)}"
            : $"Target: {GetAffiliateTargetLabel(_affiliateAssignmentTarget)}";
        _assignAffiliateButton.Label = compactActionLabels
            ? $"To {GetAffiliateTargetLabel(_affiliateAssignmentTarget)}"
            : $"Send To {GetAffiliateTargetLabel(_affiliateAssignmentTarget)}";
        _removeFromFortyManButton.Label = compactActionLabels ? "Remove 40-Man" : "Remove From 40-Man";

        uiRenderer.DrawText("Roster", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, 360, 22), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(48, 112, Math.Max(640, _viewport.X - 96), 42), Color.White, uiRenderer.UiSmallFont, 2);

        var listBounds = GetListPanelBounds();
        var detailBounds = GetDetailPanelBounds();
        uiRenderer.DrawButton(string.Empty, listBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, detailBounds, new Color(38, 48, 56), Color.White);
        var selectedPlayers = GetSelectedPlayersForActions();
        var rosterViewLabel = GetRosterViewLabel();
        var selectedLabel = selectedPlayers.Count > 1 ? $"{rosterViewLabel} ({selectedPlayers.Count} selected)" : rosterViewLabel;
        uiRenderer.DrawTextInBounds(selectedLabel, new Rectangle(listBounds.X + 12, listBounds.Y + 8, listBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(_detailPanelMode == DetailPanelMode.Player ? "Player Details" : "Roster Counts", new Rectangle(detailBounds.X + 12, detailBounds.Y + 8, detailBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        DrawDetailPanelButtons(uiRenderer, mousePosition);

        if (players.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds(GetEmptyRosterMessage(), new Rectangle(listBounds.X + 12, listBounds.Y + 40, listBounds.Width - 24, 60), Color.White, uiRenderer.UiSmallFont, 3);
            if (_detailPanelMode == DetailPanelMode.Counts)
            {
                DrawRosterComposition(uiRenderer, GetCurrentRosterComposition());
            }
            else
            {
                uiRenderer.DrawWrappedTextInBounds("Change the filter or return after adding players to the organization.", new Rectangle(detailBounds.X + 12, detailBounds.Y + 40, detailBounds.Width - 24, 60), Color.White, uiRenderer.UiSmallFont, 3);
            }
        }
        else
        {
            DrawPlayerList(uiRenderer, players, mousePosition);
            if (_detailPanelMode == DetailPanelMode.Counts)
            {
                DrawRosterComposition(uiRenderer, GetCurrentRosterComposition());
            }
            else if (selectedPlayers.Count > 1)
            {
                DrawSelectionSummary(uiRenderer, selectedPlayers);
            }
            else if (selectedPlayer != null)
            {
                DrawPlayerDetail(uiRenderer, selectedPlayer, contractsByPlayerId);
            }
        }

        DrawButtons(uiRenderer, mousePosition, selectedPlayer);

        if (_showTeamFilterDropdown)
        {
            DrawTeamFilterDropdown(uiRenderer);
        }

        if (_showPositionFilterDropdown)
        {
            DrawPositionFilterDropdown(uiRenderer);
        }

        _playerContextOverlay.Draw(uiRenderer, mousePosition, _viewport);
    }

    private void DrawPlayerList(UiRenderer uiRenderer, IReadOnlyList<OrganizationRosterPlayerView> players, Point mousePosition)
    {
        var suppressHover = IsOverlayCapturingMouse();
        var pageSize = GetPageSize();
        var maxPage = Math.Max(0, (int)Math.Ceiling(players.Count / (double)pageSize) - 1);
        _pageIndex = Math.Clamp(_pageIndex, 0, maxPage);
        var startIndex = _pageIndex * pageSize;
        var visiblePlayers = players.Skip(startIndex).Take(pageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetPlayerRowBounds(i);
            var isFocused = player.PlayerId == _selectedPlayerId;
            var isSelected = _selectedPlayerIds.Contains(player.PlayerId);
            var background = isFocused ? Color.DarkOliveGreen : (isSelected ? new Color(72, 94, 66) : (!suppressHover && bounds.Contains(mousePosition) ? Color.DimGray : new Color(54, 62, 70)));
            uiRenderer.DrawButton(string.Empty, bounds, background, Color.White);
            var checkboxBounds = GetPlayerCheckboxBounds(i);
            uiRenderer.DrawButton(string.Empty, checkboxBounds, isSelected ? Color.DarkOliveGreen : new Color(24, 32, 38), Color.White);
            if (isSelected)
            {
                uiRenderer.DrawTextInBounds("X", checkboxBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            }

            uiRenderer.DrawTextInBounds($"{player.PlayerName} | {player.PrimaryPosition}/{player.SecondaryPosition}".TrimEnd('/'), new Rectangle(bounds.X + 34, bounds.Y + 4, bounds.Width - 242, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(player.AssignmentLabel, new Rectangle(bounds.Right - 208, bounds.Y + 4, 200, 16), player.IsOnFortyMan ? Color.Gold : Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds(GetPlayerListSummary(player), new Rectangle(bounds.X + 8, bounds.Y + 22, bounds.Width - 16, 14), Color.White, uiRenderer.UiSmallFont);
        }

        uiRenderer.DrawButton(_previousPageButton.Label, GetPreviousPageBounds(), _pageIndex > 0 && !suppressHover && GetPreviousPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, GetNextPageBounds(), _pageIndex < maxPage && !suppressHover && GetNextPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawTextInBounds($"Page {_pageIndex + 1}/{maxPage + 1}", new Rectangle(GetPreviousPageBounds().Right + 8, GetPreviousPageBounds().Y + 4, 120, 18), Color.White, uiRenderer.UiSmallFont);
    }

    private void DrawPlayerDetail(UiRenderer uiRenderer, OrganizationRosterPlayerView player, IReadOnlyDictionary<Guid, BaseballManager.Core.Economy.Contract> contractsByPlayerId)
    {
        var currentStats = _franchiseSession.GetPlayerSeasonStats(player.PlayerId);
        var lastSeasonStats = _franchiseSession.GetLastSeasonStats(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var recentStats = _franchiseSession.GetRecentPlayerStats(player.PlayerId);
        var scoutNote = _franchiseSession.GetQuickScoutNote(player.PlayerId);
        var medicalStatus = _franchiseSession.GetPlayerMedicalStatus(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var medicalReport = _franchiseSession.GetPlayerMedicalReport(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        contractsByPlayerId.TryGetValue(player.PlayerId, out var contract);

        var content = GetDetailBodyBounds();
        var cursorY = content.Y;

        bool TryDrawLine(string text, Color color)
        {
            var bounds = new Rectangle(content.X, cursorY, content.Width, 18);
            if (!FitsWithin(content, bounds))
            {
                return false;
            }

            uiRenderer.DrawTextInBounds(text, bounds, color, uiRenderer.UiSmallFont);
            cursorY += 24;
            return true;
        }

        bool TryDrawWrapped(string text, int maxLines)
        {
            var height = maxLines <= 2 ? 36 : 52;
            var bounds = new Rectangle(content.X, cursorY, content.Width, height);
            if (!FitsWithin(content, bounds))
            {
                return false;
            }

            uiRenderer.DrawWrappedTextInBounds(text, bounds, Color.White, uiRenderer.UiSmallFont, maxLines);
            cursorY += height + 8;
            return true;
        }

        bool TryDrawSection(string title, string body, int maxLines)
        {
            if (!TryDrawLine(title, Color.Gold))
            {
                return false;
            }

            return TryDrawWrapped(body, maxLines);
        }

        if (!TryDrawLine(player.PlayerName, Color.White))
        {
            return;
        }

        if (!TryDrawLine($"{player.PrimaryPosition}/{player.SecondaryPosition} | Age {player.Age} | {player.AssignmentLabel}", Color.Gold))
        {
            return;
        }

        if (!TryDrawLine($"40-Man Spots Used: {_franchiseSession.GetSelectedTeam40ManCount()}/40", Color.White))
        {
            return;
        }

        var slotLabel = player.RotationSlot.HasValue
            ? $"Rotation Slot: {player.RotationSlot.Value}"
            : player.LineupSlot.HasValue
                ? $"Lineup Slot: {player.LineupSlot.Value}"
                : "Not in current lineup or rotation";
        if (!TryDrawLine(slotLabel, Color.White))
        {
            return;
        }

        if (!TryDrawSection("Current Season", FormatSeasonLine(player.PrimaryPosition, currentStats), 2))
        {
            return;
        }

        if (!TryDrawSection("Last Season", FormatSeasonLine(player.PrimaryPosition, lastSeasonStats), 2))
        {
            return;
        }

        if (!TryDrawSection("Recent Form", FormatRecentLine(player.PrimaryPosition, recentStats), 2))
        {
            return;
        }

        if (!TryDrawLine("Contract", Color.Gold))
        {
            return;
        }

        if (!TryDrawLine(contract == null ? "No active contract on file." : $"{FormatSalary(contract.AnnualSalary)} for {contract.YearsRemaining} year(s) remaining.", Color.White))
        {
            return;
        }

        var optionsLabel = player.IsDraftedPlayer
            ? $"Minor-League Options Remaining: {player.MinorLeagueOptionsRemaining}"
            : "Veteran import: affiliate and 40-man moves are enabled here; option years are not tracked yet.";
        if (!TryDrawWrapped(optionsLabel, 2))
        {
            return;
        }

        var automationLabel = player.AffiliateLevel.HasValue
            ? player.IsMinorLeagueAssignmentLocked
                ? "Minor-League AI: User locked to this affiliate tier."
                : "Minor-League AI: Eligible for automatic affiliate promotion or demotion."
            : "Minor-League AI: Active only for players already assigned to AAA, AA, or A.";
        if (!TryDrawWrapped(automationLabel, 2))
        {
            return;
        }

        if (!TryDrawLine($"Medical: {medicalStatus}", Color.Gold))
        {
            return;
        }

        if (!TryDrawWrapped(medicalReport, 3))
        {
            return;
        }

        if (!TryDrawLine("Scout Note", Color.Gold))
        {
            return;
        }

        _ = TryDrawWrapped(scoutNote, 3);
    }

    private void DrawSelectionSummary(UiRenderer uiRenderer, IReadOnlyList<OrganizationRosterPlayerView> selectedPlayers)
    {
        var content = GetDetailBodyBounds();
        var firstTeamCount = selectedPlayers.Count(player => player.IsOnFirstTeam);
        var affiliateCount = selectedPlayers.Count(player => player.AffiliateLevel.HasValue);
        var tripleACount = selectedPlayers.Count(player => player.AffiliateLevel == MinorLeagueAffiliateLevel.TripleA);
        var doubleACount = selectedPlayers.Count(player => player.AffiliateLevel == MinorLeagueAffiliateLevel.DoubleA);
        var singleACount = selectedPlayers.Count(player => player.AffiliateLevel == MinorLeagueAffiliateLevel.SingleA);
        var lockedCount = selectedPlayers.Count(player => player.IsMinorLeagueAssignmentLocked);
        var depthCount = selectedPlayers.Count(player => player.TeamStatusLabel == "Organization Depth");
        var fortyManCount = selectedPlayers.Count(player => player.IsOnFortyMan);

        var cursorY = content.Y;

        bool TryDrawLine(string text, Color color)
        {
            var bounds = new Rectangle(content.X, cursorY, content.Width, 18);
            if (!FitsWithin(content, bounds))
            {
                return false;
            }

            uiRenderer.DrawTextInBounds(text, bounds, color, uiRenderer.UiSmallFont);
            cursorY += 24;
            return true;
        }

        if (!TryDrawLine($"{selectedPlayers.Count} Players Selected", Color.White))
        {
            return;
        }

        if (!TryDrawLine($"First Team {firstTeamCount} | Depth {depthCount} | Affiliate {affiliateCount}", Color.Gold))
        {
            return;
        }

        if (!TryDrawLine($"AAA {tripleACount} | AA {doubleACount} | A {singleACount}", Color.White))
        {
            return;
        }

        if (!TryDrawLine($"Manual Locks {lockedCount} | AI Managed {selectedPlayers.Count - lockedCount}", Color.White))
        {
            return;
        }

        if (!TryDrawLine($"40-Man {fortyManCount} | Non-40-Man {selectedPlayers.Count - fortyManCount}", Color.White))
        {
            return;
        }

        var guidanceBounds = new Rectangle(content.X, cursorY + 2, content.Width, 60);
        if (FitsWithin(content, guidanceBounds))
        {
            uiRenderer.DrawWrappedTextInBounds("Use the roster action buttons to apply moves to the full selection. Manual affiliate assignments lock a player to that tier until you return him to AI control.", guidanceBounds, Color.White, uiRenderer.UiSmallFont, 3);
            cursorY = guidanceBounds.Bottom + 12;
        }

        var listY = cursorY;
        foreach (var player in selectedPlayers.Take(8))
        {
            var nameBounds = new Rectangle(content.X, listY, content.Width, 16);
            var detailBounds = new Rectangle(content.X + 8, listY + 16, content.Width - 8, 16);
            if (!FitsWithin(content, detailBounds))
            {
                break;
            }

            uiRenderer.DrawTextInBounds($"{player.PlayerName} | {player.PrimaryPosition}/{player.SecondaryPosition}".TrimEnd('/'), new Rectangle(content.X, listY, content.Width, 16), player.IsOnFirstTeam ? Color.Gold : Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{player.TeamStatusLabel} | {player.AssignmentLabel}", detailBounds, Color.White, uiRenderer.UiSmallFont);
            listY += 40;
        }
    }

    private void DrawRosterComposition(UiRenderer uiRenderer, OrganizationRosterCompositionView composition)
    {
        var bounds = GetDetailPanelBounds();
        var content = new Rectangle(bounds.X + 12, bounds.Y + 72, bounds.Width - 24, bounds.Height - 84);
        var titleBounds = new Rectangle(content.X, content.Y, content.Width, 18);
        if (!FitsWithin(content, titleBounds))
        {
            return;
        }

        uiRenderer.DrawTextInBounds(composition.Title, titleBounds, Color.White, uiRenderer.UiSmallFont);

        var summaryLineCount = content.Height >= 260 ? 3 : 2;
        var summaryBounds = new Rectangle(content.X, content.Y + 22, content.Width, summaryLineCount == 3 ? 54 : 36);
        if (FitsWithin(content, summaryBounds))
        {
            uiRenderer.DrawWrappedTextInBounds(composition.Summary, summaryBounds, Color.Gold, uiRenderer.UiSmallFont, summaryLineCount);
        }

        var totalLabel = composition.TargetCount.HasValue
            ? $"Total: {composition.TotalCount}/{composition.TargetCount.Value}"
            : $"Total: {composition.TotalCount}";
        var totalBounds = new Rectangle(content.X, content.Y + (summaryLineCount == 3 ? 84 : 66), content.Width, 18);
        if (!FitsWithin(content, totalBounds))
        {
            return;
        }

        uiRenderer.DrawTextInBounds(totalLabel, totalBounds, Color.White, uiRenderer.UiSmallFont);

        var rowsStartY = totalBounds.Bottom + 20;

        for (var i = 0; i < composition.Buckets.Count; i++)
        {
            var bucket = composition.Buckets[i];
            var valueLabel = bucket.TargetCount.HasValue
                ? $"{bucket.Count}/{bucket.TargetCount.Value}"
                : bucket.Count.ToString();
            var rowTop = rowsStartY + GetCompositionRowOffset(composition.Buckets, i);
            var rowBounds = new Rectangle(content.X, rowTop, content.Width, 18);
            if (!FitsWithin(content, rowBounds))
            {
                break;
            }

            uiRenderer.DrawTextInBounds(bucket.Label, new Rectangle(rowBounds.X, rowBounds.Y, rowBounds.Width - 84, rowBounds.Height), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(valueLabel, new Rectangle(rowBounds.Right - 84, rowBounds.Y, 84, rowBounds.Height), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);

            if (bucket.Details == null)
            {
                continue;
            }

            for (var detailIndex = 0; detailIndex < bucket.Details.Count; detailIndex++)
            {
                var detail = bucket.Details[detailIndex];
                var detailBounds = new Rectangle(content.X + 18, rowBounds.Bottom + 4 + (detailIndex * 22), content.Width - 18, 16);
                if (!FitsWithin(content, detailBounds))
                {
                    break;
                }

                uiRenderer.DrawTextInBounds(detail.Label, new Rectangle(detailBounds.X, detailBounds.Y, detailBounds.Width - 84, detailBounds.Height), Color.LightGray, uiRenderer.UiSmallFont);
                uiRenderer.DrawTextInBounds(detail.Count.ToString(), new Rectangle(detailBounds.Right - 84, detailBounds.Y, 84, detailBounds.Height), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            }
        }
    }

    private static bool FitsWithin(Rectangle container, Rectangle candidate)
    {
        return candidate.Y >= container.Y && candidate.Bottom <= container.Bottom;
    }

    private static int GetCompositionRowOffset(IReadOnlyList<OrganizationRosterCompositionBucketView> buckets, int index)
    {
        var offset = 0;
        for (var bucketIndex = 0; bucketIndex < index; bucketIndex++)
        {
            offset += 28;
            offset += (buckets[bucketIndex].Details?.Count ?? 0) * 22;
        }

        return offset;
    }

    private void DrawDetailPanelButtons(UiRenderer uiRenderer, Point mousePosition)
    {
        var suppressHover = IsOverlayCapturingMouse();
        var detailModeBounds = GetDetailModeBounds();
        var compositionModeBounds = GetCompositionModeBounds();
        var minorLeagueAutomationBounds = GetMinorLeagueAutomationBounds();
        uiRenderer.DrawButton(_detailModeButton.Label, detailModeBounds, !suppressHover && detailModeBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_compositionModeButton.Label, compositionModeBounds, !suppressHover && compositionModeBounds.Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
        uiRenderer.DrawButton(_minorLeagueAutomationButton.Label, minorLeagueAutomationBounds, !suppressHover && minorLeagueAutomationBounds.Contains(mousePosition) ? Color.DarkOliveGreen : (_franchiseSession.IsSelectedTeamMinorLeagueAutomationEnabled() ? Color.OliveDrab : new Color(110, 88, 34)), Color.White);
    }

    private void DrawButtons(UiRenderer uiRenderer, Point mousePosition, OrganizationRosterPlayerView? selectedPlayer)
    {
        var suppressHover = _showTeamFilterDropdown || _showPositionFilterDropdown;
        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), !suppressHover && GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_teamFilterButton.Label, GetTeamFilterButtonBounds(), GetTeamFilterButtonBounds().Contains(mousePosition) || _showTeamFilterDropdown ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_positionFilterButton.Label, GetPositionFilterButtonBounds(), GetPositionFilterButtonBounds().Contains(mousePosition) || _showPositionFilterDropdown ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_sortButton.Label, GetSortButtonBounds(), !suppressHover && GetSortButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_clearSelectionButton.Label, GetClearSelectionBounds(), _selectedPlayerIds.Count > 0 && !suppressHover && GetClearSelectionBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (_selectedPlayerIds.Count > 0 ? Color.SlateBlue : new Color(76, 76, 76)), _selectedPlayerIds.Count > 0 ? Color.White : new Color(188, 188, 188));

        var actionTargets = GetSelectedPlayersForActions();
        var canAssignToFortyMan = actionTargets.Any(player => player.CanAssignToFortyMan);
        var canAssignToAffiliate = actionTargets.Any(player => player.AffiliateLevel != _affiliateAssignmentTarget);
        var canReturnToAuto = actionTargets.Any(player => player.CanReturnToAutomaticAffiliate);
        var canRemoveFromFortyMan = actionTargets.Any(player => player.IsOnFortyMan);
        var canRelease = selectedPlayer?.CanRelease == true;
        uiRenderer.DrawButton(_returnToAutoButton.Label, GetReturnToAutoBounds(), canReturnToAuto && !suppressHover && GetReturnToAutoBounds().Contains(mousePosition) ? Color.DarkOliveGreen : (canReturnToAuto ? Color.OliveDrab : new Color(76, 76, 76)), canReturnToAuto ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_assign40ManButton.Label, GetAssign40ManBounds(), canAssignToFortyMan && !suppressHover && GetAssign40ManBounds().Contains(mousePosition) ? Color.DarkOliveGreen : (canAssignToFortyMan ? Color.OliveDrab : new Color(76, 76, 76)), canAssignToFortyMan ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_affiliateTargetButton.Label, GetAffiliateTargetBounds(), !suppressHover && GetAffiliateTargetBounds().Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
        uiRenderer.DrawButton(_assignAffiliateButton.Label, GetAssignAffiliateBounds(), canAssignToAffiliate && !suppressHover && GetAssignAffiliateBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canAssignToAffiliate ? Color.SlateBlue : new Color(76, 76, 76)), canAssignToAffiliate ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_removeFromFortyManButton.Label, GetRemoveFromFortyManBounds(), canRemoveFromFortyMan && !suppressHover && GetRemoveFromFortyManBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canRemoveFromFortyMan ? Color.SlateBlue : new Color(76, 76, 76)), canRemoveFromFortyMan ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_releaseButton.Label, GetReleaseButtonBounds(), canRelease && !suppressHover && GetReleaseButtonBounds().Contains(mousePosition) ? Color.DarkRed : (canRelease ? Color.Firebrick : new Color(76, 76, 76)), canRelease ? Color.White : new Color(188, 188, 188));
    }

    private void DrawTeamFilterDropdown(UiRenderer uiRenderer)
    {
        var options = GetTeamFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var bounds = GetTeamFilterOptionBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = option == _teamFilterMode;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton(GetTeamFilterLabel(option), bounds, color, Color.White);
        }
    }

    private void DrawPositionFilterDropdown(UiRenderer uiRenderer)
    {
        var options = GetPositionFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var bounds = GetPositionFilterOptionBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = option == _positionFilterMode;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton(GetPositionFilterLabel(option), bounds, color, Color.White);
        }
    }

    private IReadOnlyList<OrganizationRosterPlayerView> GetVisiblePlayers()
    {
        if (!_visibleRosterDirty)
        {
            return _cachedVisiblePlayers;
        }

        var players = GetOrganizationPlayers();
        IEnumerable<OrganizationRosterPlayerView> filtered = _teamFilterMode switch
        {
            RosterTeamFilterMode.FirstTeam => players.Where(player => player.IsOnFirstTeam),
            RosterTeamFilterMode.FortyMan => players.Where(player => player.IsOnFortyMan),
            RosterTeamFilterMode.Affiliate => players.Where(player => player.AffiliateLevel.HasValue),
            RosterTeamFilterMode.TripleA => players.Where(player => player.AffiliateLevel == MinorLeagueAffiliateLevel.TripleA),
            RosterTeamFilterMode.DoubleA => players.Where(player => player.AffiliateLevel == MinorLeagueAffiliateLevel.DoubleA),
            RosterTeamFilterMode.SingleA => players.Where(player => player.AffiliateLevel == MinorLeagueAffiliateLevel.SingleA),
            _ => players
        };

        filtered = _positionFilterMode switch
        {
            RosterPositionFilterMode.Hitters => filtered.Where(player => !IsPitcher(player.PrimaryPosition)),
            RosterPositionFilterMode.Pitchers => filtered.Where(player => IsPitcher(player.PrimaryPosition)),
            RosterPositionFilterMode.Catcher => filtered.Where(player => MatchesRosterPositionFilter(player, "C")),
            RosterPositionFilterMode.FirstBase => filtered.Where(player => MatchesRosterPositionFilter(player, "1B")),
            RosterPositionFilterMode.SecondBase => filtered.Where(player => MatchesRosterPositionFilter(player, "2B")),
            RosterPositionFilterMode.ThirdBase => filtered.Where(player => MatchesRosterPositionFilter(player, "3B")),
            RosterPositionFilterMode.Shortstop => filtered.Where(player => MatchesRosterPositionFilter(player, "SS")),
            RosterPositionFilterMode.LeftField => filtered.Where(player => MatchesRosterPositionFilter(player, "LF")),
            RosterPositionFilterMode.CenterField => filtered.Where(player => MatchesRosterPositionFilter(player, "CF")),
            RosterPositionFilterMode.RightField => filtered.Where(player => MatchesRosterPositionFilter(player, "RF")),
            RosterPositionFilterMode.DesignatedHitter => filtered.Where(player => MatchesRosterPositionFilter(player, "DH")),
            RosterPositionFilterMode.StartingPitcher => filtered.Where(player => MatchesRosterPositionFilter(player, "SP")),
            RosterPositionFilterMode.ReliefPitcher => filtered.Where(player => MatchesRosterPositionFilter(player, "RP")),
            _ => filtered
        };

        _cachedVisiblePlayers = _sortMode switch
        {
            RosterSortMode.Name => filtered.OrderBy(player => player.PlayerName).ToList(),
            RosterSortMode.Position => filtered.OrderBy(player => player.PrimaryPosition).ThenBy(player => player.PlayerName).ToList(),
            _ => filtered.OrderByDescending(player => player.AssignmentLabel == "Organization Roster")
                .ThenByDescending(player => player.AssignmentLabel == "Decision Needed")
                .ThenByDescending(player => player.IsOnFortyMan)
                .ThenBy(player => player.PlayerName)
                .ToList()
        };

        _visibleRosterDirty = false;
        return _cachedVisiblePlayers;
    }

    private void EnsureSelectionIsValid(IReadOnlyList<OrganizationRosterPlayerView> players)
    {
        if (players.Count == 0)
        {
            _selectedPlayerId = null;
            _pageIndex = 0;
            return;
        }

        if (_selectedPlayerId == null || players.All(player => player.PlayerId != _selectedPlayerId.Value))
        {
            _selectedPlayerId = _selectedPlayerIds.FirstOrDefault(playerId => players.Any(player => player.PlayerId == playerId));
            _selectedPlayerId = _selectedPlayerId == Guid.Empty ? players[0].PlayerId : _selectedPlayerId;
        }

        _pageIndex = Math.Clamp(_pageIndex, 0, Math.Max(0, (int)Math.Ceiling(players.Count / (double)GetPageSize()) - 1));
    }

    private OrganizationRosterPlayerView? GetSelectedPlayer(IReadOnlyList<OrganizationRosterPlayerView> players)
    {
        if (_selectedPlayerId == null)
        {
            return players.Count == 0 ? null : players[0];
        }

        return players.FirstOrDefault(player => player.PlayerId == _selectedPlayerId.Value);
    }

    private void TrySelectPlayer(Point mousePosition, IReadOnlyList<OrganizationRosterPlayerView> players)
    {
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleCount = Math.Min(pageSize, Math.Max(0, players.Count - startIndex));

        for (var i = 0; i < visibleCount; i++)
        {
            if (!GetPlayerRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            var player = players[startIndex + i];
            if (GetPlayerCheckboxBounds(i).Contains(mousePosition))
            {
                TogglePlayerSelection(player.PlayerId, players);
            }
            else
            {
                _selectedPlayerIds.Clear();
                _selectedPlayerIds.Add(player.PlayerId);
                _selectedPlayerId = player.PlayerId;
            }

            return;
        }
    }

    private void TogglePlayerSelection(Guid playerId, IReadOnlyList<OrganizationRosterPlayerView> visiblePlayers)
    {
        if (!_selectedPlayerIds.Add(playerId))
        {
            _selectedPlayerIds.Remove(playerId);
        }

        if (_selectedPlayerIds.Count == 0)
        {
            _selectedPlayerId = visiblePlayers.Count == 0 ? null : visiblePlayers[0].PlayerId;
            return;
        }

        _selectedPlayerId = playerId;
    }

    private void ToggleTeamFilterDropdown()
    {
        _showTeamFilterDropdown = !_showTeamFilterDropdown;
        if (_showTeamFilterDropdown)
        {
            _showPositionFilterDropdown = false;
        }
    }

    private void TogglePositionFilterDropdown()
    {
        _showPositionFilterDropdown = !_showPositionFilterDropdown;
        if (_showPositionFilterDropdown)
        {
            _showTeamFilterDropdown = false;
        }
    }

    private void CycleSort()
    {
        _sortMode = _sortMode switch
        {
            RosterSortMode.Status => RosterSortMode.Name,
            RosterSortMode.Name => RosterSortMode.Position,
            _ => RosterSortMode.Status
        };
        _pageIndex = 0;
        ClearSelection();
        InvalidateVisibleRosterCache();
    }

    private void ToggleDetailPanelMode()
    {
        _detailPanelMode = _detailPanelMode == DetailPanelMode.Player
            ? DetailPanelMode.Counts
            : DetailPanelMode.Player;
    }

    private void CycleCompositionMode()
    {
        _compositionMode = _compositionMode switch
        {
            OrganizationRosterCompositionMode.FirstTeam => OrganizationRosterCompositionMode.Depth,
            OrganizationRosterCompositionMode.Depth => OrganizationRosterCompositionMode.Affiliate,
            _ => OrganizationRosterCompositionMode.FirstTeam
        };
    }

    private void CycleAffiliateAssignmentTarget()
    {
        _affiliateAssignmentTarget = _affiliateAssignmentTarget switch
        {
            MinorLeagueAffiliateLevel.TripleA => MinorLeagueAffiliateLevel.DoubleA,
            MinorLeagueAffiliateLevel.DoubleA => MinorLeagueAffiliateLevel.SingleA,
            _ => MinorLeagueAffiliateLevel.TripleA
        };
    }

    private void ToggleMinorLeagueAutomation()
    {
        _statusMessage = _franchiseSession.ToggleSelectedTeamMinorLeagueAutomation();
        SyncSelectionAfterRosterMove();
    }

    private void AssignToFortyMan()
    {
        var selectedPlayers = GetSelectedPlayersForActions();
        if (selectedPlayers.Count == 0)
        {
            _statusMessage = "Select at least one player before managing the 40-man roster.";
            return;
        }

        _franchiseSession.AssignSelectedTeamPlayersToFortyMan(selectedPlayers.Select(player => player.PlayerId).ToList(), out var message);
        _statusMessage = message;
        SyncSelectionAfterRosterMove();
    }

    private void AssignToAffiliate()
    {
        AssignToAffiliate(_affiliateAssignmentTarget);
    }

    private void AssignToAffiliate(MinorLeagueAffiliateLevel affiliateLevel)
    {
        var selectedPlayers = GetSelectedPlayersForActions();
        if (selectedPlayers.Count == 0)
        {
            _statusMessage = $"Select at least one player before sending players to {GetAffiliateTargetLabel(affiliateLevel)}.";
            return;
        }

        _affiliateAssignmentTarget = affiliateLevel;
        _franchiseSession.AssignSelectedTeamPlayersToAffiliate(selectedPlayers.Select(player => player.PlayerId).ToList(), affiliateLevel, out var message);
        _statusMessage = message;
        SyncSelectionAfterRosterMove();
    }

    private void RemoveFromFortyMan()
    {
        var selectedPlayers = GetSelectedPlayersForActions();
        if (selectedPlayers.Count == 0)
        {
            _statusMessage = "Select at least one player before removing players from the 40-man roster.";
            return;
        }

        _franchiseSession.RemoveSelectedTeamPlayersFromFortyMan(selectedPlayers.Select(player => player.PlayerId).ToList(), out var message);
        _statusMessage = message;
        SyncSelectionAfterRosterMove();
    }

    private void ReturnSelectedPlayersToAuto()
    {
        var selectedPlayers = GetSelectedPlayersForActions();
        if (selectedPlayers.Count == 0)
        {
            _statusMessage = "Select at least one player before returning players to AI control.";
            return;
        }

        _franchiseSession.ReturnSelectedTeamPlayersToAutomaticAffiliate(selectedPlayers.Select(player => player.PlayerId).ToList(), out var message);
        _statusMessage = message;
        SyncSelectionAfterRosterMove();
    }

    private void ReleasePlayer()
    {
        var selectedPlayer = GetSelectedPlayer(GetVisiblePlayers());
        if (selectedPlayer == null)
        {
            _statusMessage = "Select a player before releasing someone from the organization.";
            return;
        }

        _franchiseSession.ReleaseSelectedTeamPlayer(selectedPlayer.PlayerId, out var message);
        _statusMessage = message;
        _selectedPlayerIds.Remove(selectedPlayer.PlayerId);
        SyncSelectionAfterRosterMove();
    }

    private void ClearSelection()
    {
        _selectedPlayerIds.Clear();
    }

    private void EnsurePlayerRemainsVisibleAfterAffiliateMove(Guid playerId)
    {
        var organizationPlayers = _franchiseSession.GetSelectedTeamOrganizationRoster();
        if (organizationPlayers.All(player => player.PlayerId != playerId))
        {
            return;
        }

        _selectedPlayerId = playerId;
        if (_teamFilterMode == RosterTeamFilterMode.FortyMan)
        {
            _teamFilterMode = RosterTeamFilterMode.All;
            _pageIndex = 0;
            InvalidateVisibleRosterCache();
        }
    }

    private string GetPlayerListSummary(OrganizationRosterPlayerView player)
    {
        var stats = _franchiseSession.GetPlayerSeasonStats(player.PlayerId);
        var slotLabel = player.RotationSlot.HasValue
            ? $"Rotation {player.RotationSlot.Value}"
            : player.LineupSlot.HasValue
                ? $"Lineup {player.LineupSlot.Value}"
                : player.TeamStatusLabel;
        if (IsPitcher(player.PrimaryPosition))
        {
            return $"{stats.EarnedRunAverageDisplay} ERA | {stats.StrikeoutsPitched} K | {slotLabel}";
        }

        return $"{stats.BattingAverageDisplay} AVG | {stats.OpsDisplay} OPS | {slotLabel}";
    }

    private static string FormatSeasonLine(string primaryPosition, PlayerSeasonStatsState stats)
    {
        if (IsPitcher(primaryPosition))
        {
            return $"GP {stats.GamesPitched} | IP {FormatInnings(stats.InningsPitchedOuts)} | ERA {stats.EarnedRunAverageDisplay} | K {stats.StrikeoutsPitched} | W-L {stats.WinLossDisplay}";
        }

        return $"G {stats.GamesPlayed} | AVG {stats.BattingAverageDisplay} | HR {stats.HomeRuns} | RBI {stats.RunsBattedIn} | OPS {stats.OpsDisplay}";
    }

    private static string FormatRecentLine(string primaryPosition, RecentPlayerStatsView stats)
    {
        if (IsPitcher(primaryPosition))
        {
            return $"Last {stats.SampleGames} G: {stats.InningsPitchedDisplay} IP, {stats.EraDisplay} ERA, {stats.PitcherStrikeouts} K, {stats.Wins}-{stats.Losses}";
        }

        return $"Last {stats.SampleGames} G: {stats.BattingAverageDisplay} AVG, {stats.HomeRuns} HR, {stats.OpsDisplay} OPS";
    }

    private static string GetTeamFilterLabel(RosterTeamFilterMode filterMode)
    {
        return filterMode switch
        {
            RosterTeamFilterMode.FirstTeam => "First Team",
            RosterTeamFilterMode.FortyMan => "40-Man",
            RosterTeamFilterMode.Affiliate => "Affiliate",
            RosterTeamFilterMode.TripleA => "AAA",
            RosterTeamFilterMode.DoubleA => "AA",
            RosterTeamFilterMode.SingleA => "A",
            _ => "All"
        };
    }

    private static string GetPositionFilterLabel(RosterPositionFilterMode filterMode)
    {
        return filterMode switch
        {
            RosterPositionFilterMode.Hitters => "Hitters",
            RosterPositionFilterMode.Pitchers => "Pitchers",
            RosterPositionFilterMode.Catcher => "C",
            RosterPositionFilterMode.FirstBase => "1B",
            RosterPositionFilterMode.SecondBase => "2B",
            RosterPositionFilterMode.ThirdBase => "3B",
            RosterPositionFilterMode.Shortstop => "SS",
            RosterPositionFilterMode.LeftField => "LF",
            RosterPositionFilterMode.CenterField => "CF",
            RosterPositionFilterMode.RightField => "RF",
            RosterPositionFilterMode.DesignatedHitter => "DH",
            RosterPositionFilterMode.StartingPitcher => "SP",
            RosterPositionFilterMode.ReliefPitcher => "RP",
            _ => "All"
        };
    }

    private static IReadOnlyList<RosterTeamFilterMode> GetTeamFilterOptions()
    {
        return
        [
            RosterTeamFilterMode.All,
            RosterTeamFilterMode.FirstTeam,
            RosterTeamFilterMode.FortyMan,
            RosterTeamFilterMode.Affiliate,
            RosterTeamFilterMode.TripleA,
            RosterTeamFilterMode.DoubleA,
            RosterTeamFilterMode.SingleA
        ];
    }

    private static IReadOnlyList<RosterPositionFilterMode> GetPositionFilterOptions()
    {
        return
        [
            RosterPositionFilterMode.All,
            RosterPositionFilterMode.Hitters,
            RosterPositionFilterMode.Pitchers,
            RosterPositionFilterMode.Catcher,
            RosterPositionFilterMode.FirstBase,
            RosterPositionFilterMode.SecondBase,
            RosterPositionFilterMode.ThirdBase,
            RosterPositionFilterMode.Shortstop,
            RosterPositionFilterMode.LeftField,
            RosterPositionFilterMode.CenterField,
            RosterPositionFilterMode.RightField,
            RosterPositionFilterMode.DesignatedHitter,
            RosterPositionFilterMode.StartingPitcher,
            RosterPositionFilterMode.ReliefPitcher
        ];
    }

    private bool TrySelectTeamFilterOption(Point mousePosition)
    {
        var options = GetTeamFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            if (!GetTeamFilterOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _teamFilterMode = options[i];
            _pageIndex = 0;
            _showTeamFilterDropdown = false;
            ClearSelection();
            InvalidateVisibleRosterCache();
            return true;
        }

        _showTeamFilterDropdown = false;
        return false;
    }

    private bool TrySelectPositionFilterOption(Point mousePosition)
    {
        var options = GetPositionFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            if (!GetPositionFilterOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _positionFilterMode = options[i];
            _pageIndex = 0;
            _showPositionFilterDropdown = false;
            ClearSelection();
            InvalidateVisibleRosterCache();
            return true;
        }

        _showPositionFilterDropdown = false;
        return false;
    }

    private static bool MatchesRosterPositionFilter(OrganizationRosterPlayerView player, string filter)
    {
        return PositionMatchesFilter(player.PrimaryPosition, filter)
            || PositionMatchesFilter(player.SecondaryPosition, filter);
    }

    private static bool PositionMatchesFilter(string position, string filter)
    {
        if (string.Equals(filter, "SP", StringComparison.OrdinalIgnoreCase) || string.Equals(filter, "RP", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(position, filter, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(position, filter, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOverlayCapturingMouse()
    {
        return _showTeamFilterDropdown || _showPositionFilterDropdown || _playerContextOverlay.IsCapturingMouse;
    }

    private OrganizationRosterCompositionView GetCurrentRosterComposition()
    {
        return _teamFilterMode switch
        {
            RosterTeamFilterMode.TripleA => _franchiseSession.GetSelectedTeamAffiliateRosterComposition(MinorLeagueAffiliateLevel.TripleA),
            RosterTeamFilterMode.DoubleA => _franchiseSession.GetSelectedTeamAffiliateRosterComposition(MinorLeagueAffiliateLevel.DoubleA),
            RosterTeamFilterMode.SingleA => _franchiseSession.GetSelectedTeamAffiliateRosterComposition(MinorLeagueAffiliateLevel.SingleA),
            _ => _franchiseSession.GetSelectedTeamRosterComposition(_compositionMode)
        };
    }

    private string GetRosterViewLabel()
    {
        return _teamFilterMode switch
        {
            RosterTeamFilterMode.TripleA => $"{_franchiseSession.SelectedTeamName} AAA Roster",
            RosterTeamFilterMode.DoubleA => $"{_franchiseSession.SelectedTeamName} AA Roster",
            RosterTeamFilterMode.SingleA => $"{_franchiseSession.SelectedTeamName} A Roster",
            RosterTeamFilterMode.FirstTeam => $"{_franchiseSession.SelectedTeamName} First Team",
            RosterTeamFilterMode.FortyMan => $"{_franchiseSession.SelectedTeamName} 40-Man",
            _ => "Organization Roster"
        };
    }

    private string GetEmptyRosterMessage()
    {
        return _teamFilterMode switch
        {
            RosterTeamFilterMode.TripleA => "No players are currently assigned to AAA.",
            RosterTeamFilterMode.DoubleA => "No players are currently assigned to AA.",
            RosterTeamFilterMode.SingleA => "No players are currently assigned to A.",
            RosterTeamFilterMode.FirstTeam => "No players are currently on the first team.",
            _ => "No roster players match the current filter."
        };
    }

    private bool TryOpenPlayerContextMenu(Point mousePosition, IReadOnlyList<OrganizationRosterPlayerView> players)
    {
        var pageSize = GetPageSize();
        var startIndex = _pageIndex * pageSize;
        var visibleCount = Math.Min(pageSize, Math.Max(0, players.Count - startIndex));

        for (var i = 0; i < visibleCount; i++)
        {
            if (!GetPlayerRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            var player = players[startIndex + i];
            _selectedPlayerId = player.PlayerId;
            _selectedPlayerIds.Clear();
            _selectedPlayerIds.Add(player.PlayerId);

            var rosterActions = new List<PlayerContextActionView>
            {
                new(PlayerContextAction.AssignToFortyMan, "Add To 40-Man", player.CanAssignToFortyMan),
                new(PlayerContextAction.AssignToTripleA, "Send To AAA", player.AffiliateLevel != MinorLeagueAffiliateLevel.TripleA),
                new(PlayerContextAction.AssignToDoubleA, "Send To AA", player.AffiliateLevel != MinorLeagueAffiliateLevel.DoubleA),
                new(PlayerContextAction.AssignToSingleA, "Send To A", player.AffiliateLevel != MinorLeagueAffiliateLevel.SingleA),
                new(PlayerContextAction.ReturnToAutomaticAffiliate, "Return To AI", player.CanReturnToAutomaticAffiliate),
                new(PlayerContextAction.RemoveFromFortyMan, "Remove From 40-Man", player.IsOnFortyMan),
                new(PlayerContextAction.ReleasePlayer, "Release", player.CanRelease)
            };

            var primaryActions = new List<PlayerContextActionView>
            {
                new(PlayerContextAction.OpenRosterAssignments, "Roster", rosterActions.Any(action => action.IsEnabled)),
                new(PlayerContextAction.OpenProfile, "Profile", true)
            };

            _playerContextOverlay.Open(mousePosition, player.PlayerName, primaryActions, rosterActions, _franchiseSession.GetPlayerProfile(player.PlayerId));
            return true;
        }

        return false;
    }

    private void ExecuteContextAction(PlayerContextAction action)
    {
        switch (action)
        {
            case PlayerContextAction.AssignToFortyMan:
                AssignToFortyMan();
                break;
            case PlayerContextAction.AssignToTripleA:
                AssignToAffiliate(MinorLeagueAffiliateLevel.TripleA);
                break;
            case PlayerContextAction.AssignToDoubleA:
                AssignToAffiliate(MinorLeagueAffiliateLevel.DoubleA);
                break;
            case PlayerContextAction.AssignToSingleA:
                AssignToAffiliate(MinorLeagueAffiliateLevel.SingleA);
                break;
            case PlayerContextAction.ReturnToAutomaticAffiliate:
                ReturnSelectedPlayersToAuto();
                break;
            case PlayerContextAction.RemoveFromFortyMan:
                RemoveFromFortyMan();
                break;
            case PlayerContextAction.ReleasePlayer:
                ReleasePlayer();
                break;
        }
    }

    private static string GetSortLabel(RosterSortMode sortMode)
    {
        return sortMode switch
        {
            RosterSortMode.Name => "Name",
            RosterSortMode.Position => "Position",
            _ => "Status"
        };
    }

    private static string GetCompositionModeLabel(OrganizationRosterCompositionMode mode)
    {
        return mode switch
        {
            OrganizationRosterCompositionMode.Depth => "Depth",
            OrganizationRosterCompositionMode.Affiliate => "Affiliates",
            _ => "First Team"
        };
    }

    private static string GetAffiliateTargetLabel(MinorLeagueAffiliateLevel affiliateLevel)
    {
        return affiliateLevel switch
        {
            MinorLeagueAffiliateLevel.DoubleA => "AA",
            MinorLeagueAffiliateLevel.SingleA => "A",
            _ => "AAA"
        };
    }

    private IReadOnlyList<OrganizationRosterPlayerView> GetSelectedPlayersForActions()
    {
        var organizationPlayers = GetOrganizationPlayers();
        var selectedPlayers = organizationPlayers
            .Where(player => _selectedPlayerIds.Contains(player.PlayerId))
            .ToList();

        if (selectedPlayers.Count > 0)
        {
            return selectedPlayers;
        }

        if (!_selectedPlayerId.HasValue)
        {
            return [];
        }

        var selectedPlayer = organizationPlayers.FirstOrDefault(player => player.PlayerId == _selectedPlayerId.Value);
        return selectedPlayer == null ? [] : [selectedPlayer];
    }

    private void SyncSelectionAfterRosterMove()
    {
        InvalidateRosterCaches();
        var organizationPlayers = GetOrganizationPlayers();
        var organizationPlayerIds = organizationPlayers.Select(player => player.PlayerId).ToHashSet();
        _selectedPlayerIds.RemoveWhere(playerId => !organizationPlayerIds.Contains(playerId));

        if (_selectedPlayerId.HasValue && !organizationPlayerIds.Contains(_selectedPlayerId.Value))
        {
            _selectedPlayerId = null;
        }

        if (_teamFilterMode == RosterTeamFilterMode.FortyMan && _selectedPlayerIds.Any(playerId => organizationPlayers.All(player => player.PlayerId != playerId || !player.IsOnFortyMan)))
        {
            _teamFilterMode = RosterTeamFilterMode.All;
            _pageIndex = 0;
            InvalidateVisibleRosterCache();
        }
    }

    private IReadOnlyList<OrganizationRosterPlayerView> GetOrganizationPlayers()
    {
        if (_organizationRosterDirty)
        {
            _cachedOrganizationPlayers = _franchiseSession.GetSelectedTeamOrganizationRoster();
            _organizationRosterDirty = false;
            _visibleRosterDirty = true;
            _contractsDirty = true;
        }

        return _cachedOrganizationPlayers;
    }

    private IReadOnlyDictionary<Guid, Contract> GetContractsByPlayerId()
    {
        if (_contractsDirty)
        {
            _cachedContractsByPlayerId = _franchiseSession.GetSelectedTeamEconomy()
                .PlayerContracts
                .ToDictionary(contract => contract.SubjectId, contract => contract);
            _contractsDirty = false;
        }

        return _cachedContractsByPlayerId;
    }

    private void InvalidateRosterCaches()
    {
        _organizationRosterDirty = true;
        _visibleRosterDirty = true;
        _contractsDirty = true;
    }

    private void InvalidateVisibleRosterCache()
    {
        _visibleRosterDirty = true;
    }

    private static bool IsPitcher(string position)
    {
        return position is "SP" or "RP";
    }

    private static string FormatInnings(int inningsPitchedOuts)
    {
        return $"{inningsPitchedOuts / 3}.{inningsPitchedOuts % 3}";
    }

    private static string FormatSalary(decimal salary)
    {
        return salary >= 1_000_000m ? $"${salary / 1_000_000m:0.0}M" : $"${salary / 1_000m:0}K";
    }

    private Rectangle GetBackButtonBounds() => ScreenLayout.BackButtonBounds(_viewport);

    private Rectangle GetTeamFilterButtonBounds()
    {
        var (teamWidth, _, _, _, _, _, _, _, _, _, _, _) = GetToolbarLayoutMetrics();
        return new Rectangle(48, ScreenLayout.ToolbarY(_viewport), teamWidth, ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetTeamFilterOptionBounds(int index)
    {
        var buttonBounds = GetTeamFilterButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Y - 4 - ((index + 1) * 30), buttonBounds.Width, 26);
    }

    private Rectangle GetPositionFilterButtonBounds()
    {
        var (_, teamGap, _, positionWidth, _, _, _, _, _, _, _, _) = GetToolbarLayoutMetrics();
        var teamBounds = GetTeamFilterButtonBounds();
        return new Rectangle(teamBounds.Right + teamGap, ScreenLayout.ToolbarY(_viewport), positionWidth, ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetPositionFilterOptionBounds(int index)
    {
        var buttonBounds = GetPositionFilterButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Y - 4 - ((index + 1) * 30), buttonBounds.Width, 26);
    }

    private Rectangle GetSortButtonBounds()
    {
        var (_, _, positionGap, _, sortWidth, _, _, _, _, _, _, _) = GetToolbarLayoutMetrics();
        var positionBounds = GetPositionFilterButtonBounds();
        return new Rectangle(positionBounds.Right + positionGap, ScreenLayout.ToolbarY(_viewport), sortWidth, ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetClearSelectionBounds()
    {
        var (_, _, _, _, _, sortGap, clearWidth, _, _, _, _, _) = GetToolbarLayoutMetrics();
        var sortBounds = GetSortButtonBounds();
        return new Rectangle(sortBounds.Right + sortGap, ScreenLayout.ToolbarY(_viewport), clearWidth, ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetReleaseButtonBounds()
    {
        var removeBounds = GetRemoveFromFortyManBounds();
        var (_, _, _, _, _, _, _, _, _, _, _, releaseWidth) = GetToolbarLayoutMetrics();
        var x = removeBounds.Right + GetSecondaryToolbarGap();
        return new Rectangle(x, GetSecondaryToolbarY(), releaseWidth, ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetRemoveFromFortyManBounds()
    {
        var (_, _, _, _, _, _, _, secondaryStartX, returnToAutoWidth, assign40ManWidth, affiliateTargetWidth, _) = GetToolbarLayoutMetrics();
        var x = GetSecondaryToolbarXOffset(secondaryStartX) + returnToAutoWidth + GetSecondaryToolbarGap() + assign40ManWidth + GetSecondaryToolbarGap() + affiliateTargetWidth + GetSecondaryToolbarGap() + GetAssignAffiliateWidth() + GetSecondaryToolbarGap();
        return new Rectangle(x, GetSecondaryToolbarY(), GetRemoveFromFortyManWidth(), ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetAssignAffiliateBounds()
    {
        var (_, _, _, _, _, _, _, secondaryStartX, returnToAutoWidth, assign40ManWidth, affiliateTargetWidth, _) = GetToolbarLayoutMetrics();
        var x = GetSecondaryToolbarXOffset(secondaryStartX) + returnToAutoWidth + GetSecondaryToolbarGap() + assign40ManWidth + GetSecondaryToolbarGap() + affiliateTargetWidth + GetSecondaryToolbarGap();
        return new Rectangle(x, GetSecondaryToolbarY(), GetAssignAffiliateWidth(), ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetAffiliateTargetBounds()
    {
        var (_, _, _, _, _, _, _, secondaryStartX, returnToAutoWidth, assign40ManWidth, _, _) = GetToolbarLayoutMetrics();
        var x = GetSecondaryToolbarXOffset(secondaryStartX) + returnToAutoWidth + GetSecondaryToolbarGap() + assign40ManWidth + GetSecondaryToolbarGap();
        return new Rectangle(x, GetSecondaryToolbarY(), GetAffiliateTargetWidth(), ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetAssign40ManBounds()
    {
        var (_, _, _, _, _, _, _, secondaryStartX, returnToAutoWidth, _, _, _) = GetToolbarLayoutMetrics();
        var x = GetSecondaryToolbarXOffset(secondaryStartX) + returnToAutoWidth + GetSecondaryToolbarGap();
        return new Rectangle(x, GetSecondaryToolbarY(), GetAssign40ManWidth(), ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetReturnToAutoBounds()
    {
        var (_, _, _, _, _, _, _, secondaryStartX, returnToAutoWidth, _, _, _) = GetToolbarLayoutMetrics();
        return new Rectangle(GetSecondaryToolbarXOffset(secondaryStartX), GetSecondaryToolbarY(), returnToAutoWidth, ScreenLayout.ToolbarButtonHeight(_viewport));
    }

    private Rectangle GetDetailModeBounds() => GetDetailActionLayout().DetailModeBounds;

    private Rectangle GetCompositionModeBounds() => GetDetailActionLayout().CompositionModeBounds;

    private Rectangle GetMinorLeagueAutomationBounds() => GetDetailActionLayout().AutomationModeBounds;

    private Rectangle GetDetailBodyBounds()
    {
        var bounds = GetDetailPanelBounds();
        var actionLayout = GetDetailActionLayout();
        var controlsBottom = Math.Max(actionLayout.DetailModeBounds.Bottom, Math.Max(actionLayout.CompositionModeBounds.Bottom, actionLayout.AutomationModeBounds.Bottom));
        var bodyY = controlsBottom + 8;
        var bodyHeight = Math.Max(120, bounds.Bottom - bodyY - 12);
        return new Rectangle(bounds.X + 12, bodyY, bounds.Width - 24, bodyHeight);
    }

    private Rectangle GetListPanelBounds()
    {
        var y = ScreenLayout.ContentTop(_viewport);
        var availableHeight = GetSecondaryToolbarY() - y - 16;
        var height = Math.Max(280, availableHeight);
        return new Rectangle(48, y, Math.Max(560, _viewport.X - 540), height);
    }

    private Rectangle GetDetailPanelBounds()
    {
        var listBounds = GetListPanelBounds();
        return new Rectangle(listBounds.Right + 18, listBounds.Y, Math.Max(360, _viewport.X - listBounds.Right - 66), listBounds.Height);
    }

    private int GetPageSize()
    {
        return Math.Max(6, (GetListPanelBounds().Height - 92) / 44);
    }

    private Rectangle GetPlayerRowBounds(int visibleIndex)
    {
        var panelBounds = GetListPanelBounds();
        return new Rectangle(panelBounds.X + 10, panelBounds.Y + 36 + (visibleIndex * 44), panelBounds.Width - 20, 38);
    }

    private Rectangle GetPlayerCheckboxBounds(int visibleIndex)
    {
        var rowBounds = GetPlayerRowBounds(visibleIndex);
        return new Rectangle(rowBounds.X + 8, rowBounds.Y + 9, 18, 18);
    }

    private Rectangle GetPreviousPageBounds()
    {
        var panelBounds = GetListPanelBounds();
        return new Rectangle(panelBounds.X + 10, panelBounds.Bottom - 40, 60, 28);
    }

    private Rectangle GetNextPageBounds()
    {
        var previous = GetPreviousPageBounds();
        return new Rectangle(previous.Right + 126, previous.Y, 60, 28);
    }

    private int GetSecondaryToolbarY()
    {
        return ScreenLayout.ToolbarY(_viewport) - ScreenLayout.ToolbarButtonHeight(_viewport) - 8;
    }

    private int GetSecondaryToolbarGap()
    {
        return Math.Clamp(_viewport.X / 120, 4, 12);
    }

    private int GetAssignAffiliateWidth()
    {
        return Math.Max(96, (int)(136 * GetToolbarScale()));
    }

    private int GetRemoveFromFortyManWidth()
    {
        return Math.Max(120, (int)(170 * GetToolbarScale()));
    }

    private int GetAffiliateTargetWidth()
    {
        return Math.Max(88, (int)(116 * GetToolbarScale()));
    }

    private int GetAssign40ManWidth()
    {
        return Math.Max(110, (int)(140 * GetToolbarScale()));
    }

    private float GetToolbarScale()
    {
        var baseSecondaryWidths = 132 + 140 + 116 + 136 + 170 + 100;
        var available = Math.Max(520, _viewport.X - 96);
        var gapTotal = GetSecondaryToolbarGap() * 5;
        var scale = (available - gapTotal) / (float)baseSecondaryWidths;
        return Math.Clamp(scale, 0.72f, 1f);
    }

    private int GetSecondaryToolbarXOffset(int secondaryWidth)
    {
        // Keep both bottom button rows aligned to the same left margin.
        // Width scaling already guarantees the secondary row fits in the viewport.
        _ = secondaryWidth;
        return GetTeamFilterButtonBounds().X;
    }

    private (int TeamWidth, int TeamGap, int PositionGap, int PositionWidth, int SortWidth, int SortGap, int ClearWidth, int SecondaryWidth, int ReturnToAutoWidth, int Assign40ManWidth, int AffiliateTargetWidth, int ReleaseWidth) GetToolbarLayoutMetrics()
    {
        var primaryGap = Math.Clamp(_viewport.X / 160, 6, 12);
        var secondaryGap = GetSecondaryToolbarGap();
        var scale = GetToolbarScale();

        var teamWidth = Math.Max(128, (int)(150 * scale));
        var positionWidth = Math.Max(120, (int)(140 * scale));
        var sortWidth = Math.Max(120, (int)(140 * scale));
        var clearWidth = Math.Max(112, (int)(130 * scale));

        var returnToAutoWidth = Math.Max(110, (int)(132 * scale));
        var assign40ManWidth = GetAssign40ManWidth();
        var affiliateTargetWidth = GetAffiliateTargetWidth();
        var assignAffiliateWidth = GetAssignAffiliateWidth();
        var removeFromFortyManWidth = GetRemoveFromFortyManWidth();
        var releaseWidth = Math.Max(84, (int)(100 * scale));

        var secondaryWidth =
            returnToAutoWidth + secondaryGap +
            assign40ManWidth + secondaryGap +
            affiliateTargetWidth + secondaryGap +
            assignAffiliateWidth + secondaryGap +
            removeFromFortyManWidth + secondaryGap +
            releaseWidth;

        return (teamWidth, primaryGap, primaryGap, positionWidth, sortWidth, primaryGap, clearWidth, secondaryWidth, returnToAutoWidth, assign40ManWidth, affiliateTargetWidth, releaseWidth);
    }

    private DetailActionLayout GetDetailActionLayout()
    {
        var detailBounds = GetDetailPanelBounds();
        var x = detailBounds.X + 12;
        var y = detailBounds.Y + 32;
        var rowHeight = 28;
        var gap = 10;
        var available = Math.Max(220, detailBounds.Width - 24);

        if (available >= 420)
        {
            var detailMode = new Rectangle(x, y, 116, rowHeight);
            var composition = new Rectangle(detailMode.Right + gap, y, 148, rowHeight);
            var automation = new Rectangle(composition.Right + gap, y, 136, rowHeight);
            return new DetailActionLayout(detailMode, composition, automation);
        }

        if (available >= 340)
        {
            var scale = available / 420f;
            var detailWidth = Math.Max(92, (int)(116 * scale));
            var compositionWidth = Math.Max(110, (int)(148 * scale));
            var automationWidth = Math.Max(96, available - detailWidth - compositionWidth - (gap * 2));
            var detailMode = new Rectangle(x, y, detailWidth, rowHeight);
            var composition = new Rectangle(detailMode.Right + gap, y, compositionWidth, rowHeight);
            var automation = new Rectangle(composition.Right + gap, y, automationWidth, rowHeight);
            return new DetailActionLayout(detailMode, composition, automation);
        }

        var firstRowWidth = (available - gap) / 2;
        var detailRow = new Rectangle(x, y, Math.Max(96, firstRowWidth), rowHeight);
        var compositionRow = new Rectangle(detailRow.Right + gap, y, Math.Max(110, available - detailRow.Width - gap), rowHeight);
        var automationRow = new Rectangle(x, y + rowHeight + 6, available, rowHeight);
        return new DetailActionLayout(detailRow, compositionRow, automationRow);
    }
}
