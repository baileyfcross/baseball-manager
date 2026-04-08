using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class TransfersScreen : GameScreen
{
    private const int PageSize = 8;
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private readonly ButtonControl _backButton;
    private readonly ButtonControl _tradeButton;
    private readonly ButtonControl _filterButton;
    private readonly ButtonControl _marketPreviousButton;
    private readonly ButtonControl _marketNextButton;
    private readonly ButtonControl _offerPreviousButton;
    private readonly ButtonControl _offerNextButton;
    private readonly Rectangle _backButtonBounds = new(24, 34, 120, 36);
    private MouseState _previousMouseState = default;
    private Point _viewport = new(1280, 720);
    private bool _ignoreClicksUntilRelease = true;
    private int _marketPageIndex;
    private int _offerPageIndex;
    private bool _showFilterDropdown;
    private ScoutingFilterMode _filterMode = ScoutingFilterMode.All;
    private string _selectedCoachRole = "Scouting Director";
    private Guid? _selectedPlayerId;
    private Guid? _selectedOfferPlayerId;
    private string _statusMessage = "Review the market, talk to your coaches, and line up a swap.";

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
            Label = "Swap Players",
            OnClick = CompleteTrade
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
        _offerPreviousButton = new ButtonControl
        {
            Label = "Prev",
            OnClick = () => _offerPageIndex = Math.Max(0, _offerPageIndex - 1)
        };
        _offerNextButton = new ButtonControl
        {
            Label = "Next",
            OnClick = () => _offerPageIndex++
        };
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
        _showFilterDropdown = false;
        RefreshSelections();
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
            else if (GetTradeButtonBounds().Contains(mousePosition))
            {
                _tradeButton.Click();
            }
            else if (GetFilterButtonBounds().Contains(mousePosition))
            {
                _filterButton.Click();
            }
            else if (_showFilterDropdown)
            {
                TrySelectFilterOption(mousePosition);
            }
            else if (GetMarketPreviousBounds().Contains(mousePosition))
            {
                _marketPreviousButton.Click();
            }
            else if (GetMarketNextBounds().Contains(mousePosition))
            {
                var maxPage = GetMaxPage(ApplyFilter(_franchiseSession.GetScoutingBoardPlayers()).Count);
                if (_marketPageIndex < maxPage)
                {
                    _marketNextButton.Click();
                }
            }
            else if (GetOfferPreviousBounds().Contains(mousePosition))
            {
                _offerPreviousButton.Click();
            }
            else if (GetOfferNextBounds().Contains(mousePosition))
            {
                var maxPage = GetMaxPage(_franchiseSession.GetTradeChipPlayers().Count);
                if (_offerPageIndex < maxPage)
                {
                    _offerNextButton.Click();
                }
            }
            else if (TrySelectCoach(mousePosition))
            {
            }
            else if (TrySelectMarketPlayer(mousePosition))
            {
            }
            else if (TrySelectOfferPlayer(mousePosition))
            {
            }
        }

        _previousMouseState = currentMouseState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        _viewport = new Point(uiRenderer.Viewport.Width, uiRenderer.Viewport.Height);

        var coaches = _franchiseSession.GetCoachingStaff();
        var marketPlayers = ApplyFilter(_franchiseSession.GetScoutingBoardPlayers());
        var offerPlayers = _franchiseSession.GetTradeChipPlayers();
        ClampPages(marketPlayers.Count, offerPlayers.Count);
        RefreshSelections(marketPlayers, offerPlayers, coaches);
        _filterButton.Label = _showFilterDropdown
            ? $"Filter: {GetFilterLabel(_filterMode)} ^"
            : $"Filter: {GetFilterLabel(_filterMode)} v";

        uiRenderer.DrawText("Transfers", new Vector2(168, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds(_franchiseSession.SelectedTeamName, new Rectangle(168, 82, Math.Max(320, _viewport.X - 220), 22), Color.White, uiRenderer.UiSmallFont);
        var filterBounds = GetFilterButtonBounds();
        uiRenderer.DrawButton(_filterButton.Label, filterBounds, filterBounds.Contains(Mouse.GetState().Position) || _showFilterDropdown ? Color.DarkSlateBlue : Color.SlateGray, Color.White);

        uiRenderer.DrawWrappedTextInBounds(
            "Talk to your coaches for plain-English reads on league players, then swap players if you want to make a move.",
            new Rectangle(168, 112, 620, 40),
            Color.White,
            uiRenderer.UiSmallFont,
            2);

        DrawCoaches(uiRenderer, coaches);
        DrawMarketPlayers(uiRenderer, marketPlayers);
        DrawOfferPlayers(uiRenderer, offerPlayers);
        DrawReportArea(uiRenderer, coaches, marketPlayers);

        var mousePosition = Mouse.GetState().Position;
        var tradeButtonBounds = GetTradeButtonBounds();
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, !_showFilterDropdown && _backButtonBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_tradeButton.Label, tradeButtonBounds, !_showFilterDropdown && tradeButtonBounds.Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);

        if (_showFilterDropdown)
        {
            DrawFilterDropdown(uiRenderer);
        }
    }

    private void DrawCoaches(UiRenderer uiRenderer, IReadOnlyList<CoachProfileView> coaches)
    {
        var coachHeaderBounds = new Rectangle(GetCoachRowBounds(0).X, 174, GetCoachRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("COACH REPORTS", coachHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);

        for (var i = 0; i < coaches.Count; i++)
        {
            var coach = coaches[i];
            var bounds = GetCoachRowBounds(i);
            var isHovered = !_showFilterDropdown && bounds.Contains(Mouse.GetState().Position);
            var isSelected = string.Equals(_selectedCoachRole, coach.Role, StringComparison.OrdinalIgnoreCase);
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton($"{coach.Role}: {Truncate(coach.Name, 14)}", bounds, color, Color.White);
        }
    }

    private void DrawMarketPlayers(UiRenderer uiRenderer, IReadOnlyList<ScoutingPlayerCard> marketPlayers)
    {
        var marketHeaderBounds = new Rectangle(GetMarketRowBounds(0).X, 174, GetMarketRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("TRADE TARGETS", marketHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        var visiblePlayers = marketPlayers.Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetMarketRowBounds(i);
            var isHovered = !_showFilterDropdown && bounds.Contains(Mouse.GetState().Position);
            var isSelected = _selectedPlayerId == player.PlayerId;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            var ownTag = player.IsOnSelectedTeam ? " (Yours)" : string.Empty;
            var label = $"{player.TeamAbbreviation} {Truncate(player.PlayerName, 15)} {player.PrimaryPosition} Age {player.Age}{ownTag}";
            uiRenderer.DrawButton(label, bounds, color, Color.White);
        }

        DrawListPaging(uiRenderer, GetMarketPreviousBounds(), GetMarketNextBounds(), _marketPageIndex, marketPlayers.Count);
    }

    private void DrawOfferPlayers(UiRenderer uiRenderer, IReadOnlyList<ScoutingPlayerCard> offerPlayers)
    {
        var offerHeaderBounds = new Rectangle(GetOfferRowBounds(0).X, 174, GetOfferRowBounds(0).Width, 24);
        uiRenderer.DrawTextInBounds("YOUR TRADE CHIP", offerHeaderBounds, Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        var visiblePlayers = offerPlayers.Skip(_offerPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetOfferRowBounds(i);
            var isHovered = !_showFilterDropdown && bounds.Contains(Mouse.GetState().Position);
            var isSelected = _selectedOfferPlayerId == player.PlayerId;
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            var label = $"{Truncate(player.PlayerName, 18)} {player.PrimaryPosition}/{player.SecondaryPosition} Age {player.Age}";
            uiRenderer.DrawButton(label, bounds, color, Color.White);
        }

        DrawListPaging(uiRenderer, GetOfferPreviousBounds(), GetOfferNextBounds(), _offerPageIndex, offerPlayers.Count);
    }

    private void DrawReportArea(UiRenderer uiRenderer, IReadOnlyList<CoachProfileView> coaches, IReadOnlyList<ScoutingPlayerCard> marketPlayers)
    {
        var selectedCoach = coaches.FirstOrDefault(coach => string.Equals(coach.Role, _selectedCoachRole, StringComparison.OrdinalIgnoreCase))
            ?? coaches.FirstOrDefault();
        var selectedPlayer = marketPlayers.FirstOrDefault(player => player.PlayerId == _selectedPlayerId)
            ?? marketPlayers.FirstOrDefault();

        var reportPanelBounds = GetReportPanelBounds();
        var movesPanelBounds = GetMovesPanelBounds(reportPanelBounds);

        uiRenderer.DrawTextInBounds("TRADE REPORT", new Rectangle(reportPanelBounds.X, reportPanelBounds.Y - 30, 300, 24), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawButton(string.Empty, reportPanelBounds, new Color(38, 48, 56), Color.White);
        uiRenderer.DrawButton(string.Empty, movesPanelBounds, new Color(38, 48, 56), Color.White);

        if (selectedCoach == null || selectedPlayer == null)
        {
            uiRenderer.DrawWrappedTextInBounds("Select a coach and a player to see a report.", new Rectangle(reportPanelBounds.X + 12, reportPanelBounds.Y + 12, reportPanelBounds.Width - 24, reportPanelBounds.Height - 24), Color.White, uiRenderer.ScoreboardFont, 2);
            return;
        }

        var report = _franchiseSession.GetCoachScoutingReport(selectedPlayer.PlayerId, selectedCoach.Role);
        uiRenderer.DrawTextInBounds($"{report.CoachName} - {report.CoachRole}", new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 8, reportPanelBounds.Width - 20, 16), Color.Goldenrod, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Specialty: {selectedCoach.Specialty} | Voice: {selectedCoach.Voice}", new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 24, reportPanelBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);

        var reportText = string.Join(" ", new[]
        {
            BuildLastSeasonSummary(selectedPlayer),
            report.Summary,
            report.Strengths,
            report.Concern,
            report.TransferRecommendation,
            $"Status: {_statusMessage}"
        });

        uiRenderer.DrawWrappedTextInBounds(reportText, new Rectangle(reportPanelBounds.X + 10, reportPanelBounds.Y + 44, reportPanelBounds.Width - 20, reportPanelBounds.Height - 52), Color.White, uiRenderer.UiSmallFont, 5);

        uiRenderer.DrawTextInBounds("Recent Moves", new Rectangle(movesPanelBounds.X + 10, movesPanelBounds.Y + 8, movesPanelBounds.Width - 20, 16), Color.White, uiRenderer.UiSmallFont);
        var recentMoves = _franchiseSession.GetRecentTransferSummaries(2);
        var movesText = recentMoves.Count == 0
            ? "No recent swaps yet."
            : string.Join(" ", recentMoves);
        uiRenderer.DrawWrappedTextInBounds(movesText, new Rectangle(movesPanelBounds.X + 10, movesPanelBounds.Y + 28, movesPanelBounds.Width - 20, movesPanelBounds.Height - 36), Color.White, uiRenderer.UiSmallFont, 5);
    }

    private void DrawListPaging(UiRenderer uiRenderer, Rectangle previousBounds, Rectangle nextBounds, int currentPage, int totalItems)
    {
        uiRenderer.DrawButton(_marketPreviousButton.Label, previousBounds, !_showFilterDropdown && previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_marketNextButton.Label, nextBounds, !_showFilterDropdown && nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);

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
        var visiblePlayers = ApplyFilter(_franchiseSession.GetScoutingBoardPlayers()).Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();
        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            if (!GetMarketRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedPlayerId = visiblePlayers[i].PlayerId;
            _statusMessage = $"{visiblePlayers[i].PlayerName} is now on the board for discussion.";
            return true;
        }

        return false;
    }

    private bool TrySelectOfferPlayer(Point mousePosition)
    {
        var visiblePlayers = _franchiseSession.GetTradeChipPlayers().Skip(_offerPageIndex * PageSize).Take(PageSize).ToList();
        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            if (!GetOfferRowBounds(i).Contains(mousePosition))
            {
                continue;
            }

            _selectedOfferPlayerId = visiblePlayers[i].PlayerId;
            _statusMessage = $"You are offering {visiblePlayers[i].PlayerName} in the swap.";
            return true;
        }

        return false;
    }

    private void CompleteTrade()
    {
        if (!_selectedPlayerId.HasValue || !_selectedOfferPlayerId.HasValue)
        {
            _statusMessage = "Pick both a target and an outgoing player first.";
            return;
        }

        _franchiseSession.TryTradeForPlayer(_selectedPlayerId.Value, _selectedOfferPlayerId.Value, out _statusMessage);
        RefreshSelections();
    }

    private void RefreshSelections(IReadOnlyList<ScoutingPlayerCard>? marketPlayers = null, IReadOnlyList<ScoutingPlayerCard>? offerPlayers = null, IReadOnlyList<CoachProfileView>? coaches = null)
    {
        coaches ??= _franchiseSession.GetCoachingStaff();
        marketPlayers ??= ApplyFilter(_franchiseSession.GetScoutingBoardPlayers());
        offerPlayers ??= _franchiseSession.GetTradeChipPlayers();

        if (coaches.Count > 0 && coaches.All(coach => !string.Equals(coach.Role, _selectedCoachRole, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedCoachRole = coaches[0].Role;
        }

        if (!_selectedPlayerId.HasValue || marketPlayers.All(player => player.PlayerId != _selectedPlayerId.Value))
        {
            _selectedPlayerId = marketPlayers.FirstOrDefault()?.PlayerId;
        }

        if (!_selectedOfferPlayerId.HasValue || offerPlayers.All(player => player.PlayerId != _selectedOfferPlayerId.Value))
        {
            _selectedOfferPlayerId = offerPlayers.FirstOrDefault()?.PlayerId;
        }

        ClampPages(marketPlayers.Count, offerPlayers.Count);
    }

    private void ClampPages(int marketCount, int offerCount)
    {
        _marketPageIndex = Math.Clamp(_marketPageIndex, 0, GetMaxPage(marketCount));
        _offerPageIndex = Math.Clamp(_offerPageIndex, 0, GetMaxPage(offerCount));
    }

    private static int GetMaxPage(int totalCount)
    {
        return Math.Max(0, (int)Math.Ceiling(totalCount / (double)PageSize) - 1);
    }

    private Rectangle GetFilterButtonBounds() => new(_viewport.X - 520, 40, 200, 44);

    private Rectangle GetTradeButtonBounds() => new(_viewport.X - 300, 38, 240, 44);

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
        var availableWidth = Math.Max(960, _viewport.X - 80 - (gap * 2));
        var marketWidth = Math.Clamp((int)(availableWidth * 0.32f), 280, 380);
        return new Rectangle(x, 210 + (index * 34), marketWidth, 30);
    }

    private Rectangle GetOfferRowBounds(int index)
    {
        var marketBounds = GetMarketRowBounds(0);
        var gap = 24;
        var x = marketBounds.Right + gap;
        return new Rectangle(x, 210 + (index * 34), Math.Max(300, _viewport.X - x - 40), 30);
    }

    private Rectangle GetMarketPreviousBounds() => new(GetMarketRowBounds(0).X, 490, 84, 34);

    private Rectangle GetMarketNextBounds() => new(GetMarketRowBounds(0).Right - 84, 490, 84, 34);

    private Rectangle GetOfferPreviousBounds() => new(GetOfferRowBounds(0).X, 490, 84, 34);

    private Rectangle GetOfferNextBounds() => new(GetOfferRowBounds(0).Right - 84, 490, 84, 34);

    private Rectangle GetReportPanelBounds()
    {
        var bottomY = Math.Max(548, _viewport.Y - 160);
        var width = Math.Max(520, (int)(_viewport.X * 0.62f));
        return new Rectangle(40, bottomY, Math.Min(width, _viewport.X - 420), 140);
    }

    private Rectangle GetMovesPanelBounds(Rectangle reportPanelBounds)
    {
        var x = reportPanelBounds.Right + 30;
        return new Rectangle(x, reportPanelBounds.Y, Math.Max(250, _viewport.X - x - 40), reportPanelBounds.Height);
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
