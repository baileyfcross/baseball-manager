using BaseballManager.Game.Screens.LiveMatch;
using Microsoft.Xna.Framework;

namespace BaseballManager.Game.Graphics.Rendering.LiveMatch;

public sealed class LiveMatchHudRenderer
{
    public void Draw(LiveMatchViewModel viewModel, UiRenderer uiRenderer)
    {
        var viewport = uiRenderer.Viewport;
        var panelX = viewport.Width - 300;
        var scorePanel = new Rectangle(panelX, 40, 250, 180);
        var detailPanel = new Rectangle(panelX, 240, 250, 190);
        var latestPlayPanel = new Rectangle(panelX, 450, 250, 180);

        uiRenderer.DrawButton(string.Empty, scorePanel, new Color(20, 35, 48, 215), Color.Transparent);
        uiRenderer.DrawText("LIVE MATCH", new Vector2(panelX + 18, 56), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText($"{viewModel.AwayAbbreviation}  {viewModel.AwayScore}", new Vector2(panelX + 20, 98), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText($"{viewModel.HomeAbbreviation}  {viewModel.HomeScore}", new Vector2(panelX + 20, 132), Color.White, uiRenderer.UiMediumFont);
        uiRenderer.DrawText($"{(viewModel.IsTopHalf ? "Top" : "Bottom")} {viewModel.InningNumber}", new Vector2(panelX + 20, 172), Color.Gold, uiRenderer.ScoreboardFont);

        uiRenderer.DrawButton(string.Empty, detailPanel, new Color(24, 48, 28, 210), Color.Transparent);
        uiRenderer.DrawText($"Count: {viewModel.Balls}-{viewModel.Strikes}", new Vector2(panelX + 18, 258), Color.White, uiRenderer.ScoreboardFont);
        uiRenderer.DrawText($"Outs: {viewModel.Outs}", new Vector2(panelX + 18, 288), Color.White, uiRenderer.ScoreboardFont);
        uiRenderer.DrawText($"Batter: {Trim(viewModel.BatterName, 20)}", new Vector2(panelX + 18, 324), Color.White);
        uiRenderer.DrawText($"Pitcher: {Trim(viewModel.PitcherName, 20)}", new Vector2(panelX + 18, 352), Color.White);
        uiRenderer.DrawText($"On 1st: {RunnerDisplay(viewModel.RunnerOnFirst, viewModel.RunnerOnFirstName)}", new Vector2(panelX + 18, 382), Color.White);
        uiRenderer.DrawText($"On 2nd: {RunnerDisplay(viewModel.RunnerOnSecond, viewModel.RunnerOnSecondName)}", new Vector2(panelX + 18, 406), Color.White);
        uiRenderer.DrawText($"On 3rd: {RunnerDisplay(viewModel.RunnerOnThird, viewModel.RunnerOnThirdName)}", new Vector2(panelX + 18, 430), Color.White);

        uiRenderer.DrawButton(string.Empty, latestPlayPanel, new Color(48, 26, 24, 210), Color.Transparent);
        uiRenderer.DrawText(viewModel.IsGameOver ? "FINAL" : "LATEST PLAY", new Vector2(panelX + 18, 466), viewModel.IsGameOver ? Color.Gold : Color.White, uiRenderer.UiMediumFont);

        var lineY = 504f;
        foreach (var line in WrapText(viewModel.LatestPlayText, 28))
        {
            uiRenderer.DrawText(line, new Vector2(panelX + 18, lineY), Color.White);
            lineY += 24f;
        }

        uiRenderer.DrawText(viewModel.StatusText, new Vector2(48, viewport.Height - 42), Color.White, uiRenderer.ScoreboardFont);
    }

    private static IEnumerable<string> WrapText(string text, int maxChars)
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
            if (candidate.Length <= maxChars)
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

    private static string RunnerDisplay(bool occupied, string playerName)
    {
        return occupied ? Trim(playerName, 16) : "-";
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
