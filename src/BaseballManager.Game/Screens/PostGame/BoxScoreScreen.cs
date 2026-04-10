using BaseballManager.Game.Screens;

using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using BaseballManager.Game.Input;
using BaseballManager.Game.Screens.FranchiseHub;
using BaseballManager.Game.Screens.LiveMatch;
using BaseballManager.Game.Screens.MainMenu;
using BaseballManager.Sim.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BaseballManager.Game.Screens.PostGame;

public sealed class BoxScoreScreen : GameScreen
{
    private readonly ScreenManager _screenManager;
    private readonly FranchiseSession _franchiseSession;
    private MouseState _previousMouseState = default;
    private KeyboardState _previousKeyboardState = default;
    private bool _ignoreClicksUntilRelease = true;

    private readonly Rectangle _backButtonBounds = new(160, 592, 220, 44);
    private readonly Rectangle _continueButtonBounds = new(392, 592, 240, 44);

    public BoxScoreScreen(ScreenManager screenManager, FranchiseSession franchiseSession)
    {
        _screenManager = screenManager;
        _franchiseSession = franchiseSession;
    }

    public override void OnEnter()
    {
        _ignoreClicksUntilRelease = true;
    }

    public override void Update(GameTime gameTime, InputManager inputManager)
    {
        var currentMouseState = inputManager.MouseState;
        var currentKeyboardState = inputManager.KeyboardState;

        if (_ignoreClicksUntilRelease)
        {
            if (currentMouseState.LeftButton == ButtonState.Released)
            {
                _ignoreClicksUntilRelease = false;
            }
        }
        else if (_previousMouseState.LeftButton == ButtonState.Released && currentMouseState.LeftButton == ButtonState.Pressed)
        {
            var mousePosition = currentMouseState.Position;
            if (_backButtonBounds.Contains(mousePosition))
            {
                ReturnToDestination();
                return;
            }

            if (_continueButtonBounds.Contains(mousePosition))
            {
                ContinueFlow();
                return;
            }
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Escape))
        {
            ReturnToDestination();
            return;
        }

        if (IsNewKeyPress(currentKeyboardState, Keys.Enter))
        {
            ContinueFlow();
            return;
        }

        _previousMouseState = currentMouseState;
        _previousKeyboardState = currentKeyboardState;
    }

    public override void Draw(GameTime gameTime, UiRenderer uiRenderer)
    {
        var isLiveSnapshot = _franchiseSession.ShouldUseLiveBoxScore();
        var liveMatch = isLiveSnapshot ? _franchiseSession.GetLiveMatchState() : null;
        var boxScore = isLiveSnapshot ? null : _franchiseSession.GetCurrentCompletedGameBoxScore();
        var summary = isLiveSnapshot && liveMatch != null
            ? BuildLiveSummary(liveMatch)
            : boxScore?.Summary ?? _franchiseSession.GetLastCompletedLiveMatchSummary();
        var viewport = uiRenderer.Viewport;
        var panelBounds = new Rectangle(140, 110, Math.Max(920, viewport.Width - 280), Math.Max(520, viewport.Height - 220));
        var isBackHovered = _backButtonBounds.Contains(Mouse.GetState().Position);
        var isContinueHovered = _continueButtonBounds.Contains(Mouse.GetState().Position);
        var backLabel = isLiveSnapshot
            ? "Back To Game"
            : summary?.WasFranchiseMatch == true ? "Return To Hub" : "Main Menu";
        var continueLabel = isLiveSnapshot ? "Resume Live Game" : "Post Game";

        uiRenderer.DrawText("Box Score", new Vector2(160, 42), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(22, 34, 42), Color.Transparent);

        if (summary == null)
        {
            uiRenderer.DrawWrappedTextInBounds("No box score data is available for this game.", new Rectangle(160, 156, panelBounds.Width - 40, 50), Color.White, uiRenderer.UiSmallFont, 2);
        }
        else
        {
            var gameDate = summary.ScheduledDate == default
                ? summary.CompletedAtUtc.ToLocalTime().Date
                : summary.ScheduledDate;
            var venueLabel = string.IsNullOrWhiteSpace(summary.Venue) ? "Venue TBD" : summary.Venue;
            var headerPrefix = isLiveSnapshot ? "Live" : "Final";

            uiRenderer.DrawTextInBounds($"{headerPrefix}: {summary.AwayAbbreviation} {summary.AwayRuns} - {summary.HomeAbbreviation} {summary.HomeRuns}", new Rectangle(160, 140, panelBounds.Width - 40, 28), Color.Gold, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds($"{gameDate:dddd, MMMM d yyyy} • {venueLabel}", new Rectangle(160, 170, panelBounds.Width - 40, 20), Color.White, uiRenderer.UiSmallFont);

            DrawClassicLineScore(
                uiRenderer,
                new Rectangle(160, 204, panelBounds.Width - 40, 112),
                summary,
                isLiveSnapshot ? liveMatch?.AwayRunsByInning : boxScore?.AwayRunsByInning,
                isLiveSnapshot ? liveMatch?.HomeRunsByInning : boxScore?.HomeRunsByInning,
                isLiveSnapshot ? liveMatch?.AwayErrors ?? 0 : boxScore?.AwayErrors ?? 0,
                isLiveSnapshot ? liveMatch?.HomeErrors ?? 0 : boxScore?.HomeErrors ?? 0);
            DrawPitchingPanel(uiRenderer, new Rectangle(160, 328, panelBounds.Width - 40, 118), boxScore, liveMatch, isLiveSnapshot);
            DrawHighlightsPanel(uiRenderer, new Rectangle(160, 458, panelBounds.Width - 40, 118), boxScore, summary, liveMatch, isLiveSnapshot);
        }

        uiRenderer.DrawButton(backLabel, _backButtonBounds, isBackHovered ? Color.DarkSlateGray : Color.SlateGray, Color.White);
        uiRenderer.DrawButton(continueLabel, _continueButtonBounds, isContinueHovered ? Color.DarkOliveGreen : Color.OliveDrab, Color.White);
    }

    private CompletedLiveMatchSummaryState BuildLiveSummary(LiveMatchSaveState liveMatch)
    {
        var scheduledGame = _franchiseSession.PendingLiveMatchMode == LiveMatchMode.Franchise
            ? _franchiseSession.GetNextScheduledGame()
            : null;
        var selectedTeamName = _franchiseSession.SelectedTeam?.Name ?? string.Empty;
        var isFranchiseMatch = _franchiseSession.PendingLiveMatchMode == LiveMatchMode.Franchise;

        return new CompletedLiveMatchSummaryState
        {
            AwayTeamName = liveMatch.AwayTeam.Name,
            HomeTeamName = liveMatch.HomeTeam.Name,
            AwayAbbreviation = liveMatch.AwayTeam.Abbreviation,
            HomeAbbreviation = liveMatch.HomeTeam.Abbreviation,
            AwayRuns = liveMatch.AwayTeam.Runs,
            HomeRuns = liveMatch.HomeTeam.Runs,
            AwayHits = liveMatch.AwayTeam.Hits,
            HomeHits = liveMatch.HomeTeam.Hits,
            AwayPitchCount = liveMatch.AwayTeam.PitchCount,
            HomePitchCount = liveMatch.HomeTeam.PitchCount,
            AwayStartingPitcherName = liveMatch.AwayTeam.StartingPitcher?.FullName ?? liveMatch.AwayTeam.CurrentPitcher?.FullName ?? "TBD",
            HomeStartingPitcherName = liveMatch.HomeTeam.StartingPitcher?.FullName ?? liveMatch.HomeTeam.CurrentPitcher?.FullName ?? "TBD",
            ScheduledDate = scheduledGame?.Date.Date ?? liveMatch.SavedAtUtc.ToLocalTime().Date,
            GameNumber = scheduledGame?.GameNumber ?? 1,
            Venue = string.IsNullOrWhiteSpace(scheduledGame?.Venue) ? $"{liveMatch.HomeTeam.Name} Ballpark" : scheduledGame!.Venue,
            AwayRecord = _franchiseSession.GetTeamRecordLabel(liveMatch.AwayTeam.Name),
            HomeRecord = _franchiseSession.GetTeamRecordLabel(liveMatch.HomeTeam.Name),
            FinalInningNumber = Math.Max(1, liveMatch.InningNumber),
            EndedInTopHalf = liveMatch.IsTopHalf,
            CompletedPlays = liveMatch.CompletedPlays,
            WasFranchiseMatch = isFranchiseMatch,
            WinningTeamName = "Game In Progress",
            SelectedTeamName = selectedTeamName,
            SelectedTeamResultLabel = BuildLiveResultLabel(liveMatch, selectedTeamName),
            NextGameLabel = isFranchiseMatch ? "Current game is still in progress." : string.Empty,
            FranchiseDateAfterGame = isFranchiseMatch ? _franchiseSession.GetCurrentFranchiseDate() : DateTime.MinValue,
            FinalPlayDescription = string.IsNullOrWhiteSpace(liveMatch.LatestEvent.Description) ? "Game in progress." : liveMatch.LatestEvent.Description,
            CompletedAtUtc = liveMatch.SavedAtUtc
        };
    }

    private static string BuildLiveResultLabel(LiveMatchSaveState liveMatch, string selectedTeamName)
    {
        if (string.IsNullOrWhiteSpace(selectedTeamName))
        {
            return string.Empty;
        }

        var isAwayClub = string.Equals(liveMatch.AwayTeam.Name, selectedTeamName, StringComparison.OrdinalIgnoreCase);
        var isHomeClub = string.Equals(liveMatch.HomeTeam.Name, selectedTeamName, StringComparison.OrdinalIgnoreCase);
        if (!isAwayClub && !isHomeClub)
        {
            return string.Empty;
        }

        var selectedTeam = isAwayClub ? liveMatch.AwayTeam : liveMatch.HomeTeam;
        var opponent = isAwayClub ? liveMatch.HomeTeam : liveMatch.AwayTeam;
        var resultLabel = selectedTeam.Runs > opponent.Runs
            ? "leading"
            : selectedTeam.Runs < opponent.Runs
                ? "trailing"
                : "tied";
        var locationLabel = isHomeClub ? "vs" : "at";
        return $"In progress: {selectedTeam.Name} are {resultLabel} {selectedTeam.Runs}-{opponent.Runs} {locationLabel} {opponent.Name}.";
    }

    private static void DrawClassicLineScore(UiRenderer uiRenderer, Rectangle bounds, CompletedLiveMatchSummaryState summary, IReadOnlyList<int>? awayRunsByInning, IReadOnlyList<int>? homeRunsByInning, int awayErrors, int homeErrors)
    {
        uiRenderer.DrawButton(string.Empty, bounds, new Color(30, 46, 58), Color.Transparent);
        uiRenderer.DrawTextInBounds("Linescore", new Rectangle(bounds.X + 10, bounds.Y + 6, 120, 18), Color.Gold, uiRenderer.UiSmallFont);

        var inningsShown = Math.Max(9, Math.Max(awayRunsByInning?.Count ?? 0, Math.Max(homeRunsByInning?.Count ?? 0, Math.Max(1, summary.FinalInningNumber))));
        var labelWidth = Math.Clamp(bounds.Width / 5, 170, 210);
        var cellWidth = Math.Max(28, (bounds.Width - labelWidth - 8) / (inningsShown + 3));
        var headerY = bounds.Y + 28;

        uiRenderer.DrawTextInBounds("Team", new Rectangle(bounds.X + 8, headerY, labelWidth - 12, 20), Color.White, uiRenderer.UiSmallFont);
        for (var inning = 1; inning <= inningsShown; inning++)
        {
            var x = bounds.X + labelWidth + ((inning - 1) * cellWidth);
            uiRenderer.DrawTextInBounds(inning.ToString(), new Rectangle(x, headerY, cellWidth, 20), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        }

        var totalsStartX = bounds.X + labelWidth + (inningsShown * cellWidth);
        uiRenderer.DrawTextInBounds("R", new Rectangle(totalsStartX, headerY, cellWidth, 20), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds("H", new Rectangle(totalsStartX + cellWidth, headerY, cellWidth, 20), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds("E", new Rectangle(totalsStartX + (cellWidth * 2), headerY, cellWidth, 20), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);

        var awayLine = awayRunsByInning ?? BuildFallbackLine(summary.AwayRuns, inningsShown);
        var homeLine = homeRunsByInning ?? BuildFallbackLine(summary.HomeRuns, inningsShown);
        DrawLineScoreRow(uiRenderer, bounds.X, headerY + 24, labelWidth, cellWidth, inningsShown, $"{summary.AwayAbbreviation}  {summary.AwayTeamName}", awayLine, summary.AwayRuns, summary.AwayHits, awayErrors, new Color(34, 52, 68));
        DrawLineScoreRow(uiRenderer, bounds.X, headerY + 54, labelWidth, cellWidth, inningsShown, $"{summary.HomeAbbreviation}  {summary.HomeTeamName}", homeLine, summary.HomeRuns, summary.HomeHits, homeErrors, new Color(30, 48, 58));
    }

    private static void DrawLineScoreRow(UiRenderer uiRenderer, int x, int y, int labelWidth, int cellWidth, int inningsShown, string teamLabel, IReadOnlyList<int> inningRuns, int runs, int hits, int errors, Color background)
    {
        var rowWidth = labelWidth + ((inningsShown + 3) * cellWidth);
        uiRenderer.DrawButton(string.Empty, new Rectangle(x, y, rowWidth, 26), background, Color.Transparent);
        uiRenderer.DrawTextInBounds(teamLabel, new Rectangle(x + 8, y + 2, labelWidth - 10, 22), Color.White, uiRenderer.UiSmallFont);

        for (var inning = 0; inning < inningsShown; inning++)
        {
            var value = inning < inningRuns.Count ? inningRuns[inning] : 0;
            uiRenderer.DrawTextInBounds(value.ToString(), new Rectangle(x + labelWidth + (inning * cellWidth), y + 2, cellWidth, 22), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        }

        var totalsX = x + labelWidth + (inningsShown * cellWidth);
        uiRenderer.DrawTextInBounds(runs.ToString(), new Rectangle(totalsX, y + 2, cellWidth, 22), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(hits.ToString(), new Rectangle(totalsX + cellWidth, y + 2, cellWidth, 22), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(errors.ToString(), new Rectangle(totalsX + (cellWidth * 2), y + 2, cellWidth, 22), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
    }

    private static void DrawPitchingPanel(UiRenderer uiRenderer, Rectangle bounds, CompletedGameBoxScoreState? boxScore, LiveMatchSaveState? liveMatch, bool isLiveSnapshot)
    {
        uiRenderer.DrawButton(string.Empty, bounds, new Color(30, 46, 58), Color.Transparent);
        uiRenderer.DrawTextInBounds("Pitching", new Rectangle(bounds.X + 10, bounds.Y + 6, 120, 18), Color.Gold, uiRenderer.UiSmallFont);

        var leftWidth = (bounds.Width / 2) - 14;
        if (isLiveSnapshot && liveMatch != null)
        {
            DrawLivePitchingColumn(uiRenderer, new Rectangle(bounds.X + 10, bounds.Y + 28, leftWidth, bounds.Height - 34), liveMatch.AwayTeam, "Away Staff");
            DrawLivePitchingColumn(uiRenderer, new Rectangle(bounds.X + leftWidth + 24, bounds.Y + 28, leftWidth, bounds.Height - 34), liveMatch.HomeTeam, "Home Staff");
            return;
        }

        DrawPitchingColumn(uiRenderer, new Rectangle(bounds.X + 10, bounds.Y + 28, leftWidth, bounds.Height - 34), boxScore?.AwayPitchingLines ?? [], "Away Staff");
        DrawPitchingColumn(uiRenderer, new Rectangle(bounds.X + leftWidth + 24, bounds.Y + 28, leftWidth, bounds.Height - 34), boxScore?.HomePitchingLines ?? [], "Home Staff");
    }

    private static void DrawLivePitchingColumn(UiRenderer uiRenderer, Rectangle bounds, MatchTeamSaveState team, string title)
    {
        uiRenderer.DrawTextInBounds(title, new Rectangle(bounds.X, bounds.Y, bounds.Width, 18), Color.White, uiRenderer.UiSmallFont);

        var pitchers = new List<MatchPlayerSnapshot>();
        if (team.CurrentPitcher != null)
        {
            pitchers.Add(team.CurrentPitcher);
        }

        if (team.StartingPitcher != null)
        {
            pitchers.Add(team.StartingPitcher);
        }

        pitchers.AddRange(team.BullpenPlayers.Where(player => team.PitchCountsByPitcher.GetValueOrDefault(player.Id) > 0));

        var lines = pitchers
            .GroupBy(player => player.Id)
            .Select(group => group.First())
            .Select(player =>
            {
                var pitchCount = team.PitchCountsByPitcher.TryGetValue(player.Id, out var count)
                    ? count
                    : player.Id == team.CurrentPitcher?.Id
                        ? team.PitchCount
                        : 0;
                var role = player.Id == team.StartingPitcher?.Id ? "SP" : "RP";
                var currentSuffix = player.Id == team.CurrentPitcher?.Id ? " (current)" : string.Empty;
                return $"[{role}] {player.FullName} - {pitchCount} pitches{currentSuffix}";
            })
            .Take(3)
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add(team.CurrentPitcher == null
                ? "No pitcher data recorded yet."
                : $"[SP] {team.CurrentPitcher.FullName} - {team.PitchCount} pitches (current)");
        }

        var y = bounds.Y + 22;
        foreach (var line in lines)
        {
            uiRenderer.DrawWrappedTextInBounds(line, new Rectangle(bounds.X, y, bounds.Width, 28), Color.White, uiRenderer.UiSmallFont, 2);
            y += 28;
        }
    }

    private static void DrawPitchingColumn(UiRenderer uiRenderer, Rectangle bounds, IReadOnlyList<CompletedPitchingLineState> lines, string title)
    {
        uiRenderer.DrawTextInBounds(title, new Rectangle(bounds.X, bounds.Y, bounds.Width, 18), Color.White, uiRenderer.UiSmallFont);

        if (lines.Count == 0)
        {
            uiRenderer.DrawTextInBounds("No pitcher lines recorded.", new Rectangle(bounds.X, bounds.Y + 22, bounds.Width, 18), Color.White, uiRenderer.UiSmallFont);
            return;
        }

        var y = bounds.Y + 22;
        foreach (var line in lines.Take(3))
        {
            var starterTag = line.IsStartingPitcher ? "SP" : "RP";
            var text = $"[{starterTag}] {line.PitcherName} - {line.PitchCount} pitches, {FormatInnings(line.InningsPitchedOuts)} IP, {line.Strikeouts} K, {line.EarnedRuns} ER";
            uiRenderer.DrawWrappedTextInBounds(text, new Rectangle(bounds.X, y, bounds.Width, 30), Color.White, uiRenderer.UiSmallFont, 2);
            y += 30;
        }
    }

    private static void DrawHighlightsPanel(UiRenderer uiRenderer, Rectangle bounds, CompletedGameBoxScoreState? boxScore, CompletedLiveMatchSummaryState summary, LiveMatchSaveState? liveMatch, bool isLiveSnapshot)
    {
        uiRenderer.DrawButton(string.Empty, bounds, new Color(30, 46, 58), Color.Transparent);
        uiRenderer.DrawTextInBounds("Notable Players", new Rectangle(bounds.X + 10, bounds.Y + 6, 180, 18), Color.Gold, uiRenderer.UiSmallFont);

        var y = bounds.Y + 28;
        if (isLiveSnapshot && liveMatch != null)
        {
            var inningText = $"In progress: {(liveMatch.IsTopHalf ? "Top" : "Bottom")} {Math.Max(1, liveMatch.InningNumber)}, {Math.Clamp(liveMatch.Outs, 0, 2)} out(s).";
            var matchupText = $"At bat: {GetCurrentBatterName(liveMatch)} vs {GetCurrentPitcherName(liveMatch)}.";
            var playText = string.IsNullOrWhiteSpace(liveMatch.LatestEvent.Description) ? "Resume the game for the next pitch." : liveMatch.LatestEvent.Description;

            uiRenderer.DrawWrappedTextInBounds(inningText, new Rectangle(bounds.X + 10, y, bounds.Width - 20, 22), Color.White, uiRenderer.UiSmallFont, 2);
            uiRenderer.DrawWrappedTextInBounds(matchupText, new Rectangle(bounds.X + 10, y + 22, bounds.Width - 20, 22), Color.White, uiRenderer.UiSmallFont, 2);
            uiRenderer.DrawWrappedTextInBounds(playText, new Rectangle(bounds.X + 10, y + 44, bounds.Width - 20, 28), Color.White, uiRenderer.UiSmallFont, 2);
            uiRenderer.DrawWrappedTextInBounds("Final standout player lines will populate automatically once the game ends.", new Rectangle(bounds.X + 10, y + 72, bounds.Width - 20, 28), Color.White, uiRenderer.UiSmallFont, 2);
            return;
        }

        var highlights = boxScore?.NotablePlayers ?? [];
        if (highlights.Count == 0)
        {
            uiRenderer.DrawWrappedTextInBounds(summary.FinalPlayDescription, new Rectangle(bounds.X + 10, y, bounds.Width - 20, 34), Color.White, uiRenderer.UiSmallFont, 2);
            uiRenderer.DrawWrappedTextInBounds(string.IsNullOrWhiteSpace(summary.NextGameLabel) ? "Return to the franchise flow to continue." : $"Next: {summary.NextGameLabel}", new Rectangle(bounds.X + 10, y + 38, bounds.Width - 20, 34), Color.White, uiRenderer.UiSmallFont, 2);
            return;
        }

        foreach (var highlight in highlights.Take(4))
        {
            var text = $"[{highlight.TeamAbbreviation}] {highlight.PlayerName} ({highlight.PrimaryPosition}) - {highlight.SummaryLine}";
            uiRenderer.DrawWrappedTextInBounds(text, new Rectangle(bounds.X + 10, y, bounds.Width - 20, 24), Color.White, uiRenderer.UiSmallFont, 2);
            y += 24;
        }
    }

    private static string GetCurrentBatterName(LiveMatchSaveState liveMatch)
    {
        var offense = liveMatch.IsTopHalf ? liveMatch.AwayTeam : liveMatch.HomeTeam;
        if (offense.Lineup.Count == 0)
        {
            return "Current batter";
        }

        var index = Math.Clamp(offense.BattingIndex, 0, offense.Lineup.Count - 1);
        return offense.Lineup[index].FullName;
    }

    private static string GetCurrentPitcherName(LiveMatchSaveState liveMatch)
    {
        var defense = liveMatch.IsTopHalf ? liveMatch.HomeTeam : liveMatch.AwayTeam;
        return defense.CurrentPitcher?.FullName
            ?? defense.StartingPitcher?.FullName
            ?? "current pitcher";
    }

    private static IReadOnlyList<int> BuildFallbackLine(int totalRuns, int inningsShown)
    {
        var line = Enumerable.Repeat(0, Math.Max(1, inningsShown)).ToList();
        line[^1] = Math.Max(0, totalRuns);
        return line;
    }

    private static string FormatInnings(int outs)
    {
        return $"{Math.Max(0, outs) / 3}.{Math.Max(0, outs) % 3}";
    }

    private void ContinueFlow()
    {
        var liveMatch = _franchiseSession.GetLiveMatchState();
        if (_franchiseSession.ShouldUseLiveBoxScore() && liveMatch is { IsGameOver: false })
        {
            _franchiseSession.SetPreferLiveBoxScore(false);
            _screenManager.TransitionTo(nameof(LiveMatchScreen));
            return;
        }

        _franchiseSession.SetPreferLiveBoxScore(false);
        _screenManager.TransitionTo(nameof(PostGameScreen));
    }

    private void ReturnToDestination()
    {
        var liveMatch = _franchiseSession.GetLiveMatchState();
        if (_franchiseSession.ShouldUseLiveBoxScore() && liveMatch is { IsGameOver: false })
        {
            _franchiseSession.SetPreferLiveBoxScore(false);
            _screenManager.TransitionTo(nameof(LiveMatchScreen));
            return;
        }

        _franchiseSession.SetPreferLiveBoxScore(false);
        var summary = _franchiseSession.GetLastCompletedLiveMatchSummary();
        var destination = summary?.WasFranchiseMatch == true
            ? nameof(FranchiseHubScreen)
            : nameof(MainMenuScreen);
        _screenManager.TransitionTo(destination);
    }

    private bool IsNewKeyPress(KeyboardState currentKeyboardState, Keys key)
    {
        return currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }
}
