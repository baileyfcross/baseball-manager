using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.FranchiseHub;

public sealed class ScoutingScreen : GameScreen
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
    private readonly Rectangle _backButtonBounds = new(1080, 40, 140, 44);
    private readonly Rectangle _tradeButtonBounds = new(840, 40, 220, 44);
    private MouseState _previousMouseState = default;
    private bool _ignoreClicksUntilRelease = true;
    private int _marketPageIndex;
    private int _offerPageIndex;
    private bool _showFilterDropdown;
    private ScoutingFilterMode _filterMode = ScoutingFilterMode.All;
    private string _selectedCoachRole = "Scouting Director";
    private Guid? _selectedPlayerId;
    private Guid? _selectedOfferPlayerId;
    private string _statusMessage = "Pick a coach and a player to hear a scouting read.";

    public ScoutingScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
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
            else if (_tradeButtonBounds.Contains(mousePosition))
            {
                _tradeButton.Click();
            }
            else if (GetFilterButtonBounds().Contains(mousePosition))
            {
                _filterButton.Click();
            }
            else if (_showFilterDropdown && TrySelectFilterOption(mousePosition))
            {
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
        var coaches = _franchiseSession.GetCoachingStaff();
        var marketPlayers = ApplyFilter(_franchiseSession.GetScoutingBoardPlayers());
        var offerPlayers = _franchiseSession.GetTradeChipPlayers();
        ClampPages(marketPlayers.Count, offerPlayers.Count);
        RefreshSelections(marketPlayers, offerPlayers, coaches);
        _filterButton.Label = _showFilterDropdown
            ? $"Filter: {GetFilterLabel(_filterMode)} ^"
            : $"Filter: {GetFilterLabel(_filterMode)} v";

        uiRenderer.DrawText("Scouting / Transfers", new Vector2(100, 50), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText(_franchiseSession.SelectedTeamName, new Vector2(100, 90), Color.White);
        var filterBounds = GetFilterButtonBounds();
        uiRenderer.DrawButton(_filterButton.Label, filterBounds, filterBounds.Contains(Mouse.GetState().Position) || _showFilterDropdown ? Color.DarkSlateBlue : Color.SlateGray, Color.White);
        if (_showFilterDropdown)
        {
            DrawFilterDropdown(uiRenderer);
        }

        var introY = 120f;
        foreach (var introLine in WrapText("Talk to a coach, get a plain-English read, then swap players if you want to make the move.", 92))
        {
            uiRenderer.DrawText(introLine, new Vector2(100, introY), Color.White, uiRenderer.ScoreboardFont);
            introY += 18f;
        }

        DrawCoaches(uiRenderer, coaches);
        DrawMarketPlayers(uiRenderer, marketPlayers);
        DrawOfferPlayers(uiRenderer, offerPlayers);
        DrawReportArea(uiRenderer, coaches, marketPlayers);

        var mousePosition = Mouse.GetState().Position;
        uiRenderer.DrawButton(_backButton.Label, _backButtonBounds, _backButtonBounds.Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_tradeButton.Label, _tradeButtonBounds, _tradeButtonBounds.Contains(mousePosition) ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
    }

    private void DrawCoaches(UiRenderer uiRenderer, IReadOnlyList<CoachProfileView> coaches)
    {
        uiRenderer.DrawText("COACHES", new Vector2(40, 174), Color.White, uiRenderer.UiMediumFont);

        for (var i = 0; i < coaches.Count; i++)
        {
            var coach = coaches[i];
            var bounds = GetCoachRowBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
            var isSelected = string.Equals(_selectedCoachRole, coach.Role, StringComparison.OrdinalIgnoreCase);
            var color = isSelected ? Color.DarkOliveGreen : (isHovered ? Color.DarkSlateBlue : Color.SlateGray);
            uiRenderer.DrawButton($"{coach.Role}: {Truncate(coach.Name, 14)}", bounds, color, Color.White);
        }
    }

    private void DrawMarketPlayers(UiRenderer uiRenderer, IReadOnlyList<ScoutingPlayerCard> marketPlayers)
    {
        uiRenderer.DrawText("PLAYERS AROUND THE LEAGUE", new Vector2(400, 174), Color.White, uiRenderer.UiMediumFont);
        var visiblePlayers = marketPlayers.Skip(_marketPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetMarketRowBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
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
        uiRenderer.DrawText("PLAYER YOU OFFER", new Vector2(790, 174), Color.White, uiRenderer.UiMediumFont);
        var visiblePlayers = offerPlayers.Skip(_offerPageIndex * PageSize).Take(PageSize).ToList();

        for (var i = 0; i < visiblePlayers.Count; i++)
        {
            var player = visiblePlayers[i];
            var bounds = GetOfferRowBounds(i);
            var isHovered = bounds.Contains(Mouse.GetState().Position);
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

        uiRenderer.DrawText("SCOUT REPORT", new Vector2(40, 520), Color.White, uiRenderer.UiMediumFont);

        if (selectedCoach == null || selectedPlayer == null)
        {
            uiRenderer.DrawText("Select a coach and a player to see a report.", new Vector2(40, 556), Color.White, uiRenderer.ScoreboardFont);
            return;
        }

        var report = _franchiseSession.GetCoachScoutingReport(selectedPlayer.PlayerId, selectedCoach.Role);
        uiRenderer.DrawText($"{report.CoachName} - {report.CoachRole}", new Vector2(40, 556), Color.Goldenrod, uiRenderer.ScoreboardFont);
        uiRenderer.DrawText($"Specialty: {selectedCoach.Specialty} | Voice: {selectedCoach.Voice}", new Vector2(40, 578), Color.White, uiRenderer.ScoreboardFont);

        var reportLines = new List<string>
        {
            BuildLastSeasonSummary(selectedPlayer),
            report.Summary,
            report.Strengths,
            report.Concern,
            report.TransferRecommendation,
            $"Status: {_statusMessage}"
        };

        var y = 600f;
        foreach (var line in reportLines)
        {
            foreach (var wrappedLine in WrapText(line, 100))
            {
                uiRenderer.DrawText(wrappedLine, new Vector2(40, y), Color.White, uiRenderer.ScoreboardFont);
                y += 18f;
            }

            y += 6f;
        }

        var recentMoves = _franchiseSession.GetRecentTransferSummaries(2);
        if (recentMoves.Count > 0)
        {
            uiRenderer.DrawText("Recent Moves:", new Vector2(860, 556), Color.White, uiRenderer.ScoreboardFont);
            var moveY = 578f;
            foreach (var move in recentMoves)
            {
                foreach (var wrappedMove in WrapText(move, 30))
                {
                    uiRenderer.DrawText(wrappedMove, new Vector2(860, moveY), Color.White, uiRenderer.ScoreboardFont);
                    moveY += 18f;
                }
            }
        }
    }

    private void DrawListPaging(UiRenderer uiRenderer, Rectangle previousBounds, Rectangle nextBounds, int currentPage, int totalItems)
    {
        uiRenderer.DrawButton(_marketPreviousButton.Label, previousBounds, previousBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);
        uiRenderer.DrawButton(_marketNextButton.Label, nextBounds, nextBounds.Contains(Mouse.GetState().Position) ? Color.DarkGray : Color.Gray, Color.White);

        var pageLabel = $"Page {currentPage + 1} / {GetMaxPage(totalItems) + 1}";
        var labelX = previousBounds.X;
        var labelY = previousBounds.Bottom + 6;
        uiRenderer.DrawText(pageLabel, new Vector2(labelX, labelY), Color.White, uiRenderer.ScoreboardFont);
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

    private Rectangle GetFilterButtonBounds() => new(620, 40, 200, 44);

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
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).PowerRating;
    }

    private int GetSpeedValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).SpeedRating;
    }

    private int GetPitchingValue(ScoutingPlayerCard player)
    {
        return _franchiseSession.GetPlayerRatings(player.PlayerId, player.PlayerName, player.PrimaryPosition, player.SecondaryPosition, player.Age).PitchingRating;
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

    private static ScoutingFilterMode GetNextFilter(ScoutingFilterMode filterMode)
    {
        return filterMode switch
        {
            ScoutingFilterMode.All => ScoutingFilterMode.Hitters,
            ScoutingFilterMode.Hitters => ScoutingFilterMode.Power,
            ScoutingFilterMode.Power => ScoutingFilterMode.Speed,
            ScoutingFilterMode.Speed => ScoutingFilterMode.Pitching,
            ScoutingFilterMode.Pitching => ScoutingFilterMode.LastOps,
            ScoutingFilterMode.LastOps => ScoutingFilterMode.LastEra,
            _ => ScoutingFilterMode.All
        };
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

    private static Rectangle GetCoachRowBounds(int index) => new(40, 214 + (index * 46), 330, 40);

    private static Rectangle GetMarketRowBounds(int index) => new(400, 214 + (index * 34), 350, 30);

    private static Rectangle GetOfferRowBounds(int index) => new(790, 214 + (index * 34), 410, 30);

    private static Rectangle GetMarketPreviousBounds() => new(400, 488, 90, 36);

    private static Rectangle GetMarketNextBounds() => new(504, 488, 90, 36);

    private static Rectangle GetOfferPreviousBounds() => new(790, 488, 90, 36);

    private static Rectangle GetOfferNextBounds() => new(894, 488, 90, 36);

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
