using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.Graphics.Rendering;

public sealed class UiRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private ContentManager? _contentManager;
    private SpriteFont? _uiSmallFont;
    private SpriteFont? _uiMediumFont;
    private SpriteBatch? _spriteBatch;

    public UiRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public Viewport Viewport => _graphicsDevice.Viewport;

    public void LoadContent(ContentManager contentManager)
    {
        _contentManager = contentManager;
        _spriteBatch = new SpriteBatch(_graphicsDevice);

        try
        {
            _uiSmallFont = contentManager.Load<SpriteFont>("Fonts/UiSmall");
            _uiMediumFont = contentManager.Load<SpriteFont>("Fonts/UiMedium");
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

        if (_uiSmallFont != null)
        {
            _spriteBatch.Begin();
            _spriteBatch.DrawString(_uiSmallFont, text, position, color);
            _spriteBatch.End();
        }
        else
        {
            DrawSimpleText(text, position, color);
        }
    }

    public void DrawButton(string label, Rectangle bounds, Color backgroundColor, Color textColor, SpriteFont? font = null)
    {
        if (_spriteBatch == null)
            return;

        _spriteBatch.Begin();

        // Draw button background
        var bgTexture = new Texture2D(_graphicsDevice, 1, 1);
        bgTexture.SetData(new[] { Color.White });
        _spriteBatch.Draw(bgTexture, bounds, backgroundColor);

        // Draw button border
        DrawRectangleOutline(bounds, Color.Black);

        // Draw button text
        if (_uiSmallFont != null)
        {
            var textSize = _uiSmallFont.MeasureString(label);
            var textPosition = new Vector2(
                bounds.X + (bounds.Width - textSize.X) / 2,
                bounds.Y + (bounds.Height - textSize.Y) / 2);
            _spriteBatch.DrawString(_uiSmallFont, label, textPosition, textColor);
        }
        else
        {
            // Fallback: draw simple text in button
            DrawSimpleButtonText(label, bounds, textColor);
        }

        _spriteBatch.End();
    }

    private void DrawSimpleText(string text, Vector2 position, Color color)
    {
        if (_spriteBatch == null)
            return;

        // Very simple placeholder text rendering
        var charWidth = 8f;
        var charHeight = 12f;

        var pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });

        _spriteBatch.Begin();
        for (int i = 0; i < text.Length; i++)
        {
            var charRect = new Rectangle(
                (int)(position.X + i * charWidth),
                (int)position.Y,
                (int)charWidth,
                (int)charHeight);

            _spriteBatch.Draw(pixelTexture, charRect, color);
        }
        _spriteBatch.End();
    }

    private void DrawSimpleButtonText(string text, Rectangle bounds, Color color)
    {
        if (_spriteBatch == null)
            return;

        var charWidth = 7f;
        var totalWidth = text.Length * charWidth;
        var startX = bounds.X + (bounds.Width - totalWidth) / 2;
        var startY = bounds.Y + (bounds.Height - 10) / 2;

        var pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });

        for (int i = 0; i < text.Length; i++)
        {
            var charRect = new Rectangle(
                (int)(startX + i * charWidth),
                (int)startY,
                (int)charWidth,
                10);

            _spriteBatch.Draw(pixelTexture, charRect, color);
        }
    }

    private void DrawRectangleOutline(Rectangle rect, Color color)
    {
        if (_spriteBatch == null)
            return;

        var pixel = new Texture2D(_graphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });

        _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color); // Top
        _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color); // Left
        _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - 2, rect.Width, 2), color); // Bottom
        _spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - 2, rect.Y, 2, rect.Height), color); // Right
    }
}
