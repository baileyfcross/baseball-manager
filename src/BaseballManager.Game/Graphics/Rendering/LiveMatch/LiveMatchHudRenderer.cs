using BaseballManager.Game.Screens.LiveMatch;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.Graphics.Rendering.LiveMatch;

public sealed class LiveMatchHudRenderer
{
    public void Draw(LiveMatchViewModel viewModel, UiRenderer uiRenderer)
    {
        var viewport = uiRenderer.Viewport;
        var outerMargin = Math.Clamp(viewport.Width / 80, 10, 18);
        const int panelGap = 12;
        var sidebarWidth = Math.Clamp((int)(viewport.Width * 0.31f), 300, 420);
        var panelX = viewport.Width - sidebarWidth - outerMargin;
        var panelWidth = sidebarWidth;
        var contentWidth = panelWidth - 28;
        var topY = 40;
        var availableHeight = Math.Max(320, viewport.Height - topY - 56);

        var scorePanelHeight = Math.Clamp((int)(availableHeight * 0.23f), 132, 186);
        var detailPanelHeight = Math.Clamp((int)(availableHeight * 0.27f), 164, 210);
        var latestPlayHeight = Math.Max(96, availableHeight - scorePanelHeight - detailPanelHeight - (panelGap * 2));

        var totalStackHeight = scorePanelHeight + detailPanelHeight + latestPlayHeight + (panelGap * 2);
        if (totalStackHeight > availableHeight)
        {
            var scale = (availableHeight - (panelGap * 2)) / (float)(scorePanelHeight + detailPanelHeight + latestPlayHeight);
            scorePanelHeight = Math.Max(118, (int)Math.Floor(scorePanelHeight * scale));
            detailPanelHeight = Math.Max(148, (int)Math.Floor(detailPanelHeight * scale));
            latestPlayHeight = Math.Max(84, availableHeight - scorePanelHeight - detailPanelHeight - (panelGap * 2));
        }

        var scorePanel = new Rectangle(panelX, topY, panelWidth, scorePanelHeight);
        var detailPanel = new Rectangle(panelX, scorePanel.Bottom + panelGap, panelWidth, detailPanelHeight);
        var latestPlayPanel = new Rectangle(panelX, detailPanel.Bottom + panelGap, panelWidth, Math.Max(104, latestPlayHeight));

        DrawLiveBoxScore(viewModel, uiRenderer, scorePanel);

        uiRenderer.DrawButton(string.Empty, detailPanel, new Color(24, 48, 28, 210), Color.Transparent);
        uiRenderer.DrawText("GAME DETAILS", new Vector2(panelX + 16, detailPanel.Y + 14), Color.White, uiRenderer.UiMediumFont);

        var statsRowY = detailPanel.Y + 42;
        var halfWidth = (contentWidth - 8) / 2;
        uiRenderer.DrawTextInBounds($"Count: {viewModel.Balls}-{viewModel.Strikes}", new Rectangle(panelX + 14, statsRowY, halfWidth, 20), Color.White, uiRenderer.ScoreboardFont);
        uiRenderer.DrawTextInBounds($"Outs: {viewModel.Outs}", new Rectangle(panelX + 22 + halfWidth, statsRowY, halfWidth, 20), Color.White, uiRenderer.ScoreboardFont);

        var lineY = statsRowY + 28;
        uiRenderer.DrawTextInBounds($"Batter: {TrimToWidth(viewModel.BatterName, contentWidth - 56, uiRenderer.UiSmallFont)}", new Rectangle(panelX + 14, lineY, contentWidth, 20), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Pitcher: {TrimToWidth(viewModel.PitcherName, contentWidth - 60, uiRenderer.UiSmallFont)}", new Rectangle(panelX + 14, lineY + 24, contentWidth, 20), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawTextInBounds($"Pitch Ct: {viewModel.PitchCount}   Arm: {viewModel.PitcherFatigueText}", new Rectangle(panelX + 14, lineY + 48, contentWidth, 20), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(BuildBasesText(viewModel), new Rectangle(panelX + 14, lineY + 72, contentWidth, Math.Max(28, detailPanel.Bottom - (lineY + 72) - 10)), Color.White, uiRenderer.UiSmallFont, 2);

        uiRenderer.DrawButton(string.Empty, latestPlayPanel, new Color(48, 26, 24, 210), Color.Transparent);
        uiRenderer.DrawText(viewModel.IsGameOver ? "FINAL PLAY" : "LATEST PLAY", new Vector2(panelX + 16, latestPlayPanel.Y + 14), viewModel.IsGameOver ? Color.Gold : Color.White, uiRenderer.UiMediumFont);

        var latestLineY = latestPlayPanel.Y + 50f;
        foreach (var line in WrapTextToWidth(viewModel.LatestPlayText, contentWidth, uiRenderer.UiSmallFont))
        {
            uiRenderer.DrawText(line, new Vector2(panelX + 16, latestLineY), Color.White);
            latestLineY += 24f;
            if (latestLineY > latestPlayPanel.Bottom - 18)
            {
                break;
            }
        }

        uiRenderer.DrawText(viewModel.StatusText, new Vector2(48, viewport.Height - 42), Color.White, uiRenderer.ScoreboardFont);

        if (viewModel.ManagerMenuVisible)
        {
            DrawManagerOverlay(viewModel, uiRenderer);
        }
    }

    private static void DrawLiveBoxScore(LiveMatchViewModel viewModel, UiRenderer uiRenderer, Rectangle bounds)
    {
        uiRenderer.DrawButton(string.Empty, bounds, new Color(20, 35, 48, 215), Color.Transparent);
        uiRenderer.DrawTextInBounds("LIVE BOX SCORE", new Rectangle(bounds.X + 12, bounds.Y + 10, bounds.Width - 124, 24), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawTextInBounds($"{(viewModel.IsTopHalf ? "Top" : "Bottom")} {viewModel.InningNumber}", new Rectangle(bounds.Right - 110, bounds.Y + 10, 98, 24), Color.Gold, uiRenderer.ScoreboardFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds($"{viewModel.AwayTeamName} at {viewModel.HomeTeamName}", new Rectangle(bounds.X + 12, bounds.Y + 36, bounds.Width - 24, 18), Color.White, uiRenderer.UiSmallFont);

        var inningsShown = Math.Max(9, Math.Max(viewModel.InningNumber, Math.Max(viewModel.AwayRunsByInning.Count, viewModel.HomeRunsByInning.Count)));
        var innerBounds = new Rectangle(bounds.X + 8, bounds.Y + 60, bounds.Width - 16, bounds.Height - 68);
        var labelWidth = Math.Clamp(innerBounds.Width / 7, 42, 56);
        var cellWidth = Math.Max(14, (innerBounds.Width - labelWidth) / (inningsShown + 3));
        var headerY = innerBounds.Y;

        uiRenderer.DrawTextInBounds("Tm", new Rectangle(innerBounds.X + 2, headerY, labelWidth - 4, 16), Color.White, uiRenderer.UiSmallFont);
        for (var inning = 1; inning <= inningsShown; inning++)
        {
            var x = innerBounds.X + labelWidth + ((inning - 1) * cellWidth);
            uiRenderer.DrawTextInBounds(inning.ToString(), new Rectangle(x, headerY, cellWidth, 16), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        }

        var totalsX = innerBounds.X + labelWidth + (inningsShown * cellWidth);
        uiRenderer.DrawTextInBounds("R", new Rectangle(totalsX, headerY, cellWidth, 16), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds("H", new Rectangle(totalsX + cellWidth, headerY, cellWidth, 16), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds("E", new Rectangle(totalsX + (cellWidth * 2), headerY, cellWidth, 16), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);

        DrawLineScoreRow(uiRenderer, new Rectangle(innerBounds.X, headerY + 20, innerBounds.Width, 22), labelWidth, cellWidth, viewModel.AwayAbbreviation, viewModel.AwayRunsByInning, viewModel.AwayScore, viewModel.AwayHits, viewModel.AwayErrors, new Color(34, 52, 68));
        DrawLineScoreRow(uiRenderer, new Rectangle(innerBounds.X, headerY + 46, innerBounds.Width, 22), labelWidth, cellWidth, viewModel.HomeAbbreviation, viewModel.HomeRunsByInning, viewModel.HomeScore, viewModel.HomeHits, viewModel.HomeErrors, new Color(30, 48, 58));
    }

    private static void DrawLineScoreRow(UiRenderer uiRenderer, Rectangle bounds, int labelWidth, int cellWidth, string teamLabel, IReadOnlyList<int> inningRuns, int runs, int hits, int errors, Color background)
    {
        uiRenderer.DrawButton(string.Empty, bounds, background, Color.Transparent);
        uiRenderer.DrawTextInBounds(teamLabel, new Rectangle(bounds.X + 4, bounds.Y + 2, labelWidth - 6, bounds.Height - 4), Color.White, uiRenderer.UiSmallFont);

        var inningsShown = Math.Max(0, (bounds.Width - labelWidth) / Math.Max(1, cellWidth) - 3);
        for (var inning = 0; inning < inningsShown; inning++)
        {
            var value = inning < inningRuns.Count ? inningRuns[inning] : 0;
            var x = bounds.X + labelWidth + (inning * cellWidth);
            uiRenderer.DrawTextInBounds(value.ToString(), new Rectangle(x, bounds.Y + 2, cellWidth, bounds.Height - 4), Color.White, uiRenderer.UiSmallFont, centerHorizontally: true);
        }

        var totalsX = bounds.X + labelWidth + (inningsShown * cellWidth);
        uiRenderer.DrawTextInBounds(runs.ToString(), new Rectangle(totalsX, bounds.Y + 2, cellWidth, bounds.Height - 4), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(hits.ToString(), new Rectangle(totalsX + cellWidth, bounds.Y + 2, cellWidth, bounds.Height - 4), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawTextInBounds(errors.ToString(), new Rectangle(totalsX + (cellWidth * 2), bounds.Y + 2, cellWidth, bounds.Height - 4), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
    }

    private static void DrawManagerOverlay(LiveMatchViewModel viewModel, UiRenderer uiRenderer)
    {
        var panelBounds = new Rectangle(40, 40, 420, 320);
        uiRenderer.DrawButton(string.Empty, panelBounds, new Color(18, 24, 34, 228), Color.Transparent);
        uiRenderer.DrawText("MANAGER", new Vector2(panelBounds.X + 18, panelBounds.Y + 16), Color.Gold, uiRenderer.UiMediumFont);
        uiRenderer.DrawText(viewModel.ManagerModeLabel, new Vector2(panelBounds.X + 18, panelBounds.Y + 52), Color.White, uiRenderer.UiSmallFont);
        uiRenderer.DrawWrappedTextInBounds(viewModel.ManagerPromptText, new Rectangle(panelBounds.X + 18, panelBounds.Y + 80, panelBounds.Width - 36, 50), Color.White, uiRenderer.UiSmallFont, 3);
        uiRenderer.DrawTextInBounds(viewModel.ManagerTargetLabel, new Rectangle(panelBounds.X + 18, panelBounds.Y + 136, panelBounds.Width - 36, 22), Color.White, uiRenderer.UiSmallFont);

        var optionsTop = panelBounds.Y + 168;
        if (viewModel.ManagerOptions.Count == 0)
        {
            uiRenderer.DrawTextInBounds("No options available.", new Rectangle(panelBounds.X + 18, optionsTop, panelBounds.Width - 36, 24), Color.Orange, uiRenderer.UiSmallFont);
        }
        else
        {
            for (var index = 0; index < Math.Min(5, viewModel.ManagerOptions.Count); index++)
            {
                var rowBounds = new Rectangle(panelBounds.X + 18, optionsTop + (index * 28), panelBounds.Width - 36, 24);
                var isSelected = index == viewModel.ManagerSelectionIndex;
                uiRenderer.DrawButton(string.Empty, rowBounds, isSelected ? new Color(56, 78, 102) : new Color(34, 42, 52), Color.Transparent);
                var label = isSelected ? $"> {viewModel.ManagerOptions[index]}" : viewModel.ManagerOptions[index];
                uiRenderer.DrawTextInBounds(label, new Rectangle(rowBounds.X + 6, rowBounds.Y + 1, rowBounds.Width - 12, rowBounds.Height - 2), Color.White, uiRenderer.UiSmallFont);
            }
        }

        uiRenderer.DrawWrappedTextInBounds(viewModel.ManagerFeedbackText, new Rectangle(panelBounds.X + 18, panelBounds.Bottom - 54, panelBounds.Width - 36, 36), Color.Gold, uiRenderer.UiSmallFont, 2);
    }

    private static IEnumerable<string> WrapTextToWidth(string text, int maxWidth, SpriteFont? font)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return "Waiting for the next pitch.";
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (MeasureWidth(candidate, font) <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }

            // If an individual token is still too wide, hard-wrap it.
            if (MeasureWidth(word, font) <= maxWidth)
            {
                currentLine = word;
                continue;
            }

            var remaining = word;
            while (!string.IsNullOrEmpty(remaining))
            {
                var split = 1;
                while (split <= remaining.Length && MeasureWidth(remaining[..split], font) <= maxWidth)
                {
                    split++;
                }

                var take = Math.Max(1, split - 1);
                yield return remaining[..take];
                remaining = remaining[take..];
            }

            currentLine = string.Empty;
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            yield return currentLine;
        }
    }

    private static float MeasureWidth(string value, SpriteFont? font)
    {
        return font?.MeasureString(value).X ?? (value.Length * 8f);
    }

    private static string BuildBasesText(LiveMatchViewModel viewModel)
    {
        return $"Bases: 1B {RunnerDisplay(viewModel.RunnerOnFirst, viewModel.RunnerOnFirstName)} | 2B {RunnerDisplay(viewModel.RunnerOnSecond, viewModel.RunnerOnSecondName)} | 3B {RunnerDisplay(viewModel.RunnerOnThird, viewModel.RunnerOnThirdName)}";
    }

    private static string RunnerDisplay(bool occupied, string playerName)
    {
        return occupied ? Trim(playerName, 10) : "-";
    }

    private static string TrimToWidth(string value, int maxWidth, SpriteFont? font)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        if (MeasureWidth(value, font) <= maxWidth)
        {
            return value;
        }

        var trimmed = value;
        while (trimmed.Length > 1 && MeasureWidth(trimmed + "...", font) > maxWidth)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed + "...";
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
