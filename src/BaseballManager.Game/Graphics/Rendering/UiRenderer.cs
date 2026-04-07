using System.Globalization;
using System.Text;
using BaseballManager.Game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.Graphics.Rendering;

public sealed class UiRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontCatalog _fontCatalog = new();
    private ContentManager? _contentManager;
    private Texture2D? _pixelTexture;
    private SpriteFont? _uiSmallFont;
    private SpriteFont? _uiMediumFont;
    private SpriteFont? _scoreboardFont;
    private SpriteBatch? _spriteBatch;

    public UiRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public GraphicsDevice GraphicsDevice => _graphicsDevice;

    public Viewport Viewport => _graphicsDevice.Viewport;

    public SpriteFont? UiSmallFont => _uiSmallFont;

    public SpriteFont? UiMediumFont => _uiMediumFont;

    public SpriteFont? ScoreboardFont => _scoreboardFont;

    public void LoadContent(ContentManager contentManager)
    {
        _contentManager = contentManager;
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        try
        {
            _uiSmallFont = contentManager.Load<SpriteFont>(_fontCatalog.UiSmall);
            _uiMediumFont = contentManager.Load<SpriteFont>(_fontCatalog.UiMedium);
            _scoreboardFont = contentManager.Load<SpriteFont>(_fontCatalog.Scoreboard);
        }
        catch (ContentLoadException ex)
        {
            Console.WriteLine($"Warning: Could not load fonts. {ex.Message}");
            Console.WriteLine("Fonts will not render until Content.mgcb is compiled.");
        }
    }

    public void DrawText(string text, Vector2 position, Color color, SpriteFont? font = null)
    {
        if (_spriteBatch == null)
            return;

        var safeText = SanitizeText(text);
        var selectedFont = font ?? _uiSmallFont;
        if (selectedFont != null)
        {
            var batchStarted = false;
            try
            {
                _spriteBatch.Begin();
                batchStarted = true;
                _spriteBatch.DrawString(selectedFont, safeText, position, color);
            }
            finally
            {
                if (batchStarted)
                {
                    _spriteBatch.End();
                }
            }
        }
        else
        {
            DrawSimpleText(safeText, position, color);
        }
    }

    public void DrawTextInBounds(string text, Rectangle bounds, Color color, SpriteFont? font = null, bool centerHorizontally = false)
    {
        if (_spriteBatch == null)
            return;

        var safeText = SanitizeText(text);
        if (string.IsNullOrEmpty(safeText))
        {
            return;
        }

        var selectedFont = font ?? _uiSmallFont;
        if (selectedFont != null)
        {
            var batchStarted = false;
            try
            {
                _spriteBatch.Begin();
                batchStarted = true;

                var textSize = selectedFont.MeasureString(safeText);
                var maxTextWidth = Math.Max(1f, bounds.Width - 4f);
                var maxTextHeight = Math.Max(1f, bounds.Height - 2f);
                var scaleX = textSize.X <= 0f ? 1f : Math.Min(1f, maxTextWidth / textSize.X);
                var scaleY = textSize.Y <= 0f ? 1f : Math.Min(1f, maxTextHeight / textSize.Y);
                var scale = Math.Min(scaleX, scaleY);
                var scaledSize = textSize * scale;
                var textX = centerHorizontally
                    ? bounds.X + (bounds.Width - scaledSize.X) / 2f
                    : bounds.X + 2f;
                var textY = bounds.Y + (bounds.Height - scaledSize.Y) / 2f;
                _spriteBatch.DrawString(selectedFont, safeText, new Vector2(textX, textY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            finally
            {
                if (batchStarted)
                {
                    _spriteBatch.End();
                }
            }
        }
        else
        {
            DrawSimpleText(safeText, new Vector2(bounds.X + 2, bounds.Y + 2), color);
        }
    }

    public void DrawWrappedTextInBounds(string text, Rectangle bounds, Color color, SpriteFont? font = null, int maxLines = int.MaxValue, int lineSpacing = 2)
    {
        var safeText = SanitizeText(text);
        if (string.IsNullOrWhiteSpace(safeText))
        {
            return;
        }

        var selectedFont = font ?? _uiSmallFont;
        if (selectedFont == null)
        {
            DrawTextInBounds(safeText, bounds, color, font);
            return;
        }

        var wrappedLines = WrapTextToWidth(safeText, selectedFont, Math.Max(1f, bounds.Width - 4f));
        var lineHeight = Math.Max(12, selectedFont.LineSpacing - 2);
        var drawnLines = 0;

        foreach (var line in wrappedLines)
        {
            if (drawnLines >= maxLines)
            {
                break;
            }

            var lineBounds = new Rectangle(bounds.X, bounds.Y + drawnLines * (lineHeight + lineSpacing), bounds.Width, lineHeight);
            if (lineBounds.Bottom > bounds.Bottom)
            {
                break;
            }

            DrawTextInBounds(line, lineBounds, color, selectedFont);
            drawnLines++;
        }
    }

    public void DrawButton(string label, Rectangle bounds, Color backgroundColor, Color textColor, SpriteFont? font = null)
    {
        if (_spriteBatch == null || _pixelTexture == null)
            return;

        var safeLabel = SanitizeText(label);
        var selectedFont = font ?? _uiSmallFont;
        var batchStarted = false;

        try
        {
            _spriteBatch.Begin();
            batchStarted = true;

            _spriteBatch.Draw(_pixelTexture, bounds, backgroundColor);
            DrawRectangleOutline(bounds, Color.Black);

            if (!string.IsNullOrEmpty(safeLabel))
            {
                if (selectedFont != null)
                {
                    var textSize = selectedFont.MeasureString(safeLabel);
                    var maxTextWidth = Math.Max(1f, bounds.Width - 20f);
                    var maxTextHeight = Math.Max(1f, bounds.Height - 10f);
                    var scaleX = textSize.X <= 0f ? 1f : Math.Min(1f, maxTextWidth / textSize.X);
                    var scaleY = textSize.Y <= 0f ? 1f : Math.Min(1f, maxTextHeight / textSize.Y);
                    var scale = Math.Min(scaleX, scaleY);
                    var scaledSize = textSize * scale;
                    var textPosition = new Vector2(
                        bounds.X + (bounds.Width - scaledSize.X) / 2f,
                        bounds.Y + (bounds.Height - scaledSize.Y) / 2f);
                    _spriteBatch.DrawString(selectedFont, safeLabel, textPosition, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    DrawSimpleButtonText(safeLabel, bounds, textColor);
                }
            }
        }
        finally
        {
            if (batchStarted)
            {
                _spriteBatch.End();
            }
        }
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

    private void DrawSimpleText(string text, Vector2 position, Color color)
    {
        if (_spriteBatch == null || _pixelTexture == null)
            return;

        var charWidth = 8f;
        var charHeight = 12f;
        var batchStarted = false;

        try
        {
            _spriteBatch.Begin();
            batchStarted = true;
            for (int i = 0; i < text.Length; i++)
            {
                var charRect = new Rectangle(
                    (int)(position.X + i * charWidth),
                    (int)position.Y,
                    (int)charWidth,
                    (int)charHeight);

                _spriteBatch.Draw(_pixelTexture, charRect, color);
            }
        }
        finally
        {
            if (batchStarted)
            {
                _spriteBatch.End();
            }
        }
    }

    private void DrawSimpleButtonText(string text, Rectangle bounds, Color color)
    {
        if (_spriteBatch == null || _pixelTexture == null)
            return;

        var charWidth = 7f;
        var totalWidth = text.Length * charWidth;
        var startX = bounds.X + (bounds.Width - totalWidth) / 2;
        var startY = bounds.Y + (bounds.Height - 10) / 2;

        for (int i = 0; i < text.Length; i++)
        {
            var charRect = new Rectangle(
                (int)(startX + i * charWidth),
                (int)startY,
                (int)charWidth,
                10);

            _spriteBatch.Draw(_pixelTexture, charRect, color);
        }
    }

    private void DrawRectangleOutline(Rectangle rect, Color color)
    {
        if (_spriteBatch == null || _pixelTexture == null)
            return;

        _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, 2), color); // Top
        _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, 2, rect.Height), color); // Left
        _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y + rect.Height - 2, rect.Width, 2), color); // Bottom
        _spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X + rect.Width - 2, rect.Y, 2, rect.Height), color); // Right
    }

    private static string SanitizeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Replace('•', '-')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('’', '\'')
            .Replace('‘', '\'')
            .Replace('“', '"')
            .Replace('”', '"')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character is >= ' ' and <= '~' ? character : '?');
        }

        return builder.ToString();
    }
}
