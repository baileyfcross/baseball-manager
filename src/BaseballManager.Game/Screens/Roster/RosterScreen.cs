using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.UI.Controls;
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

    private enum RosterFilterMode
    {
        All,
        FortyMan,
        Affiliate,
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

    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _filterButton;
    private readonly ButtonControl _sortButton;
    private readonly ButtonControl _clearSelectionButton;
    private readonly ButtonControl _detailModeButton;
    private readonly ButtonControl _compositionModeButton;
    private readonly ButtonControl _assign40ManButton;
    private readonly ButtonControl _assignAffiliateButton;
    private readonly ButtonControl _removeFromFortyManButton;
    private readonly ButtonControl _releaseButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private bool _showFilterDropdown;
    private Point _viewport = new(1280, 720);
    private Guid? _selectedPlayerId;
    private readonly HashSet<Guid> _selectedPlayerIds = [];
    private int _pageIndex;
    private DetailPanelMode _detailPanelMode = DetailPanelMode.Player;
    private OrganizationRosterCompositionMode _compositionMode = OrganizationRosterCompositionMode.FirstTeam;
    private RosterFilterMode _filterMode = RosterFilterMode.All;
    private RosterSortMode _sortMode = RosterSortMode.Status;
    private string _statusMessage = "Review player stats, add or remove players from the 40-man roster, keep them in the organization, move them to the affiliate, or switch to Counts to review the 26-man and affiliate mix.";

    public RosterScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl { Label = "Back", OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen)) };
        _filterButton = new ButtonControl { Label = "Filter: All", OnClick = ToggleFilterDropdown };
        _sortButton = new ButtonControl { Label = "Sort: Status", OnClick = CycleSort };
        _clearSelectionButton = new ButtonControl { Label = "Clear Select", OnClick = ClearSelection };
        _detailModeButton = new ButtonControl { Label = "View: Player", OnClick = ToggleDetailPanelMode };
        _compositionModeButton = new ButtonControl { Label = "Scope: First Team", OnClick = CycleCompositionMode };
        _assign40ManButton = new ButtonControl { Label = "Add To 40-Man", OnClick = AssignToFortyMan };
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
        _filterMode = RosterFilterMode.All;
        _sortMode = RosterSortMode.Status;
        _showFilterDropdown = false;
        _selectedPlayerId = null;
        _selectedPlayerIds.Clear();
        _statusMessage = "Review player stats, add or remove players from the 40-man roster, keep them in the organization, move them to the affiliate, or switch to Counts to review the 26-man and affiliate mix.";
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

        if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePosition = currentMouseState.Position;
            if (GetBackButtonBounds().Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetFilterButtonBounds().Contains(mousePosition))
            {
                _filterButton.Click();
            }
            else if (_showFilterDropdown)
            {
                TrySelectFilterOption(mousePosition);
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

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);
        var mousePosition = Mouse.GetState().Position;
        var players = GetVisiblePlayers();
        EnsureSelectionIsValid(players);
        var selectedPlayer = GetSelectedPlayer(players);
        var contractsByPlayerId = _franchiseSession.GetSelectedTeamEconomy().PlayerContracts.ToDictionary(contract => contract.SubjectId, contract => contract);

        _filterButton.Label = _showFilterDropdown
            ? $"Filter: {GetFilterLabel(_filterMode)} ^"
            : $"Filter: {GetFilterLabel(_filterMode)} v";
        _sortButton.Label = $"Sort: {GetSortLabel(_sortMode)}";
        _detailModeButton.Label = _detailPanelMode == DetailPanelMode.Player ? "View: Player" : "View: Counts";
        _compositionModeButton.Label = $"Scope: {GetCompositionModeLabel(_compositionMode)}";

        uiRenderer.DrawText("Roster", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, 360, 22), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(48, 112, Math.Max(640, _viewport.X - 96), 42), Color.White, uiRenderer.UiSmallFont, 2);

        var listBounds = GetListPanelBounds();
        var detailBounds = GetDetailPanelBounds();
        uiRenderer.DrawButton(string.Empty, listBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, detailBounds, new Color(38, 48, 56), Color.White);
        var selectedPlayers = GetSelectedPlayersForActions();
        var selectedLabel = selectedPlayers.Count > 1 ? $"Organization Roster ({selectedPlayers.Count} selected)" : "Organization Roster";
        uiRenderer.DrawTextInBounds(selectedLabel, new Rectangle(listBounds.X + 12, listBounds.Y + 8, listBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(_detailPanelMode == DetailPanelMode.Player ? "Player Details" : "Roster Counts", new Rectangle(detailBounds.X + 12, detailBounds.Y + 8, detailBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        DrawDetailPanelButtons(uiRenderer, mousePosition);

        if (players.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds("No roster players match the current filter.", new Rectangle(listBounds.X + 12, listBounds.Y + 40, listBounds.Width - 24, 60), Color.White, uiRenderer.UiSmallFont, 3);
            if (_detailPanelMode == DetailPanelMode.Counts)
            {
                DrawRosterComposition(uiRenderer, _franchiseSession.GetSelectedTeamRosterComposition(_compositionMode));
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
                DrawRosterComposition(uiRenderer, _franchiseSession.GetSelectedTeamRosterComposition(_compositionMode));
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

        if (_showFilterDropdown)
        {
            DrawFilterDropdown(uiRenderer);
        }
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
        uiRenderer.DrawTextInBounds(player.PlayerName, new Rectangle(content.X, content.Y, content.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"{player.PrimaryPosition}/{player.SecondaryPosition} | Age {player.Age} | {player.AssignmentLabel}", new Rectangle(content.X, content.Y + 24, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"40-Man Spots Used: {_franchiseSession.GetSelectedTeam40ManCount()}/40", new Rectangle(content.X, content.Y + 48, content.Width, 18), Color.White, uiRenderer.UiSmallFont);

        var slotLabel = player.RotationSlot.HasValue
            ? $"Rotation Slot: {player.RotationSlot.Value}"
            : player.LineupSlot.HasValue
                ? $"Lineup Slot: {player.LineupSlot.Value}"
                : "Not in current lineup or rotation";
        uiRenderer.DrawTextInBounds(slotLabel, new Rectangle(content.X, content.Y + 72, content.Width, 18), Color.White, uiRenderer.UiSmallFont);

        uiRenderer.DrawTextInBounds("Current Season", new Rectangle(content.X, content.Y + 104, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(FormatSeasonLine(player.PrimaryPosition, currentStats), new Rectangle(content.X, content.Y + 126, content.Width, 36), Color.White, uiRenderer.UiSmallFont, 2);

        uiRenderer.DrawTextInBounds("Last Season", new Rectangle(content.X, content.Y + 170, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(FormatSeasonLine(player.PrimaryPosition, lastSeasonStats), new Rectangle(content.X, content.Y + 192, content.Width, 36), Color.White, uiRenderer.UiSmallFont, 2);

        uiRenderer.DrawTextInBounds("Recent Form", new Rectangle(content.X, content.Y + 236, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(FormatRecentLine(player.PrimaryPosition, recentStats), new Rectangle(content.X, content.Y + 258, content.Width, 36), Color.White, uiRenderer.UiSmallFont, 2);

        uiRenderer.DrawTextInBounds("Contract", new Rectangle(content.X, content.Y + 302, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(contract == null ? "No active contract on file." : $"{FormatSalary(contract.AnnualSalary)} for {contract.YearsRemaining} year(s) remaining.", new Rectangle(content.X, content.Y + 324, content.Width, 18), Color.White, uiRenderer.UiSmallFont);

        var optionsLabel = player.IsDraftedPlayer
            ? $"Minor-League Options Remaining: {player.MinorLeagueOptionsRemaining}"
            : "Veteran import: affiliate and 40-man moves are enabled here; option years are not tracked yet.";
        uiRenderer.DrawTextInBounds(optionsLabel, new Rectangle(content.X, content.Y + 348, content.Width, 18), Color.White, uiRenderer.UiSmallFont);

        uiRenderer.DrawTextInBounds($"Medical: {medicalStatus}", new Rectangle(content.X, content.Y + 380, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(medicalReport, new Rectangle(content.X, content.Y + 402, content.Width, 52), Color.White, uiRenderer.UiSmallFont, 3);

        uiRenderer.DrawTextInBounds("Scout Note", new Rectangle(content.X, content.Y + 462, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(scoutNote, new Rectangle(content.X, content.Y + 484, content.Width, 52), Color.White, uiRenderer.UiSmallFont, 3);
    }

    private void DrawSelectionSummary(UiRenderer uiRenderer, IReadOnlyList<OrganizationRosterPlayerView> selectedPlayers)
    {
        var content = GetDetailBodyBounds();
        var firstTeamCount = selectedPlayers.Count(player => player.IsOnFirstTeam);
        var affiliateCount = selectedPlayers.Count(player => player.TeamStatusLabel == "Affiliate");
        var depthCount = selectedPlayers.Count(player => player.TeamStatusLabel == "Organization Depth");
        var fortyManCount = selectedPlayers.Count(player => player.IsOnFortyMan);

        uiRenderer.DrawTextInBounds($"{selectedPlayers.Count} Players Selected", new Rectangle(content.X, content.Y, content.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"First Team {firstTeamCount} | Depth {depthCount} | Affiliate {affiliateCount}", new Rectangle(content.X, content.Y + 24, content.Width, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"40-Man {fortyManCount} | Non-40-Man {selectedPlayers.Count - fortyManCount}", new Rectangle(content.X, content.Y + 48, content.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds("Use the roster action buttons to apply moves to the full selection. Click a checkbox to add or remove players from the batch, or use Clear Select to reset.", new Rectangle(content.X, content.Y + 82, content.Width, 60), Color.White, uiRenderer.UiSmallFont, 3);

        var listY = content.Y + 156;
        foreach (var player in selectedPlayers.Take(8))
        {
            uiRenderer.DrawTextInBounds($"{player.PlayerName} | {player.PrimaryPosition}/{player.SecondaryPosition}".TrimEnd('/'), new Rectangle(content.X, listY, content.Width, 16), player.IsOnFirstTeam ? Color.Gold : Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds($"{player.TeamStatusLabel} | {player.AssignmentLabel}", new Rectangle(content.X + 8, listY + 16, content.Width - 8, 16), Color.White, uiRenderer.UiSmallFont);
            listY += 40;
        }
    }

    private void DrawRosterComposition(UiRenderer uiRenderer, OrganizationRosterCompositionView composition)
    {
        var bounds = GetDetailPanelBounds();
        var content = new Rectangle(bounds.X + 12, bounds.Y + 72, bounds.Width - 24, bounds.Height - 84);
        uiRenderer.DrawTextInBounds(composition.Title, new Rectangle(content.X, content.Y, content.Width, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(composition.Summary, new Rectangle(content.X, content.Y + 22, content.Width, 54), Color.Gold, uiRenderer.UiSmallFont, 3);

        var totalLabel = composition.TargetCount.HasValue
            ? $"Total: {composition.TotalCount}/{composition.TargetCount.Value}"
            : $"Total: {composition.TotalCount}";
        uiRenderer.DrawTextInBounds(totalLabel, new Rectangle(content.X, content.Y + 84, content.Width, 18), Color.White, uiRenderer.UiSmallFont);

        for (var i = 0; i < composition.Buckets.Count; i++)
        {
            var bucket = composition.Buckets[i];
            var valueLabel = bucket.TargetCount.HasValue
                ? $"{bucket.Count}/{bucket.TargetCount.Value}"
                : bucket.Count.ToString();
            var rowTop = content.Y + 122 + GetCompositionRowOffset(composition.Buckets, i);
            var rowBounds = new Rectangle(content.X, rowTop, content.Width, 18);
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
                uiRenderer.DrawTextInBounds(detail.Label, new Rectangle(detailBounds.X, detailBounds.Y, detailBounds.Width - 84, detailBounds.Height), Color.LightGray, uiRenderer.UiSmallFont);
                uiRenderer.DrawTextInBounds(detail.Count.ToString(), new Rectangle(detailBounds.Right - 84, detailBounds.Y, 84, detailBounds.Height), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            }
        }
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
        uiRenderer.DrawButton(_detailModeButton.Label, detailModeBounds, !suppressHover && detailModeBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_compositionModeButton.Label, compositionModeBounds, !suppressHover && compositionModeBounds.Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
    }

    private void DrawButtons(UiRenderer uiRenderer, Point mousePosition, OrganizationRosterPlayerView? selectedPlayer)
    {
        var suppressHover = _showFilterDropdown;
        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), !suppressHover && GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_filterButton.Label, GetFilterButtonBounds(), GetFilterButtonBounds().Contains(mousePosition) || _showFilterDropdown ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_sortButton.Label, GetSortButtonBounds(), !suppressHover && GetSortButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_clearSelectionButton.Label, GetClearSelectionBounds(), _selectedPlayerIds.Count > 0 && !suppressHover && GetClearSelectionBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (_selectedPlayerIds.Count > 0 ? Color.SlateBlue : new Color(76, 76, 76)), _selectedPlayerIds.Count > 0 ? Color.White : new Color(188, 188, 188));

        var actionTargets = GetSelectedPlayersForActions();
        var canAssignToFortyMan = actionTargets.Any(player => player.CanAssignToFortyMan);
        var canAssignToAffiliate = actionTargets.Any(player => player.CanAssignToAffiliate);
        var canRemoveFromFortyMan = actionTargets.Any(player => player.IsOnFortyMan);
        var canRelease = selectedPlayer?.CanRelease == true;
        uiRenderer.DrawButton(_assign40ManButton.Label, GetAssign40ManBounds(), canAssignToFortyMan && !suppressHover && GetAssign40ManBounds().Contains(mousePosition) ? Color.DarkOliveGreen : (canAssignToFortyMan ? Color.OliveDrab : new Color(76, 76, 76)), canAssignToFortyMan ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_assignAffiliateButton.Label, GetAssignAffiliateBounds(), canAssignToAffiliate && !suppressHover && GetAssignAffiliateBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canAssignToAffiliate ? Color.SlateBlue : new Color(76, 76, 76)), canAssignToAffiliate ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_removeFromFortyManButton.Label, GetRemoveFromFortyManBounds(), canRemoveFromFortyMan && !suppressHover && GetRemoveFromFortyManBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canRemoveFromFortyMan ? Color.SlateBlue : new Color(76, 76, 76)), canRemoveFromFortyMan ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_releaseButton.Label, GetReleaseButtonBounds(), canRelease && !suppressHover && GetReleaseButtonBounds().Contains(mousePosition) ? Color.DarkRed : (canRelease ? Color.Firebrick : new Color(76, 76, 76)), canRelease ? Color.White : new Color(188, 188, 188));
    }

    private void DrawFilterDropdown(UiRenderer uiRenderer)
    {
        var options = GetFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var bounds = GetFilterOptionBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = option == _filterMode;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton(GetFilterLabel(option), bounds, color, Color.White);
        }
    }

    private IReadOnlyList<OrganizationRosterPlayerView> GetVisiblePlayers()
    {
        var players = _franchiseSession.GetSelectedTeamOrganizationRoster();
        IEnumerable<OrganizationRosterPlayerView> filtered = _filterMode switch
        {
            RosterFilterMode.FortyMan => players.Where(player => player.IsOnFortyMan),
            RosterFilterMode.Affiliate => players.Where(player => string.Equals(player.TeamStatusLabel, "Affiliate", StringComparison.OrdinalIgnoreCase)),
            RosterFilterMode.Hitters => players.Where(player => !IsPitcher(player.PrimaryPosition)),
            RosterFilterMode.Pitchers => players.Where(player => IsPitcher(player.PrimaryPosition)),
            RosterFilterMode.Catcher => players.Where(player => MatchesRosterPositionFilter(player, "C")),
            RosterFilterMode.FirstBase => players.Where(player => MatchesRosterPositionFilter(player, "1B")),
            RosterFilterMode.SecondBase => players.Where(player => MatchesRosterPositionFilter(player, "2B")),
            RosterFilterMode.ThirdBase => players.Where(player => MatchesRosterPositionFilter(player, "3B")),
            RosterFilterMode.Shortstop => players.Where(player => MatchesRosterPositionFilter(player, "SS")),
            RosterFilterMode.LeftField => players.Where(player => MatchesRosterPositionFilter(player, "LF")),
            RosterFilterMode.CenterField => players.Where(player => MatchesRosterPositionFilter(player, "CF")),
            RosterFilterMode.RightField => players.Where(player => MatchesRosterPositionFilter(player, "RF")),
            RosterFilterMode.DesignatedHitter => players.Where(player => MatchesRosterPositionFilter(player, "DH")),
            RosterFilterMode.StartingPitcher => players.Where(player => MatchesRosterPositionFilter(player, "SP")),
            RosterFilterMode.ReliefPitcher => players.Where(player => MatchesRosterPositionFilter(player, "RP")),
            _ => players
        };

        return _sortMode switch
        {
            RosterSortMode.Name => filtered.OrderBy(player => player.PlayerName).ToList(),
            RosterSortMode.Position => filtered.OrderBy(player => player.PrimaryPosition).ThenBy(player => player.PlayerName).ToList(),
            _ => filtered.OrderByDescending(player => player.AssignmentLabel == "Organization Roster")
                .ThenByDescending(player => player.AssignmentLabel == "Decision Needed")
                .ThenByDescending(player => player.IsOnFortyMan)
                .ThenBy(player => player.PlayerName)
                .ToList()
        };
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

    private void ToggleFilterDropdown()
    {
        _showFilterDropdown = !_showFilterDropdown;
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
        var selectedPlayers = GetSelectedPlayersForActions();
        if (selectedPlayers.Count == 0)
        {
            _statusMessage = "Select at least one player before sending players to the affiliate.";
            return;
        }

        _franchiseSession.AssignSelectedTeamPlayersToAffiliate(selectedPlayers.Select(player => player.PlayerId).ToList(), out var message);
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
        if (_filterMode == RosterFilterMode.FortyMan)
        {
            _filterMode = RosterFilterMode.All;
            _pageIndex = 0;
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

    private static string GetFilterLabel(RosterFilterMode filterMode)
    {
        return filterMode switch
        {
            RosterFilterMode.FortyMan => "40-Man",
            RosterFilterMode.Affiliate => "Affiliate",
            RosterFilterMode.Hitters => "Hitters",
            RosterFilterMode.Pitchers => "Pitchers",
            RosterFilterMode.Catcher => "C",
            RosterFilterMode.FirstBase => "1B",
            RosterFilterMode.SecondBase => "2B",
            RosterFilterMode.ThirdBase => "3B",
            RosterFilterMode.Shortstop => "SS",
            RosterFilterMode.LeftField => "LF",
            RosterFilterMode.CenterField => "CF",
            RosterFilterMode.RightField => "RF",
            RosterFilterMode.DesignatedHitter => "DH",
            RosterFilterMode.StartingPitcher => "SP",
            RosterFilterMode.ReliefPitcher => "RP",
            _ => "All"
        };
    }

    private static IReadOnlyList<RosterFilterMode> GetFilterOptions()
    {
        return
        [
            RosterFilterMode.All,
            RosterFilterMode.FortyMan,
            RosterFilterMode.Affiliate,
            RosterFilterMode.Hitters,
            RosterFilterMode.Pitchers,
            RosterFilterMode.Catcher,
            RosterFilterMode.FirstBase,
            RosterFilterMode.SecondBase,
            RosterFilterMode.ThirdBase,
            RosterFilterMode.Shortstop,
            RosterFilterMode.LeftField,
            RosterFilterMode.CenterField,
            RosterFilterMode.RightField,
            RosterFilterMode.DesignatedHitter,
            RosterFilterMode.StartingPitcher,
            RosterFilterMode.ReliefPitcher
        ];
    }

    private bool TrySelectFilterOption(Point mousePosition)
    {
        var options = GetFilterOptions();
        for (var i = 0; i < options.Count; i++)
        {
            if (!GetFilterOptionBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _filterMode = options[i];
            _pageIndex = 0;
            _showFilterDropdown = false;
            ClearSelection();
            return true;
        }

        _showFilterDropdown = false;
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
        return _showFilterDropdown;
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
            OrganizationRosterCompositionMode.Affiliate => "Affiliate",
            _ => "First Team"
        };
    }

    private IReadOnlyList<OrganizationRosterPlayerView> GetSelectedPlayersForActions()
    {
        var organizationPlayers = _franchiseSession.GetSelectedTeamOrganizationRoster();
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
        var organizationPlayers = _franchiseSession.GetSelectedTeamOrganizationRoster();
        var organizationPlayerIds = organizationPlayers.Select(player => player.PlayerId).ToHashSet();
        _selectedPlayerIds.RemoveWhere(playerId => !organizationPlayerIds.Contains(playerId));

        if (_selectedPlayerId.HasValue && !organizationPlayerIds.Contains(_selectedPlayerId.Value))
        {
            _selectedPlayerId = null;
        }

        if (_filterMode == RosterFilterMode.FortyMan && _selectedPlayerIds.Any(playerId => organizationPlayers.All(player => player.PlayerId != playerId || !player.IsOnFortyMan)))
        {
            _filterMode = RosterFilterMode.All;
            _pageIndex = 0;
        }
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

    private Rectangle GetBackButtonBounds() => new(24, 34, 120, 36);

    private Rectangle GetFilterButtonBounds() => new(48, _viewport.Y - 58, 150, 34);

    private Rectangle GetFilterOptionBounds(int index)
    {
        var buttonBounds = GetFilterButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Y - 4 - ((index + 1) * 30), 150, 26);
    }

    private Rectangle GetSortButtonBounds() => new(210, _viewport.Y - 58, 140, 34);

    private Rectangle GetClearSelectionBounds() => new(362, _viewport.Y - 58, 130, 34);

    private Rectangle GetReleaseButtonBounds() => new(Math.Max(360, _viewport.X - 120), _viewport.Y - 58, 100, 34);

    private Rectangle GetRemoveFromFortyManBounds()
    {
        var releaseBounds = GetReleaseButtonBounds();
        return new Rectangle(releaseBounds.X - 182, releaseBounds.Y, 170, 34);
    }

    private Rectangle GetAssignAffiliateBounds()
    {
        var removeBounds = GetRemoveFromFortyManBounds();
        return new Rectangle(removeBounds.X - 162, removeBounds.Y, 150, 34);
    }

    private Rectangle GetAssign40ManBounds()
    {
        var affiliateBounds = GetAssignAffiliateBounds();
        return new Rectangle(affiliateBounds.X - 152, affiliateBounds.Y, 140, 34);
    }

    private Rectangle GetDetailModeBounds()
    {
        var detailBounds = GetDetailPanelBounds();
        return new Rectangle(detailBounds.X + 12, detailBounds.Y + 32, 116, 28);
    }

    private Rectangle GetCompositionModeBounds()
    {
        var detailModeBounds = GetDetailModeBounds();
        return new Rectangle(detailModeBounds.Right + 10, detailModeBounds.Y, 148, detailModeBounds.Height);
    }

    private Rectangle GetDetailBodyBounds()
    {
        var bounds = GetDetailPanelBounds();
        return new Rectangle(bounds.X + 12, bounds.Y + 68, bounds.Width - 24, bounds.Height - 80);
    }

    private Rectangle GetListPanelBounds() => new(48, 168, Math.Max(560, _viewport.X - 540), Math.Max(360, _viewport.Y - 250));

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
}
