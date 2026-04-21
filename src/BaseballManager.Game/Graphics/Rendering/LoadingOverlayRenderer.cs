using BaseballManager.Game.Data;
using Microsoft.Xna.Framework;

namespace BaseballManager.Game.Graphics.Rendering;

public static class LoadingOverlayRenderer
{
    public static void Draw(UiRenderer uiRenderer, AsyncOperationProgressView progress, string footerText)
    {
        var viewport = uiRenderer.Viewport;
        var backdrop = new Rectangle(0, 0, viewport.Width, viewport.Height);
        var panelWidth = Math.Min(760, Math.Max(420, viewport.Width - 180));
        var panelHeight = 220;
        var panel = new Rectangle((viewport.Width - panelWidth) / 2, (viewport.Height - panelHeight) / 2, panelWidth, panelHeight);
        var barOuter = new Rectangle(panel.X + 36, panel.Y + 122, panel.Width - 72, 28);
        var innerPadding = 4;
        var fillWidth = Math.Max(0, (int)Math.Round((barOuter.Width - (innerPadding * 2)) * progress.ClampedProgressValue));
        var barFill = new Rectangle(barOuter.X + innerPadding, barOuter.Y + innerPadding, fillWidth, barOuter.Height - (innerPadding * 2));

        uiRenderer.DrawButton(string.Empty, backdrop, new Color(8, 12, 18, 215), Color.Transparent);
        uiRenderer.DrawButton(string.Empty, panel, new Color(28, 38, 46), Color.White);
        uiRenderer.DrawTextInBounds(progress.Title, new Rectangle(panel.X + 24, panel.Y + 24, panel.Width - 48, 28), Color.White, uiRenderer.UiMediumFont, centerHorizontally: true);
        uiRenderer.DrawWrappedTextInBounds(progress.Status, new Rectangle(panel.X + 32, panel.Y + 62, panel.Width - 64, 44), Color.White, uiRenderer.UiSmallFont, 2);
        uiRenderer.DrawButton(string.Empty, barOuter, new Color(54, 66, 78), Color.Transparent);
        uiRenderer.DrawButton(string.Empty, barFill, new Color(108, 170, 92), Color.Transparent);
        uiRenderer.DrawTextInBounds($"{progress.PercentComplete}%", new Rectangle(panel.X + 24, barOuter.Bottom + 10, panel.Width - 48, 20), Color.Gold, uiRenderer.UiSmallFont, centerHorizontally: true);
        uiRenderer.DrawWrappedTextInBounds(footerText, new Rectangle(panel.X + 32, panel.Bottom - 44, panel.Width - 64, 28), Color.LightGray, uiRenderer.UiSmallFont, 2);
    }
}
