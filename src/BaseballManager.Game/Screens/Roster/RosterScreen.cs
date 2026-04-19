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
    private enum RosterFilterMode
    {
        All,
        FortyMan,
        Affiliate,
        Hitters,
        Pitchers
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
    private readonly ButtonControl _assign40ManButton;
    private readonly ButtonControl _assignAffiliateButton;
    private readonly ButtonControl _removeFromFortyManButton;
    private readonly ButtonControl _releaseButton;
    private readonly ButtonControl _previousPageButton;
    private readonly ButtonControl _nextPageButton;
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private Point _viewport = new(1280, 720);
    private Guid? _selectedPlayerId;
    private int _pageIndex;
    private RosterFilterMode _filterMode = RosterFilterMode.All;
    private RosterSortMode _sortMode = RosterSortMode.Status;
    private string _statusMessage = "Review player stats, add or remove players from the 40-man roster, keep them in the organization, move them to the affiliate, or release them outright from this screen.";

    public RosterScreen(ScreenManager screenManager, ImportedLeagueData leagueData, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl { Label = "Back", OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen)) };
        _filterButton = new ButtonControl { Label = "Filter: All", OnClick = CycleFilter };
        _sortButton = new ButtonControl { Label = "Sort: Status", OnClick = CycleSort };
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
        _filterMode = RosterFilterMode.All;
        _sortMode = RosterSortMode.Status;
        _selectedPlayerId = null;
        _statusMessage = "Review player stats, add or remove players from the 40-man roster, keep them in the organization, move them to the affiliate, or release them outright from this screen.";
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
            else if (GetSortButtonBounds().Contains(mousePosition))
            {
                _sortButton.Click();
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

        _filterButton.Label = $"Filter: {GetFilterLabel(_filterMode)}";
        _sortButton.Label = $"Sort: {GetSortLabel(_sortMode)}";

        uiRenderer.DrawText("Roster", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, 360, 22), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(48, 112, Math.Max(640, _viewport.X - 96), 42), Color.White, uiRenderer.UiSmallFont, 2);

        var listBounds = GetListPanelBounds();
        var detailBounds = GetDetailPanelBounds();
        uiRenderer.DrawButton(string.Empty, listBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, detailBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawTextInBounds("Organization Roster", new Rectangle(listBounds.X + 12, listBounds.Y + 8, listBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds("Player Details", new Rectangle(detailBounds.X + 12, detailBounds.Y + 8, detailBounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);

        if (players.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds("No roster players match the current filter.", new Rectangle(listBounds.X + 12, listBounds.Y + 40, listBounds.Width - 24, 60), Color.White, uiRenderer.UiSmallFont, 3);
            uiRenderer.DrawWrappedTextInBounds("Change the filter or return after adding players to the organization.", new Rectangle(detailBounds.X + 12, detailBounds.Y + 40, detailBounds.Width - 24, 60), Color.White, uiRenderer.UiSmallFont, 3);
        }
        else
        {
            DrawPlayerList(uiRenderer, players, mousePosition);
            if (selectedPlayer != null)
            {
                DrawPlayerDetail(uiRenderer, selectedPlayer, contractsByPlayerId);
            }
        }

        DrawButtons(uiRenderer, mousePosition, selectedPlayer);
    }

    private void DrawPlayerList(UiRenderer uiRenderer, IReadOnlyList<OrganizationRosterPlayerView> players, Point mousePosition)
    {
        var pageSize = GetPageSize();
        var maxPage = Math.Max(0, (int)Math.Ceiling(players.Count / (double)pageSize) - 1);
        _pageIndex = Math.Clamp(_pageIndex, 0, maxPage);
        var startIndex = _pageIndex * pageSize;
        var visiblePlayers = players.Skip(startIndex).Take(pageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetPlayerRowBounds(i);
            var isSelected = player.PlayerId == _selectedPlayerId;
            var background = isSelected ? Color.DarkOliveGreen : (bounds.Contains(mousePosition) ? Color.DimGray : new Color(54, 62, 70));
            uiRenderer.DrawButton(string.Empty, bounds, background, Color.White);
            uiRenderer.DrawTextInBounds($"{player.PlayerName} | {player.PrimaryPosition}/{player.SecondaryPosition}".TrimEnd('/'), new Rectangle(bounds.X + 8, bounds.Y + 4, bounds.Width - 216, 16), Color.White, uiRenderer.UiSmallFont);
            uiRenderer.DrawTextInBounds(player.AssignmentLabel, new Rectangle(bounds.Right - 208, bounds.Y + 4, 200, 16), player.IsOnFortyMan ? Color.Gold : Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
            uiRenderer.DrawTextInBounds(GetPlayerListSummary(player), new Rectangle(bounds.X + 8, bounds.Y + 22, bounds.Width - 16, 14), Color.White, uiRenderer.UiSmallFont);
        }

        uiRenderer.DrawButton(_previousPageButton.Label, GetPreviousPageBounds(), _pageIndex > 0 && GetPreviousPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(_nextPageButton.Label, GetNextPageBounds(), _pageIndex < maxPage && GetNextPageBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
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

        var bounds = GetDetailPanelBounds();
        var content = new Rectangle(bounds.X + 12, bounds.Y + 36, bounds.Width - 24, bounds.Height - 48);
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

    private void DrawButtons(UiRenderer uiRenderer, Point mousePosition, OrganizationRosterPlayerView? selectedPlayer)
    {
        uiRenderer.DrawButton(_backButton.Label, GetBackButtonBounds(), GetBackButtonBounds().Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_filterButton.Label, GetFilterButtonBounds(), GetFilterButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton(_sortButton.Label, GetSortButtonBounds(), GetSortButtonBounds().Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);

        var canAssignToFortyMan = selectedPlayer?.CanAssignToFortyMan == true;
        var canAssignToAffiliate = selectedPlayer?.CanAssignToAffiliate == true;
        var canRemoveFromFortyMan = selectedPlayer?.IsOnFortyMan == true;
        var canRelease = selectedPlayer?.CanRelease == true;
        uiRenderer.DrawButton(_assign40ManButton.Label, GetAssign40ManBounds(), canAssignToFortyMan && GetAssign40ManBounds().Contains(mousePosition) ? Color.DarkOliveGreen : (canAssignToFortyMan ? Color.OliveDrab : new Color(76, 76, 76)), canAssignToFortyMan ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_assignAffiliateButton.Label, GetAssignAffiliateBounds(), canAssignToAffiliate && GetAssignAffiliateBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canAssignToAffiliate ? Color.SlateBlue : new Color(76, 76, 76)), canAssignToAffiliate ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_removeFromFortyManButton.Label, GetRemoveFromFortyManBounds(), canRemoveFromFortyMan && GetRemoveFromFortyManBounds().Contains(mousePosition) ? Color.DarkSlateBlue : (canRemoveFromFortyMan ? Color.SlateBlue : new Color(76, 76, 76)), canRemoveFromFortyMan ? Color.White : new Color(188, 188, 188));
        uiRenderer.DrawButton(_releaseButton.Label, GetReleaseButtonBounds(), canRelease && GetReleaseButtonBounds().Contains(mousePosition) ? Color.DarkRed : (canRelease ? Color.Firebrick : new Color(76, 76, 76)), canRelease ? Color.White : new Color(188, 188, 188));
    }

    private IReadOnlyList<OrganizationRosterPlayerView> GetVisiblePlayers()
    {
        var players = _franchiseSession.GetSelectedTeamOrganizationRoster();
        IEnumerable<OrganizationRosterPlayerView> filtered = _filterMode switch
        {
            RosterFilterMode.FortyMan => players.Where(player => player.IsOnFortyMan),
            RosterFilterMode.Affiliate => players.Where(player => !player.IsOnFortyMan),
            RosterFilterMode.Hitters => players.Where(player => !IsPitcher(player.PrimaryPosition)),
            RosterFilterMode.Pitchers => players.Where(player => IsPitcher(player.PrimaryPosition)),
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
            _selectedPlayerId = players[0].PlayerId;
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

            _selectedPlayerId = players[startIndex + i].PlayerId;
            return;
        }
    }

    private void CycleFilter()
    {
        _filterMode = _filterMode switch
        {
            RosterFilterMode.All => RosterFilterMode.FortyMan,
            RosterFilterMode.FortyMan => RosterFilterMode.Affiliate,
            RosterFilterMode.Affiliate => RosterFilterMode.Hitters,
            RosterFilterMode.Hitters => RosterFilterMode.Pitchers,
            _ => RosterFilterMode.All
        };
        _pageIndex = 0;
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
    }

    private void AssignToFortyMan()
    {
        var selectedPlayer = GetSelectedPlayer(GetVisiblePlayers());
        if (selectedPlayer == null)
        {
            _statusMessage = "Select a player before managing the 40-man roster.";
            return;
        }

        _franchiseSession.AssignSelectedTeamPlayerToFortyMan(selectedPlayer.PlayerId, out var message);
        _statusMessage = message;
    }

    private void AssignToAffiliate()
    {
        var selectedPlayer = GetSelectedPlayer(GetVisiblePlayers());
        if (selectedPlayer == null)
        {
            _statusMessage = "Select a player before sending someone to the affiliate.";
            return;
        }

        _franchiseSession.AssignSelectedTeamPlayerToAffiliate(selectedPlayer.PlayerId, out var message);
        _statusMessage = message;
        EnsurePlayerRemainsVisibleAfterAffiliateMove(selectedPlayer.PlayerId);
    }

    private void RemoveFromFortyMan()
    {
        var selectedPlayer = GetSelectedPlayer(GetVisiblePlayers());
        if (selectedPlayer == null)
        {
            _statusMessage = "Select a player before removing someone from the 40-man roster.";
            return;
        }

        _franchiseSession.RemoveSelectedTeamPlayerFromFortyMan(selectedPlayer.PlayerId, out var message);
        _statusMessage = message;
        EnsurePlayerRemainsVisibleAfterAffiliateMove(selectedPlayer.PlayerId);
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
                : player.IsOnFortyMan ? "Bench / Bullpen" : "Affiliate";
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
            _ => "All"
        };
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

    private Rectangle GetSortButtonBounds() => new(210, _viewport.Y - 58, 140, 34);

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
