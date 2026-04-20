using BaseballManager.Game.Data;
using BaseballManager.Game.Graphics.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.UI.Widgets;

public sealed class PlayerContextOverlay
{
    private const int MenuWidth = 220;
    private const int RowHeight = 28;
    private const int HeaderHeight = 24;
    private const int ProfileMinWidth = 520;
    private const int ProfileMaxWidth = 640;
    private const int ProfileMinHeight = 392;
    private const int ProfilePadding = 14;
    private const int ProfileSectionSpacing = 8;
    private const int WrappedLineSpacing = 2;
    private const int FallbackWrappedLineHeight = 24;
    private readonly List<PlayerContextActionView> _primaryActions = [];
    private readonly List<PlayerContextActionView> _rosterActions = [];
    private Point _anchor;
    private string _title = string.Empty;
    private PlayerProfileView? _profile;
    private bool _showRosterSubmenu;
    private bool _showProfile;

    public bool IsOpen => _primaryActions.Count > 0;

    public bool IsCapturingMouse => IsOpen || _showProfile;

    public void Open(Point anchor, string title, IReadOnlyList<PlayerContextActionView> primaryActions, IReadOnlyList<PlayerContextActionView> rosterActions, PlayerProfileView? profile)
    {
        _anchor = anchor;
        _title = title;
        _profile = profile;
        _showRosterSubmenu = false;
        _showProfile = false;
        _primaryActions.Clear();
        _primaryActions.AddRange(primaryActions);
        _rosterActions.Clear();
        _rosterActions.AddRange(rosterActions);
    }

    public void Close()
    {
        _primaryActions.Clear();
        _rosterActions.Clear();
        _profile = null;
        _showRosterSubmenu = false;
        _showProfile = false;
        _title = string.Empty;
    }

    public bool HandleLeftClick(Point mousePosition, Point viewport, out PlayerContextAction? action)
    {
        action = null;
        if (!IsCapturingMouse)
        {
            return false;
        }

        if (_showProfile)
        {
            if (GetProfileCloseBounds(viewport).Contains(mousePosition) || !GetProfileBounds(viewport).Contains(mousePosition))
            {
                _showProfile = false;
                if (!IsOpen)
                {
                    Close();
                }
            }

            return true;
        }

        var menuBounds = GetMenuBounds(viewport);
        var submenuBounds = GetRosterSubmenuBounds(viewport);
        var clickedMenu = menuBounds.Contains(mousePosition);
        var clickedSubmenu = _showRosterSubmenu && submenuBounds.Contains(mousePosition);
        if (!clickedMenu && !clickedSubmenu)
        {
            Close();
            return true;
        }

        for (var i = 0; i < _primaryActions.Count; i++)
        {
            var bounds = GetPrimaryItemBounds(viewport, i);
            if (!bounds.Contains(mousePosition))
            {
                continue;
            }

            var item = _primaryActions[i];
            if (!item.IsEnabled)
            {
                return true;
            }

            if (item.Action == PlayerContextAction.OpenRosterAssignments)
            {
                _showRosterSubmenu = _rosterActions.Count > 0 && !_showRosterSubmenu;
                return true;
            }

            if (item.Action == PlayerContextAction.OpenProfile && _profile != null)
            {
                _showProfile = true;
                return true;
            }

            action = item.Action;
            Close();
            return true;
        }

        if (_showRosterSubmenu)
        {
            for (var i = 0; i < _rosterActions.Count; i++)
            {
                var bounds = GetRosterItemBounds(viewport, i);
                if (!bounds.Contains(mousePosition))
                {
                    continue;
                }

                var item = _rosterActions[i];
                if (!item.IsEnabled)
                {
                    return true;
                }

                action = item.Action;
                Close();
                return true;
            }
        }

        return true;
    }

    public void Draw(UiRenderer uiRenderer, Point mousePosition, Point viewport)
    {
        if (IsOpen)
        {
            var menuBounds = GetMenuBounds(viewport);
            uiRenderer.DrawButton(string.Empty, menuBounds, new Color(24, 30, 38), Color.Transparent);
            uiRenderer.DrawTextInBounds(_title, new Rectangle(menuBounds.X + 6, menuBounds.Y + 4, menuBounds.Width - 12, 16), Color.Gold, uiRenderer.UiSmallFont);

            for (var i = 0; i < _primaryActions.Count; i++)
            {
                var item = _primaryActions[i];
                var bounds = GetPrimaryItemBounds(viewport, i);
                var background = !item.IsEnabled
                    ? new Color(70, 70, 70)
                    : bounds.Contains(mousePosition)
                        ? Color.DarkSlateBlue
                        : new Color(48, 58, 68);
                var label = item.Action == PlayerContextAction.OpenRosterAssignments && _rosterActions.Count > 0
                    ? $"{item.Label} >"
                    : item.Label;
                uiRenderer.DrawButton(label, bounds, background, item.IsEnabled ? Color.White : new Color(188, 188, 188), uiRenderer.UiSmallFont);
            }

            if (_showRosterSubmenu)
            {
                var submenuBounds = GetRosterSubmenuBounds(viewport);
                uiRenderer.DrawButton(string.Empty, submenuBounds, new Color(24, 30, 38), Color.Transparent);
                uiRenderer.DrawTextInBounds("Roster", new Rectangle(submenuBounds.X + 6, submenuBounds.Y + 4, submenuBounds.Width - 12, 16), Color.Gold, uiRenderer.UiSmallFont);

                for (var i = 0; i < _rosterActions.Count; i++)
                {
                    var item = _rosterActions[i];
                    var bounds = GetRosterItemBounds(viewport, i);
                    var background = !item.IsEnabled
                        ? new Color(70, 70, 70)
                        : bounds.Contains(mousePosition)
                            ? Color.DarkSlateBlue
                            : new Color(48, 58, 68);
                    uiRenderer.DrawButton(item.Label, bounds, background, item.IsEnabled ? Color.White : new Color(188, 188, 188), uiRenderer.UiSmallFont);
                }
            }
        }

        if (_showProfile && _profile != null)
        {
            var bounds = GetProfileBounds(viewport);
            var detailFont = uiRenderer.UiSmallFont;
            uiRenderer.DrawButton(string.Empty, bounds, new Color(24, 30, 38), Color.Transparent);
            uiRenderer.DrawTextInBounds(_profile.Title, new Rectangle(bounds.X + 12, bounds.Y + 10, bounds.Width - 96, 20), Color.White, uiRenderer.UiMediumFont);
            uiRenderer.DrawTextInBounds(_profile.Subtitle, new Rectangle(bounds.X + 12, bounds.Y + 38, bounds.Width - 24, 18), Color.Gold, uiRenderer.UiSmallFont);
            uiRenderer.DrawButton("Close", GetProfileCloseBounds(viewport), GetProfileCloseBounds(viewport).Contains(mousePosition) ? Color.DarkGray : Color.Gray, Color.White, uiRenderer.UiSmallFont);

            var detailY = bounds.Y + 68;
            var textWidth = bounds.Width - (ProfilePadding * 2);
            foreach (var line in _profile.DetailLines)
            {
                var wrappedLineCount = EstimateWrappedLineCount(line, textWidth, detailFont);
                var blockHeight = EstimateTextBlockHeight(wrappedLineCount, detailFont);
                uiRenderer.DrawWrappedTextInBounds(line, new Rectangle(bounds.X + ProfilePadding, detailY, textWidth, blockHeight), Color.White, detailFont, wrappedLineCount, WrappedLineSpacing);
                detailY += blockHeight + 4;
            }

            var summaryY = detailY + ProfileSectionSpacing;
            foreach (var line in _profile.SummaryLines)
            {
                var wrappedLineCount = EstimateWrappedLineCount(line, textWidth, detailFont);
                var blockHeight = EstimateTextBlockHeight(wrappedLineCount, detailFont);
                uiRenderer.DrawWrappedTextInBounds(line, new Rectangle(bounds.X + ProfilePadding, summaryY, textWidth, blockHeight), Color.White, detailFont, wrappedLineCount, WrappedLineSpacing);
                summaryY += blockHeight + 4;
            }
        }
    }

    private Rectangle GetMenuBounds(Point viewport)
    {
        var height = HeaderHeight + (_primaryActions.Count * RowHeight) + 8;
        var x = Math.Clamp(_anchor.X, 8, Math.Max(8, viewport.X - MenuWidth - 8));
        var y = Math.Clamp(_anchor.Y, 8, Math.Max(8, viewport.Y - height - 8));
        return new Rectangle(x, y, MenuWidth, height);
    }

    private Rectangle GetPrimaryItemBounds(Point viewport, int index)
    {
        var menuBounds = GetMenuBounds(viewport);
        return new Rectangle(menuBounds.X + 6, menuBounds.Y + HeaderHeight + 4 + (index * RowHeight), menuBounds.Width - 12, RowHeight - 2);
    }

    private Rectangle GetRosterSubmenuBounds(Point viewport)
    {
        var menuBounds = GetMenuBounds(viewport);
        var width = MenuWidth;
        var height = HeaderHeight + (_rosterActions.Count * RowHeight) + 8;
        var preferredX = menuBounds.Right + 6;
        var x = preferredX + width <= viewport.X - 8 ? preferredX : Math.Max(8, menuBounds.X - width - 6);
        var y = Math.Clamp(menuBounds.Y, 8, Math.Max(8, viewport.Y - height - 8));
        return new Rectangle(x, y, width, height);
    }

    private Rectangle GetRosterItemBounds(Point viewport, int index)
    {
        var submenuBounds = GetRosterSubmenuBounds(viewport);
        return new Rectangle(submenuBounds.X + 6, submenuBounds.Y + HeaderHeight + 4 + (index * RowHeight), submenuBounds.Width - 12, RowHeight - 2);
    }

    private Rectangle GetProfileBounds(Point viewport)
    {
        var width = Math.Min(ProfileMaxWidth, Math.Max(ProfileMinWidth, viewport.X - 48));
        var height = Math.Min(CalculateProfileHeight(width), Math.Max(ProfileMinHeight, viewport.Y - 48));
        return new Rectangle((viewport.X - width) / 2, (viewport.Y - height) / 2, width, height);
    }

    private Rectangle GetProfileCloseBounds(Point viewport)
    {
        var bounds = GetProfileBounds(viewport);
        return new Rectangle(bounds.Right - 90, bounds.Y + 10, 76, 24);
    }

    private int CalculateProfileHeight(int width)
    {
        var contentWidth = Math.Max(220, width - (ProfilePadding * 2));
        var height = 82;

        if (_profile != null)
        {
            foreach (var line in _profile.DetailLines)
            {
                height += EstimateTextBlockHeight(EstimateWrappedLineCount(line, contentWidth)) + 4;
            }

            height += ProfileSectionSpacing;

            foreach (var line in _profile.SummaryLines)
            {
                height += EstimateTextBlockHeight(EstimateWrappedLineCount(line, contentWidth)) + 4;
            }
        }

        return height + 12;
    }

    private static int EstimateWrappedLineCount(string text, int width, SpriteFont? font = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }

        if (font != null)
        {
            return Math.Max(1, WrapTextToWidth(text, font, Math.Max(1f, width - 4f)).Count);
        }

        var charsPerLine = Math.Max(20, width / 9);
        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)charsPerLine));
    }

    private static int EstimateTextBlockHeight(int lineCount, SpriteFont? font = null)
    {
        var visibleLines = Math.Max(1, lineCount);
        var lineHeight = font == null
            ? FallbackWrappedLineHeight
            : Math.Max(12, font.LineSpacing - 2);
        return (visibleLines * lineHeight) + ((visibleLines - 1) * WrappedLineSpacing) + 4;
    }

    private static List<string> WrapTextToWidth(string text, SpriteFont font, float maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (font.MeasureString(candidate).X <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            currentLine = word;
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines;
    }
}
