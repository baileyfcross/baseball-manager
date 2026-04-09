using BaseballManager.Game.Screens.LiveMatch;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.Graphics.Rendering.LiveMatch;

public sealed class LiveMatchHudRenderer
{
    public void Draw(LiveMatchViewModel viewModel, UiRenderer uiRenderer)
    {
        var viewport = uiRenderer.Viewport;
        const int sidebarWidth = 350;
        const int panelHorizontalMargin = 12;
        var panelX = viewport.Width - sidebarWidth + panelHorizontalMargin;
        var panelWidth = sidebarWidth - (panelHorizontalMargin * 2);
        var contentWidth = panelWidth - 36;
        var scorePanel = new Rectangle(panelX, 40, panelWidth, 180);
        var detailPanel = new Rectangle(panelX, 240, panelWidth, 250);
        var latestPlayY = detailPanel.Bottom + 20;
        var latestPlayHeight = Math.Max(140, viewport.Height - latestPlayY - 40);
        var latestPlayPanel = new Rectangle(panelX, latestPlayY, panelWidth, latestPlayHeight);

        uiRenderer.DrawButton(string.Empty, scorePanel, new Color(20, 35, 48, 215), Color.Transparent);
        uiRenderer.DrawText("LIVE MATCH", new Vector2(panelX + 18, 56), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText($"{viewModel.AwayAbbreviation}  {viewModel.AwayScore}", new Vector2(panelX + 20, 98), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText($"{viewModel.HomeAbbreviation}  {viewModel.HomeScore}", new Vector2(panelX + 20, 132), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText($"{(viewModel.IsTopHalf ? "Top" : "Bottom")} {viewModel.InningNumber}", new Vector2(panelX + 20, 172), Color.Gold, uiRenderer.ScoreboardFont);

        uiRenderer.DrawButton(string.Empty, detailPanel, new Color(24, 48, 28, 210), Color.Transparent);
        uiRenderer.DrawText($"Count: {viewModel.Balls}-{viewModel.Strikes}", new Vector2(panelX + 18, 258), Color.White, uiRenderer.ScoreboardFont);
        uiRenderer.DrawText($"Outs: {viewModel.Outs}", new Vector2(panelX + 18, 288), Color.White, uiRenderer.ScoreboardFont);
        uiRenderer.DrawText($"Batter: {TrimToWidth(viewModel.BatterName, contentWidth - 62, uiRenderer.UiSmallFont)}", new Vector2(panelX + 18, 324), Color.White);
        uiRenderer.DrawText($"Pitcher: {TrimToWidth(viewModel.PitcherName, contentWidth - 68, uiRenderer.UiSmallFont)}", new Vector2(panelX + 18, 352), Color.White);
        uiRenderer.DrawText($"Pitch Ct: {viewModel.PitchCount}", new Vector2(panelX + 18, 380), Color.White);
        uiRenderer.DrawText($"Arm: {viewModel.PitcherFatigueText}", new Vector2(panelX + 18, 404), Color.White);
        uiRenderer.DrawText($"On 1st: {RunnerDisplay(viewModel.RunnerOnFirst, viewModel.RunnerOnFirstName)}", new Vector2(panelX + 18, 430), Color.White);
        uiRenderer.DrawText($"On 2nd: {RunnerDisplay(viewModel.RunnerOnSecond, viewModel.RunnerOnSecondName)}", new Vector2(panelX + 18, 454), Color.White);
        uiRenderer.DrawText($"On 3rd: {RunnerDisplay(viewModel.RunnerOnThird, viewModel.RunnerOnThirdName)}", new Vector2(panelX + 18, 478), Color.White);

        uiRenderer.DrawButton(string.Empty, latestPlayPanel, new Color(48, 26, 24, 210), Color.Transparent);
        uiRenderer.DrawText(viewModel.IsGameOver ? "FINAL" : "LATEST PLAY", new Vector2(panelX + 18, latestPlayPanel.Y + 16), viewModel.IsGameOver ? Color.Gold : Color.White, uiRenderer.UiMediumFont);

        var lineY = latestPlayPanel.Y + 54f;
        foreach (var line in WrapTextToWidth(viewModel.LatestPlayText, contentWidth, uiRenderer.UiSmallFont))
        {
            uiRenderer.DrawText(line, new Vector2(panelX + 18, lineY), Color.White);
            lineY += 24f;
            if (lineY > latestPlayPanel.Bottom - 22)
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

    private static string RunnerDisplay(bool occupied, string playerName)
    {
        return occupied ? Trim(playerName, 14) : "-";
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
