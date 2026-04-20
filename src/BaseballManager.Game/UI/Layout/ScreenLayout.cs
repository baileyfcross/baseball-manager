using Microsoft.Xna.Framework;

namespace BaseballManager.Game.UI.Layout;

/// <summary>
/// Viewport-aware layout constants shared across all game screens.
/// Every value scales proportionally with the current window size so the
/// UI looks correct at 576p, 720p, 900p, and 1080p (and anything in between).
/// </summary>
public static class ScreenLayout
{
    // -----------------------------------------------------------------------
    // Back button (top-left corner, present on most non-hub screens)
    // -----------------------------------------------------------------------

    /// <summary>Standard back button in the top-left corner.
    /// Height scales with viewport height (34 px at 576p → 44 px at 1080p).</summary>
    public static Rectangle BackButtonBounds(Point viewport) =>
        new(24, 34, 120, BackButtonHeight(viewport));

    /// <summary>Scaled height for the standard back button.</summary>
    public static int BackButtonHeight(Point viewport) =>
        Math.Clamp(viewport.Y / 20, 34, 44);

    // -----------------------------------------------------------------------
    // Header zone  (title at ~42 px, subtitle at ~82 px – both fixed to
    // the top-left corner so they always align with the back button)
    // -----------------------------------------------------------------------

    public const int TitleY = 42;
    public const int SubtitleY = 82;
    public const int DescriptionY = 112;

    /// <summary>X for title text on screens that show a Back button to the left
    /// (168 = 24 left-pad + 120 button width + 24 gap).</summary>
    public const int TitleXAfterBackButton = 168;

    /// <summary>X for title text on screens that own the full top-left area
    /// (main menu, franchise hub).</summary>
    public const int TitleXNoBackButton = 56;

    // -----------------------------------------------------------------------
    // Content area
    // -----------------------------------------------------------------------

    /// <summary>Y coordinate where main content panels start (below the header).
    /// Scales from 140 px (576p) to 224 px (1080p).</summary>
    public static int ContentTop(Point viewport) =>
        Math.Clamp(viewport.Y * 22 / 100, 140, 224);

    /// <summary>Standard horizontal left-edge for content panels.</summary>
    public const int ContentLeft = 48;

    // -----------------------------------------------------------------------
    // Bottom toolbar
    // -----------------------------------------------------------------------

    /// <summary>Height of bottom toolbar buttons.
    /// Scales from 34 px (576p) to 44 px (1080p).</summary>
    public static int ToolbarButtonHeight(Point viewport) =>
        Math.Clamp(viewport.Y / 20, 34, 44);

    /// <summary>Distance from the bottom of the viewport to the top of the toolbar.
    /// Scales from 52 px (576p) to 68 px (1080p).</summary>
    public static int ToolbarBottomOffset(Point viewport) =>
        Math.Clamp(viewport.Y * 9 / 100, 52, 68);

    /// <summary>Y coordinate for the top edge of the bottom toolbar row.</summary>
    public static int ToolbarY(Point viewport) =>
        viewport.Y - ToolbarBottomOffset(viewport);
}
