using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using BaseballManager.Game.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class TransfersScreen : GameScreen
{
    private const int PageSize = 8;
    private const int RosterCap = 40;
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _tradeButton;
    private readonly ButtonControl _filterButton;
    private readonly ButtonControl _marketPreviousButton;
    private readonly ButtonControl _marketNextButton;
    private readonly Rectangle _backButtonBounds = new(24, 34, 120, 36);
    private readonly PlayerContextOverlay _playerContextOverlay = new();
    private MouseState _previousMouseState = default;
    private Point _viewport = new(1280, 720);
    private bool _ignoreClicksUntilRelease = true;
    private int _marketPageIndex;
    private bool _showFilterDropdown;
    private bool _showTransactionMenu;
    private ScoutingFilterMode _filterMode = ScoutingFilterMode.All;
    private string _selectedCoachRole = "Scouting Director";
    private Guid? _selectedPlayerId;
    private Guid? _transactionTargetPlayerId;
    private int _tradeChipPageIndex;
    private readonly HashSet<Guid> _selectedTradeChipIds = new();
    private bool _marketCacheDirty = true;
    private bool _transactionCacheDirty = true;
    private List<ScoutingPlayerCard> _scoutingBoardCache = new();
    private List<ScoutingPlayerCard> _marketPlayersCache = new();
    private List<ScoutingPlayerCard> _tradeChipCache = new();
    private readonly Dictionary<Guid, decimal> _tradeChipAdjustedValueCache = new();
    private readonly Dictionary<Guid, string> _tradeChipNeedLabelCache = new();
    private decimal _transactionAskingPrice;
    private string _transactionOwningTeamName = string.Empty;
    private string _statusMessage = "Browse the transfer market, then open a transaction to buy or build a trade package.";

    public TransfersScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
        _backButton = new ButtonControl
        {
            Label = "Back",
            OnClick = () => _screenManager.TransitionTo(nameof(FranchiseHubScreen))
        };
        _tradeButton = new ButtonControl
        {
            Label = "Open Transaction",
            OnClick = OpenTransactionMenu
        };
        _filterButton = new ButtonControl
        {
            Label = "Filter: All",
            OnClick = () => _showFilterDropdown = !_showFilterDropdown
        };
        _marketPreviousButton = new ButtonControl
        {
            Label = "Prev",
            OnClick = () => _marketPageIndex = Math.Max(0, _marketPageIndex - 1)
        };
        _marketNextButton = new ButtonControl
        {
            Label = "Next",
            OnClick = () => _marketPageIndex++
        };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _showFilterDropdown = false;
        _showTransactionMenu = false;
        _transactionTargetPlayerId = null;
        _selectedTradeChipIds.Clear();
        _playerContextOverlay.Close();
        _tradeChipPageIndex = 0;
        MarkAllCachesDirty();
        RefreshSelections();

        if (_franchiseSession.TryConsumeTransfersFocus(out var focusedPlayerId))
        {
            _selectedPlayerId = focusedPlayerId;
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

            if (_showTransactionMenu)
            {
                TryHandleTransactionClick(mousePosition);
                _previousMouseState = currentMouseState;
                return;
            }

            if (_showFilterDropdown)
            {
                if (GetFilterButtonBounds().Contains(mousePosition))
                {
                    _filterButton.Click();
                }
                else
                {
                    TrySelectFilterOption(mousePosition);
                }

                _previousMouseState = currentMouseState;
                return;
            }

            if (_backButtonBounds.Contains(mousePosition))
            {
                _backButton.Click();
            }
            else if (GetTradeButtonBounds().Contains(mousePosition))
            {
                _tradeButton.Click();
            }
            else if (GetFilterButtonBounds().Contains(mousePosition))
            {
                _filterButton.Click();
            }
            else if (GetMarketPreviousBounds().Contains(mousePosition))
            {
                _marketPreviousButton.Click();
            }
            else if (GetMarketNextBounds().Contains(mousePosition))
            {
                var maxPage = GetMaxPage(GetMarketPlayers().Count);
                if (_marketPageIndex < maxPage)
                {
                    _marketNextButton.Click();
                }
            }
            else if (TrySelectCoach(mousePosition))
            {
            }
            else if (TrySelectMarketPlayer(mousePosition))
            {
            }
        }

        if (isRightPress && !_showFilterDropdown && !_showTransactionMenu)
        {
            if (!TryOpenPlayerContextMenu(currentMouseState.Position))
            {
                _playerContextOverlay.Close();
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        var coaches = _franchiseSession.GetCoachingStaff();
        var marketPlayers = GetMarketPlayers();
        ClampPages(marketPlayers.Count);
        RefreshSelections(marketPlayers, coaches);
        if (_showTransactionMenu)
        {
            EnsureTransactionCache();
        }
        _filterButton.Label = _showFilterDropdown
            ? $"Filter: {GetFilterLabel(_filterMode)} ^"
            : $"Filter: {GetFilterLabel(_filterMode)} v";
        _tradeButton.Label = _showTransactionMenu ? "Close Transaction" : "Open Transaction";
        var suppressHover = IsOverlayCapturingMouse();

        var transferBudget = _franchiseSession.GetTransferBudget();
        var rosterCount = _franchiseSession.GetSelectedTeamRosterCount();
        uiRenderer.DrawText("Transfers", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"{_franchiseSession.SelectedTeamName}  |  Available Budget: {FormatMoney(transferBudget)}  |  Roster: {rosterCount}/{RosterCap}", new Rectangle(168, 82, Math.Max(520, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        var filterBounds = GetFilterButtonBounds();
        uiRenderer.DrawButton(_filterButton.Label, filterBounds, filterBounds.Contains(Mouse.GetState().Position) || _showFilterDropdown ? Color.DarkSlateBlue : Color.SlateGray, Color.White);

        uiRenderer.DrawWrappedTextInBounds(
            "Open a transaction with the owning club to buy or offer a trade package. Buying requires roster space, and package value depends on the other club's positional needs.",
            new Rectangle(168, 112, 620, 40),
            Color.White,
            uiRenderer.UiSmallFont,
            2);

        DrawCoaches(uiRenderer, coaches);
        DrawMarketPlayers(uiRenderer, marketPlayers);
        DrawReportArea(uiRenderer, coaches, marketPlayers);

        var mousePosition = Mouse.GetState().Position;
        var tradeButtonBounds = GetTradeButtonBounds();
        var statusBounds = new Rectangle(40, _viewport.Y - 62, Math.Max(480, _viewport.X - 420), 52);
        uiRenderer.DrawButton(string.Empty, statusBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawWrappedTextInBounds(_statusMessage, new Rectangle(statusBounds.X + 10, statusBounds.Y + 8, statusBounds.Width - 20, statusBounds.Height - 16), Color.White, uiRenderer.UiSmallFont, 2);
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, !suppressHover && _backButtonBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        var tradeButtonColor = _showTransactionMenu ? Color.DarkSlateBlue : Color.OliveDrab;
        uiRenderer.DrawButton(_tradeButton.Label, tradeButtonBounds, !suppressHover && tradeButtonBounds.Contains(mousePosition) ? Color.DarkOliveGreen : tradeButtonColor, Color.White);

        if (_showFilterDropdown)
        {
            DrawFilterDropdown(uiRenderer);
        }

        if (_showTransactionMenu)
        {
            DrawTransactionMenu(uiRenderer);
        }

        _playerContextOverlay.Draw(uiRenderer, mousePosition, _viewport);
    }

    private void DrawCoaches(UiRenderer uiRenderer, IReadOnlyList<CoachProfileView> coaches)
    {
        var suppressHover = IsOverlayCapturingMouse();
        var coachHeaderBounds = new Rectangle(GetCoachRowBounds(0).X, 174, GetCoachRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("COACH REPORTS", coachHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);

        for (var i = 0; i < coaches.Count; i++)
        {
            var coach = coaches[i];
            var bounds = GetCoachRowBounds(i);
            var isHovered = !suppressHover && bounds.Contains(Mouse.GetState().Position);
            var isSelected = string.Equals(_selectedCoachRole, coach.Role, StringComparison.OrdinalIgnoreCase);
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton($"{coach.Role}: {Truncate(coach.Name, 14)}", bounds, color, Color.White);
        }
    }

    private void DrawMarketPlayers(UiRenderer uiRenderer, IReadOnlyList<ScoutingPlayerCard> marketPlayers)
    {
        var suppressHover = IsOverlayCapturingMouse();
        var marketHeaderBounds = new Rectangle(GetMarketRowBounds(0).X, 174, GetMarketRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("TRADE TARGETS", marketHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        var visiblePlayers = marketPlayers.Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetMarketRowBounds(i);
            var isHovered = !suppressHover && bounds.Contains(Mouse.GetState().Position);
            var isSelected = _selectedPlayerId == player.PlayerId;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            var ownTag = player.IsOnSelectedTeam ? " (Yours)" : string.Empty;
            var feeTag = !player.IsOnSelectedTeam ? $"  {FormatMoney(player.TransferFee)}" : string.Empty;
            var label = $"{player.TeamAbbreviation} {Truncate(player.PlayerName, 14)} {player.PrimaryPosition} Age {player.Age}{feeTag}{ownTag}";
            uiRenderer.DrawButton(label, bounds, color, Color.White);
        }

        DrawListPaging(uiRenderer, GetMarketPreviousBounds(), GetMarketNextBounds(), _marketPageIndex, marketPlayers.Count);
    }

    private void DrawReportArea(UiRenderer uiRenderer, IReadOnlyList<CoachProfileView> coaches, IReadOnlyList<ScoutingPlayerCard> marketPlayers)
    {
        var selectedCoach = coaches.FirstOrDefault(coach => string.Equals(coach.Role, _selectedCoachRole, StringComparison.OrdinalIgnoreCase))
            ?? coaches.FirstOrDefault();
        var selectedPlayer = marketPlayers.FirstOrDefault(player => player.PlayerId == _selectedPlayerId)
            ?? marketPlayers.FirstOrDefault();

        var reportPanelBounds = GetReportPanelBounds();
        uiRenderer.DrawTextInBounds("SCOUT REPORT", new Rectangle(reportPanelBounds.X, reportPanelBounds.Y - 26, 300, 22), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawButton(string.Empty, reportPanelBounds, new Color(38, 48, 56), Color.White);

        if (selectedCoach == null || selectedPlayer == null)
        {
            uiRenderer.DrawWrappedTextInBounds("Select a coach and a player to see a scouting report.", new Rectangle(reportPanelBounds.X + 12, reportPanelBounds.Y + 14, reportPanelBounds.Width - 24, reportPanelBounds.Height - 24), Color.White, uiRenderer.ScoreboardFont, 3);
            return;
        }

        var report = _franchiseSession.GetCoachScoutingReport(selectedPlayer.PlayerId, selectedCoach.Role);
        uiRenderer.DrawTextInBounds($"{report.CoachName} - {report.CoachRole}", new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 8, reportPanelBounds.Width - 20, 16), Color.Goldenrod, uiRenderer.UiSmallFont);
        var feeLine = selectedPlayer.IsOnSelectedTeam
            ? "Already on your roster"
            : $"Asking Price: {FormatMoney(selectedPlayer.TransferFee)}";
        uiRenderer.DrawTextInBounds(feeLine, new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 26, reportPanelBounds.Width - 20, 16), selectedPlayer.IsOnSelectedTeam ? Color.Gray : Color.Gold, uiRenderer.UiSmallFont);

        var reportText = string.Join(" ", new[]
        {
            BuildLastSeasonSummary(selectedPlayer),
            report.Summary,
            report.Strengths,
            report.Concern,
            report.TransferRecommendation
        });
        uiRenderer.DrawWrappedTextInBounds(reportText, new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 48, reportPanelBounds.Width - 20, reportPanelBounds.Height - 56), Color.White, uiRenderer.UiSmallFont, 8);
    }

    private void DrawListPaging(UiRenderer uiRenderer, Rectangle previousBounds, Rectangle nextBounds, int currentPage, int totalItems)
    {
        var suppressHover = IsOverlayCapturingMouse();
        uiRenderer.DrawButton(_marketPreviousButton.Label, previousBounds, !suppressHover && previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_marketNextButton.Label, nextBounds, !suppressHover && nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);

        var pageLabel = $"Page {currentPage + 1} / {GetMaxPage(totalItems) + 1}";
        var labelBounds = new Rectangle(previousBounds.Right + 8, previousBounds.Y, Math.Max(84, nextBounds.X - previousBounds.Right - 16), previousBounds.Height);
        uiRenderer.DrawButton(string.Empty, labelBounds, new Color(70, 70, 70), Color.White);
        uiRenderer.DrawTextInBounds(pageLabel, labelBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
    }

    private bool TrySelectCoach(Point mousePosition)
    {
        var coaches = _franchiseSession.GetCoachingStaff();
        for (var i = 0; i < coaches.Count; i++)
        {
            if (!GetCoachRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedCoachRole = coaches[i].Role;
            _statusMessage = $"Talking with {coaches[i].Name}.";
            return true;
        }

        return false;
    }

    private bool TrySelectMarketPlayer(Point mousePosition)
    {
        var visiblePlayers = GetMarketPlayers().Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();
        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            if (!GetMarketRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedPlayerId = visiblePlayers[i].PlayerId;
            _statusMessage = $"{visiblePlayers[i].PlayerName} is on the board. Asking price: {FormatMoney(visiblePlayers[i].TransferFee)}. Open Transaction to negotiate with {visiblePlayers[i].TeamAbbreviation}.";
            return true;
        }

        return false;
    }

    private void OpenTransactionMenu()
    {
        if (_showTransactionMenu)
        {
            _showTransactionMenu = false;
            _selectedTradeChipIds.Clear();
            _transactionTargetPlayerId = null;
            _transactionCacheDirty = true;
            _statusMessage = "Transaction closed. Select another player whenever you are ready.";
            return;
        }

        if (!_selectedPlayerId.HasValue)
        {
            _statusMessage = "Select a player from the market first.";
            return;
        }

        var selectedPlayer = GetScoutingBoardPlayers().FirstOrDefault(player => player.PlayerId == _selectedPlayerId.Value);
        if (selectedPlayer == null)
        {
            _statusMessage = "That player is no longer available on the market.";
            return;
        }

        if (selectedPlayer.IsOnSelectedTeam)
        {
            _statusMessage = "That player is already on your roster.";
            return;
        }

        _transactionTargetPlayerId = selectedPlayer.PlayerId;
        _showTransactionMenu = true;
        _tradeChipPageIndex = 0;
        _selectedTradeChipIds.Clear();
        _transactionCacheDirty = true;
        _statusMessage = $"Transaction opened with {selectedPlayer.TeamName}. Buy outright or build a trade package.";
    }

    private bool TryHandleTransactionClick(Point mousePosition)
    {
        if (!GetTransactionPanelBounds().Contains(mousePosition))
        {
            _showTransactionMenu = false;
            _selectedTradeChipIds.Clear();
            _transactionTargetPlayerId = null;
            _transactionCacheDirty = true;
            _statusMessage = "Transaction closed.";
            return true;
        }

        if (GetTransactionCloseBounds().Contains(mousePosition))
        {
            _showTransactionMenu = false;
            _selectedTradeChipIds.Clear();
            _transactionTargetPlayerId = null;
            _transactionCacheDirty = true;
            _statusMessage = "Transaction closed.";
            return true;
        }

        if (GetTransactionBuyBounds().Contains(mousePosition))
        {
            CompleteTransactionPurchase();
            return true;
        }

        if (GetTransactionTradeBounds().Contains(mousePosition))
        {
            CompleteTradePackage();
            return true;
        }

        if (GetTradeChipPreviousBounds().Contains(mousePosition))
        {
            _tradeChipPageIndex = Math.Max(0, _tradeChipPageIndex - 1);
            return true;
        }

        if (GetTradeChipNextBounds().Contains(mousePosition))
        {
            var chipCount = _tradeChipCache.Count;
            var maxPage = GetMaxPage(chipCount);
            if (_tradeChipPageIndex < maxPage)
            {
                _tradeChipPageIndex++;
            }

            return true;
        }

        var visibleChips = _tradeChipCache.Skip(_tradeChipPageIndex * PageSize).Take(PageSize).ToList();
        for (var i = 0; i < visibleChips.Count; i++)
        {
            if (!GetTradeChipRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            var playerId = visibleChips[i].PlayerId;
            if (!_selectedTradeChipIds.Add(playerId))
            {
                _selectedTradeChipIds.Remove(playerId);
            }

            return true;
        }

        return true;
    }

    private bool IsOverlayCapturingMouse()
    {
        return _showFilterDropdown || _showTransactionMenu || _playerContextOverlay.IsCapturingMouse;
    }

    private bool TryOpenPlayerContextMenu(Point mousePosition)
    {
        var visiblePlayers = GetMarketPlayers().Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();
        var orgRosterById = _franchiseSession.GetSelectedTeamOrganizationRoster().ToDictionary(player => player.PlayerId, player => player);
        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            if (!GetMarketRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            var player = visiblePlayers[i];
            _selectedPlayerId = player.PlayerId;
            orgRosterById.TryGetValue(player.PlayerId, out var rosterPlayer);
            var rosterActions = rosterPlayer == null
                ? new List<PlayerContextActionView>()
                :
                [
                    new(PlayerContextAction.AssignToFortyMan, "Add To 40-Man", rosterPlayer.CanAssignToFortyMan),
                    new(PlayerContextAction.AssignToTripleA, "Send To AAA", rosterPlayer.AffiliateLevel != MinorLeagueAffiliateLevel.TripleA),
                    new(PlayerContextAction.AssignToDoubleA, "Send To AA", rosterPlayer.AffiliateLevel != MinorLeagueAffiliateLevel.DoubleA),
                    new(PlayerContextAction.AssignToSingleA, "Send To A", rosterPlayer.AffiliateLevel != MinorLeagueAffiliateLevel.SingleA),
                    new(PlayerContextAction.RemoveFromFortyMan, "Remove From 40-Man", rosterPlayer.IsOnFortyMan),
                    new(PlayerContextAction.ReleasePlayer, "Release", rosterPlayer.CanRelease)
                ];

            var primaryActions = new List<PlayerContextActionView>
            {
                new(PlayerContextAction.OpenRosterAssignments, "Roster", rosterActions.Any(action => action.IsEnabled)),
                new(PlayerContextAction.OpenProfile, "Profile")
            };

            if (!player.IsOnSelectedTeam)
            {
                primaryActions.Add(new PlayerContextActionView(PlayerContextAction.TradeForPlayer, "Trade For"));
                primaryActions.Add(new PlayerContextActionView(PlayerContextAction.ScoutPlayer, "Scout"));
            }

            _playerContextOverlay.Open(mousePosition, player.PlayerName, primaryActions, rosterActions, _franchiseSession.GetPlayerProfile(player.PlayerId));
            return true;
        }

        return false;
    }

    private void ExecuteContextAction(PlayerContextAction action)
    {
        if (!_selectedPlayerId.HasValue)
        {
            return;
        }

        switch (action)
        {
            case PlayerContextAction.AssignToFortyMan:
                _franchiseSession.AssignSelectedTeamPlayerToFortyMan(_selectedPlayerId.Value, out _statusMessage);
                MarkAllCachesDirty();
                RefreshSelections();
                break;
            case PlayerContextAction.AssignToTripleA:
                _franchiseSession.AssignSelectedTeamPlayerToAffiliate(_selectedPlayerId.Value, MinorLeagueAffiliateLevel.TripleA, out _statusMessage);
                MarkAllCachesDirty();
                RefreshSelections();
                break;
            case PlayerContextAction.AssignToDoubleA:
                _franchiseSession.AssignSelectedTeamPlayerToAffiliate(_selectedPlayerId.Value, MinorLeagueAffiliateLevel.DoubleA, out _statusMessage);
                MarkAllCachesDirty();
                RefreshSelections();
                break;
            case PlayerContextAction.AssignToSingleA:
                _franchiseSession.AssignSelectedTeamPlayerToAffiliate(_selectedPlayerId.Value, MinorLeagueAffiliateLevel.SingleA, out _statusMessage);
                MarkAllCachesDirty();
                RefreshSelections();
                break;
            case PlayerContextAction.RemoveFromFortyMan:
                _franchiseSession.RemoveSelectedTeamPlayerFromFortyMan(_selectedPlayerId.Value, out _statusMessage);
                MarkAllCachesDirty();
                RefreshSelections();
                break;
            case PlayerContextAction.ReleasePlayer:
                _franchiseSession.ReleaseSelectedTeamPlayer(_selectedPlayerId.Value, out _statusMessage);
                MarkAllCachesDirty();
                RefreshSelections();
                break;
            case PlayerContextAction.TradeForPlayer:
                OpenTransactionMenu();
                break;
            case PlayerContextAction.ScoutPlayer:
                var focusedPlayer = GetMarketPlayers().FirstOrDefault(player => player.PlayerId == _selectedPlayerId.Value);
                if (focusedPlayer != null)
                {
                    _statusMessage = $"{focusedPlayer.PlayerName} is highlighted. Review the coach scouting report on the right for the current read.";
                }
                break;
        }
    }

    private void CompleteTransactionPurchase()
    {
        if (!_transactionTargetPlayerId.HasValue)
        {
            _statusMessage = "No transaction target selected.";
            return;
        }

        // If trade chips are selected, use the hybrid buy-with-trades method
        if (_selectedTradeChipIds.Count > 0)
        {
            _franchiseSession.TryBuyPlayerWithTradeChips(_transactionTargetPlayerId.Value, _selectedTradeChipIds.ToList(), out _statusMessage);
        }
        else
        {
            _franchiseSession.TryBuyPlayer(_transactionTargetPlayerId.Value, out _statusMessage);
        }

        MarkAllCachesDirty();
        RefreshSelections();
    }

    private void CompleteTradePackage()
    {
        if (!_transactionTargetPlayerId.HasValue)
        {
            _statusMessage = "No transaction target selected.";
            return;
        }

        if (_selectedTradeChipIds.Count == 0)
        {
            _statusMessage = "Select at least one player from your roster to include in the trade package.";
            return;
        }

        _franchiseSession.TryTradeForPlayerPackage(_transactionTargetPlayerId.Value, _selectedTradeChipIds.ToList(), out _statusMessage);
        MarkAllCachesDirty();
        RefreshSelections();
    }

    private void RefreshSelections(IReadOnlyList<ScoutingPlayerCard>? marketPlayers = null, IReadOnlyList<CoachProfileView>? coaches = null)
    {
        coaches ??= _franchiseSession.GetCoachingStaff();
        marketPlayers ??= GetMarketPlayers();

        if (coaches.Count > 0 && coaches.All(coach => !string.Equals(coach.Role, _selectedCoachRole, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedCoachRole = coaches[0].Role;
        }

        if (!_selectedPlayerId.HasValue || marketPlayers.All(player => player.PlayerId != _selectedPlayerId.Value))
        {
            _selectedPlayerId = marketPlayers.FirstOrDefault()?.PlayerId;
        }

        ClampPages(marketPlayers.Count);
    }

    private IReadOnlyList<ScoutingPlayerCard> GetScoutingBoardPlayers()
    {
        EnsureMarketCache();
        return _scoutingBoardCache;
    }

    private IReadOnlyList<ScoutingPlayerCard> GetMarketPlayers()
    {
        EnsureMarketCache();
        return _marketPlayersCache;
    }

    private void EnsureMarketCache()
    {
        if (!_marketCacheDirty)
        {
            return;
        }

        _scoutingBoardCache = _franchiseSession.GetScoutingBoardPlayers().ToList();
        _marketPlayersCache = ApplyFilter(_scoutingBoardCache);
        _marketCacheDirty = false;
    }

    private void EnsureTransactionCache()
    {
        EnsureMarketCache();
        if (!_transactionCacheDirty)
        {
            return;
        }

        _tradeChipAdjustedValueCache.Clear();
        _tradeChipNeedLabelCache.Clear();
        _tradeChipCache.Clear();

        if (!_transactionTargetPlayerId.HasValue)
        {
            _transactionAskingPrice = 0m;
            _transactionOwningTeamName = string.Empty;
            _transactionCacheDirty = false;
            return;
        }

        var targetPlayer = _scoutingBoardCache.FirstOrDefault(player => player.PlayerId == _transactionTargetPlayerId.Value);
        if (targetPlayer == null)
        {
            _transactionAskingPrice = 0m;
            _transactionOwningTeamName = string.Empty;
            _transactionCacheDirty = false;
            return;
        }

        _transactionOwningTeamName = targetPlayer.TeamName;
        _transactionAskingPrice = _franchiseSession.GetPlayerAskingPrice(targetPlayer.PlayerId);
        _tradeChipCache = _franchiseSession.GetTradeChipPlayers()
            .Where(player => player.PlayerId != targetPlayer.PlayerId)
            .ToList();

        foreach (var chip in _tradeChipCache)
        {
            _tradeChipAdjustedValueCache[chip.PlayerId] = _franchiseSession.GetTradeChipValueForTeam(chip.PlayerId, _transactionOwningTeamName);
            _tradeChipNeedLabelCache[chip.PlayerId] = _franchiseSession.GetTeamPositionNeedLabel(_transactionOwningTeamName, chip.PrimaryPosition);
        }

        _transactionCacheDirty = false;
    }

    private void MarkAllCachesDirty()
    {
        _marketCacheDirty = true;
        _transactionCacheDirty = true;
    }

    private void ClampPages(int marketCount)
    {
        _marketPageIndex = Math.Clamp(_marketPageIndex, 0, GetMaxPage(marketCount));
    }

    private static int GetMaxPage(int totalCount)
    {
        return Math.Max(0, (int)Math.Ceiling(totalCount / (double)PageSize) - 1);
    }

    private Rectangle GetFilterButtonBounds() => new(_viewport.X - 520, 40, 200, 44);

    private Rectangle GetTradeButtonBounds() => new(_viewport.X - 300, 38, 240, 44);

    private void DrawTransactionMenu(UiRenderer uiRenderer)
    {
        var panelBounds = GetTransactionPanelBounds();
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(22, 28, 34), Color.White);
        uiRenderer.DrawTextInBounds("TRANSACTION", new Rectangle(panelBounds.X + 12, panelBounds.Y + 8, 240, 20), Color.Goldenrod, uiRenderer.UiSmallFont);

        var targetPlayerId = _transactionTargetPlayerId ?? _selectedPlayerId;
        if (!targetPlayerId.HasValue)
        {
            uiRenderer.DrawWrappedTextInBounds("Select a market player to start a transaction.", new Rectangle(panelBounds.X + 12, panelBounds.Y + 40, panelBounds.Width - 24, 48), Color.White, uiRenderer.UiSmallFont, 2);
            return;
        }

        var targetPlayer = GetScoutingBoardPlayers().FirstOrDefault(player => player.PlayerId == targetPlayerId.Value);
        if (targetPlayer == null)
        {
            uiRenderer.DrawWrappedTextInBounds("That target is no longer available.", new Rectangle(panelBounds.X + 12, panelBounds.Y + 40, panelBounds.Width - 24, 48), Color.White, uiRenderer.UiSmallFont, 2);
            return;
        }

        _transactionTargetPlayerId = targetPlayer.PlayerId;
        var askingPrice = _transactionAskingPrice;
        var rosterCount = _franchiseSession.GetSelectedTeamRosterCount();
        var hasRosterRoomForBuy = rosterCount < RosterCap;

        var targetSummaryBounds = new Rectangle(panelBounds.X + 12, panelBounds.Y + 36, panelBounds.Width - 24, 54);
        uiRenderer.DrawButton(string.Empty, targetSummaryBounds, new Color(35, 44, 52), Color.White);
        uiRenderer.DrawTextInBounds($"Target: {targetPlayer.PlayerName} ({targetPlayer.PrimaryPosition}, {targetPlayer.Age})", new Rectangle(targetSummaryBounds.X + 8, targetSummaryBounds.Y + 6, targetSummaryBounds.Width - 16, 18), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Owning Team: {targetPlayer.TeamName}   Asking Price: {FormatMoney(askingPrice)}", new Rectangle(targetSummaryBounds.X + 8, targetSummaryBounds.Y + 26, targetSummaryBounds.Width - 16, 18), Color.Gold, uiRenderer.UiSmallFont);

        var hasTradeChipsSelected = _selectedTradeChipIds.Count > 0;
        var canBuyDirectly = hasRosterRoomForBuy;
        var canBuyWithTrade = !hasRosterRoomForBuy && hasTradeChipsSelected;
        var canBuy = canBuyDirectly || canBuyWithTrade;

        var rosterStatusColor = hasRosterRoomForBuy ? Color.White : Color.OrangeRed;
        var rosterStatusText = hasRosterRoomForBuy ? "Buy is available." : $"Buy disabled: roster cap reached.{(hasTradeChipsSelected ? " (Select Buy with trade chips to swap players instead)" : "")}";
        uiRenderer.DrawTextInBounds($"Your Roster: {rosterCount}/{RosterCap}", new Rectangle(panelBounds.X + 12, panelBounds.Y + 98, 180, 18), rosterStatusColor, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds(rosterStatusText, new Rectangle(panelBounds.X + 200, panelBounds.Y + 98, panelBounds.Width - 212, 18), rosterStatusColor, uiRenderer.UiSmallFont);

        var buyButtonBounds = GetTransactionBuyBounds();
        var tradeButtonBounds = GetTransactionTradeBounds();
        var closeButtonBounds = GetTransactionCloseBounds();
        var mousePosition = Mouse.GetState().Position;

        var buyButtonLabel = hasTradeChipsSelected ? "Buy/Trade" : "Buy In Transaction";
        var buyColor = canBuy ? (buyButtonBounds.Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab) : Color.DimGray;
        uiRenderer.DrawButton(buyButtonLabel, buyButtonBounds, buyColor, Color.White);
        uiRenderer.DrawButton("Submit Trade Package", tradeButtonBounds, tradeButtonBounds.Contains(mousePosition) ? Color.DarkSlateBlue : Color.SlateBlue, Color.White);
        uiRenderer.DrawButton("Close", closeButtonBounds, closeButtonBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);

        var chipsHeaderBounds = new Rectangle(panelBounds.X + 12, panelBounds.Y + 154, panelBounds.Width - 24, 24);
        uiRenderer.DrawTextInBounds("YOUR TRADE CHIPS (value adjusts to target team's positional needs)", chipsHeaderBounds, Color.White, uiRenderer.UiSmallFont);

        _tradeChipPageIndex = Math.Clamp(_tradeChipPageIndex, 0, GetMaxPage(_tradeChipCache.Count));
        var visibleChips = _tradeChipCache.Skip(_tradeChipPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visibleChips.Count; i++)
        {
            var chip = visibleChips[i];
            var rowBounds = GetTradeChipRowBounds(i);
            var isSelected = _selectedTradeChipIds.Contains(chip.PlayerId);
            var isHovered = rowBounds.Contains(mousePosition);
            var rowColor = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);

            _tradeChipAdjustedValueCache.TryGetValue(chip.PlayerId, out var adjustedValue);
            _tradeChipNeedLabelCache.TryGetValue(chip.PlayerId, out var needLabel);
            var toggle = isSelected ? "[X]" : "[ ]";
            var label = $"{toggle} {Truncate(chip.PlayerName, 16)} {chip.PrimaryPosition}  {FormatMoney(adjustedValue)}  ({needLabel})";
            uiRenderer.DrawButton(label, rowBounds, rowColor, Color.White);
        }

        DrawListPaging(uiRenderer, GetTradeChipPreviousBounds(), GetTradeChipNextBounds(), _tradeChipPageIndex, _tradeChipCache.Count);

        var offeredValue = _selectedTradeChipIds.Sum(playerId => _tradeChipAdjustedValueCache.TryGetValue(playerId, out var value) ? value : 0m);
        var acceptanceValue = decimal.Round(askingPrice * 0.93m, 0);
        uiRenderer.DrawTextInBounds($"Selected Offer Value: {FormatMoney(offeredValue)}", new Rectangle(panelBounds.X + 12, panelBounds.Bottom - 34, 260, 20), Color.Gold, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Expected Acceptance: {FormatMoney(acceptanceValue)}", new Rectangle(panelBounds.X + 276, panelBounds.Bottom - 34, 260, 20), Color.White, uiRenderer.UiSmallFont);
    }

    private Rectangle GetTransactionPanelBounds()
    {
        var width = Math.Min(980, _viewport.X - 120);
        var height = Math.Min(620, _viewport.Y - 110);
        var x = (_viewport.X - width) / 2;
        var y = Math.Max(90, (_viewport.Y - height) / 2);
        return new Rectangle(x, y, width, height);
    }

    private Rectangle GetTransactionBuyBounds()
    {
        var panel = GetTransactionPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 122, 220, 28);
    }

    private Rectangle GetTransactionTradeBounds()
    {
        var panel = GetTransactionPanelBounds();
        return new Rectangle(panel.X + 244, panel.Y + 122, 240, 28);
    }

    private Rectangle GetTransactionCloseBounds()
    {
        var panel = GetTransactionPanelBounds();
        return new Rectangle(panel.Right - 92, panel.Y + 122, 80, 28);
    }

    private Rectangle GetTradeChipRowBounds(int index)
    {
        var panel = GetTransactionPanelBounds();
        return new Rectangle(panel.X + 12, panel.Y + 182 + (index * 36), panel.Width - 24, 30);
    }

    private Rectangle GetTradeChipPreviousBounds()
    {
        var panel = GetTransactionPanelBounds();
        return new Rectangle(panel.X + 12, panel.Bottom - 68, 84, 26);
    }

    private Rectangle GetTradeChipNextBounds()
    {
        var panel = GetTransactionPanelBounds();
        return new Rectangle(panel.Right - 96, panel.Bottom - 68, 84, 26);
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
            uiRenderer.DrawButton(GetFilterLabel(option), bounds, color, Color.White, uiRenderer.ScoreboardFont);
        }
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
            _marketPageIndex = 0;
            _showFilterDropdown = false;
            _marketCacheDirty = true;
            return true;
        }

        _showFilterDropdown = false;
        return false;
    }

    private Rectangle GetFilterOptionBounds(int index)
    {
        var buttonBounds = GetFilterButtonBounds();
        return new Rectangle(buttonBounds.X, buttonBounds.Bottom + 4 + (index * 32), buttonBounds.Width, 28);
    }

    private static IReadOnlyList<ScoutingFilterMode> GetFilterOptions()
    {
        return
        [
            ScoutingFilterMode.All,
            ScoutingFilterMode.Hitters,
            ScoutingFilterMode.Power,
            ScoutingFilterMode.Speed,
            ScoutingFilterMode.Pitching,
            ScoutingFilterMode.LastOps,
            ScoutingFilterMode.LastEra
        ];
    }

    private List<ScoutingPlayerCard> ApplyFilter(IReadOnlyList<ScoutingPlayerCard> players)
    {
        return _filterMode switch
        {
            ScoutingFilterMode.Hitters => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderBy(player => player.TeamName).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.Power => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderByDescending(GetPowerValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.Speed => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderByDescending(GetSpeedValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.Pitching => players.Where(player => player.PrimaryPosition is "SP" or "RP").OrderByDescending(GetPitchingValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.LastOps => players.Where(player => player.PrimaryPosition is not "SP" and not "RP").OrderByDescending(GetLastOpsValue).ThenBy(player => player.PlayerName).ToList(),
            ScoutingFilterMode.LastEra => players.Where(player => player.PrimaryPosition is "SP" or "RP").OrderBy(GetLastEraValue).ThenBy(player => player.PlayerName).ToList(),
            _ => players.OrderBy(player => player.IsOnSelectedTeam).ThenBy(player => player.TeamName).ThenBy(player => player.PlayerName).ToList()
        };
    }

    private int GetPowerValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).EffectivePowerRating;
    }

    private int GetSpeedValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).EffectiveSpeedRating;
    }

    private int GetPitchingValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).EffectivePitchingRating;
    }

    private double GetLastOpsValue(ScoutingPlayerCard player)
    {
        var stats = _franchiseSession.GetLastSeasonStats(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        var obp = stats.PlateAppearances == 0 ? 0d : (stats.Hits + stats.Walks) / (double)stats.PlateAppearances;
        var singles = Math.Max(0, stats.Hits - stats.Doubles - stats.Triples - stats.HomeRuns);
        var totalBases = singles + (stats.Doubles * 2) + (stats.Triples * 3) + (stats.HomeRuns * 4);
        var slg = stats.AtBats == 0 ? 0d : totalBases / (double)stats.AtBats;
        return obp + slg;
    }

    private double GetLastEraValue(ScoutingPlayerCard player)
    {
        var stats = _franchiseSession.GetLastSeasonStats(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        return stats.InningsPitchedOuts == 0 ? double.MaxValue : (stats.EarnedRuns * 9d) / (stats.InningsPitchedOuts / 3d);
    }

    private string BuildLastSeasonSummary(ScoutingPlayerCard player)
    {
        var stats = _franchiseSession.GetLastSeasonStats(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age);
        return player.PrimaryPosition is "SP" or "RP"
            ? $"Last season: {stats.EarnedRunAverageDisplay} ERA, {stats.WinLossDisplay}, {stats.StrikeoutsPitched} K in {stats.GamesPitched} G."
            : $"Last season: {stats.BattingAverageDisplay} AVG, {stats.HomeRuns} HR, {stats.RunsBattedIn} RBI, {stats.OpsDisplay} OPS in {stats.GamesPlayed} G.";
    }

    private static string GetFilterLabel(ScoutingFilterMode filterMode)
    {
        return filterMode switch
        {
            ScoutingFilterMode.All => "All Targets",
            ScoutingFilterMode.Hitters => "Hitters",
            ScoutingFilterMode.Power => "Power Bats",
            ScoutingFilterMode.Speed => "Speed",
            ScoutingFilterMode.Pitching => "Pitchers",
            ScoutingFilterMode.LastOps => "Last OPS",
            _ => "Last ERA"
        };
    }

    private static string FormatMoney(decimal amount)
    {
        return amount >= 1_000_000m ? $"${amount / 1_000_000m:0.0}M" : $"${amount / 1_000m:0}K";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private Rectangle GetCoachRowBounds(int index)
    {
        var margin = 40;
        var top = 210;
        var gap = 24;
        var availableWidth = Math.Max(960, _viewport.X - (margin * 2) - (gap * 2));
        var coachWidth = Math.Clamp((int)(availableWidth * 0.28f), 260, 360);
        return new Rectangle(margin, top + (index * 46), coachWidth, 38);
    }

    private Rectangle GetMarketRowBounds(int index)
    {
        var coachBounds = GetCoachRowBounds(0);
        var gap = 24;
        var x = coachBounds.Right + gap;
        var reportX = (int)(_viewport.X * 0.62f);
        var marketWidth = Math.Max(280, reportX - x - gap);
        return new Rectangle(x, 210 + (index * 34), marketWidth, 30);
    }

    private Rectangle GetMarketPreviousBounds() => new(GetMarketRowBounds(0).X, Math.Max(490, _viewport.Y - 80), 84, 28);

    private Rectangle GetMarketNextBounds() => new(GetMarketRowBounds(0).Right - 84, Math.Max(490, _viewport.Y - 80), 84, 28);

    private Rectangle GetReportPanelBounds()
    {
        var marketBounds = GetMarketRowBounds(0);
        var x = marketBounds.Right + 24;
        var statusTop = _viewport.Y - 72;
        return new Rectangle(x, 200, Math.Max(280, _viewport.X - x - 40), Math.Max(300, statusTop - 210));
    }

    private enum ScoutingFilterMode
    {
        All,
        Hitters,
        Power,
        Speed,
        Pitching,
        LastOps,
        LastEra
    }
}
