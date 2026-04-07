using BaseballManager.Game.Screens.LiveMatch;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.Graphics.Rendering.LiveMatch;

public sealed class FieldRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;

    public FieldRenderer(GraphicsDevice graphicsDevice)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(LiveMatchViewModel viewModel, Viewport viewport)
    {
        var fieldBounds = new Rectangle(40, 90, Math.Max(700, viewport.Width - 390), Math.Max(500, viewport.Height - 150));
        var homePlate = new Vector2(fieldBounds.Center.X, fieldBounds.Bottom - 78f);
        var baseSpacing = MathF.Min(fieldBounds.Width * 0.12f, fieldBounds.Height * 0.16f);
        var firstBase = new Vector2(homePlate.X + baseSpacing, homePlate.Y - baseSpacing);
        var secondBase = new Vector2(homePlate.X, homePlate.Y - (baseSpacing * 2f));
        var thirdBase = new Vector2(homePlate.X - baseSpacing, homePlate.Y - baseSpacing);
        var infieldCenter = Vector2.Lerp(homePlate, secondBase, 0.5f);
        var pitcher = Vector2.Lerp(homePlate, secondBase, 0.56f);
        var catcher = homePlate + new Vector2(0f, 20f);
        var batter = homePlate + new Vector2(-26f, 10f);
        var leftFoulPole = new Vector2(fieldBounds.X + 28f, fieldBounds.Y + 26f);
        var rightFoulPole = new Vector2(fieldBounds.Right - 28f, fieldBounds.Y + 26f);
        var fencePeak = new Vector2(fieldBounds.Center.X, fieldBounds.Y + 6f);

        var fielderPositions = new Dictionary<string, Vector2>
        {
            ["P"] = pitcher,
            ["C"] = catcher,
            ["1B"] = Vector2.Lerp(firstBase, secondBase, 0.30f) + new Vector2(34f, 10f),
            ["2B"] = Vector2.Lerp(firstBase, secondBase, 0.58f) + new Vector2(18f, -8f),
            ["SS"] = Vector2.Lerp(thirdBase, secondBase, 0.58f) + new Vector2(-18f, -8f),
            ["3B"] = Vector2.Lerp(thirdBase, secondBase, 0.30f) + new Vector2(-34f, 10f),
            ["LF"] = new Vector2(homePlate.X - (baseSpacing * 2.8f), homePlate.Y - (baseSpacing * 2.7f)),
            ["CF"] = new Vector2(homePlate.X, homePlate.Y - (baseSpacing * 3.25f)),
            ["RF"] = new Vector2(homePlate.X + (baseSpacing * 2.8f), homePlate.Y - (baseSpacing * 2.7f))
        };

        _spriteBatch.Begin();

        DrawFilledRectangle(fieldBounds, new Color(28, 110, 62));
        DrawFilledCircle(infieldCenter, (int)(baseSpacing * 0.95f), new Color(170, 132, 88));
        DrawFilledCircle(pitcher, 24, new Color(181, 140, 95));
        DrawFilledCircle(homePlate + new Vector2(0f, 2f), 26, new Color(170, 132, 88));

        DrawLine(homePlate, firstBase, Color.White, 3);
        DrawLine(firstBase, secondBase, Color.White, 3);
        DrawLine(secondBase, thirdBase, Color.White, 3);
        DrawLine(thirdBase, homePlate, Color.White, 3);

        DrawLine(homePlate, leftFoulPole, Color.White, 3);
        DrawLine(homePlate, rightFoulPole, Color.White, 3);
        DrawLine(leftFoulPole, fencePeak, new Color(220, 235, 220, 180), 2);
        DrawLine(fencePeak, rightFoulPole, new Color(220, 235, 220, 180), 2);

        DrawBase(homePlate + new Vector2(0f, 2f), 10, Color.White, 0f);
        DrawBase(firstBase, 12, Color.White, MathF.PI / 4f);
        DrawBase(secondBase, 12, Color.White, MathF.PI / 4f);
        DrawBase(thirdBase, 12, Color.White, MathF.PI / 4f);

        foreach (var pair in fielderPositions)
        {
            var isHighlighted = string.Equals(pair.Key, viewModel.HighlightedFielder, StringComparison.OrdinalIgnoreCase);
            var radius = isHighlighted ? 11 : 8;
            var color = isHighlighted ? Color.Gold : new Color(64, 123, 214);
            DrawFilledCircle(pair.Value, radius, color);
        }

        DrawFilledCircle(batter, 9, new Color(221, 128, 55));
        DrawFilledCircle(catcher, 8, new Color(84, 84, 84));

        if (viewModel.RunnerOnFirst)
        {
            DrawFilledCircle(firstBase + new Vector2(18f, -6f), 8, Color.Gold);
        }

        if (viewModel.RunnerOnSecond)
        {
            DrawFilledCircle(secondBase + new Vector2(0f, -18f), 8, Color.Gold);
        }

        if (viewModel.RunnerOnThird)
        {
            DrawFilledCircle(thirdBase + new Vector2(-18f, -6f), 8, Color.Gold);
        }

        if (viewModel.BallVisible)
        {
            var alpha = (byte)Math.Clamp((int)(viewModel.BallHighlightAlpha * 255f), 60, 255);
            var ballPosition = new Vector2(
                fieldBounds.X + (fieldBounds.Width * Math.Clamp(viewModel.BallXNormalized, 0.05f, 0.95f)),
                fieldBounds.Y + (fieldBounds.Height * Math.Clamp(viewModel.BallYNormalized, 0.05f, 0.95f)));
            DrawFilledCircle(ballPosition, 6, new Color((byte)255, (byte)250, (byte)250, alpha));
        }

        _spriteBatch.End();
    }

    private void DrawFilledRectangle(Rectangle rectangle, Color color)
    {
        _spriteBatch.Draw(_pixel, rectangle, color);
    }

    private void DrawBase(Vector2 center, int size, Color color, float rotation)
    {
        _spriteBatch.Draw(
            _pixel,
            center,
            null,
            color,
            rotation,
            new Vector2(0.5f, 0.5f),
            new Vector2(size, size),
            SpriteEffects.None,
            0f);
    }

    private void DrawFilledCircle(Vector2 center, int radius, Color color)
    {
        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) <= radius * radius)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle((int)center.X + x, (int)center.Y + y, 1, 1), color);
                }
            }
        }
    }

    private void DrawLine(Vector2 start, Vector2 end, Color color, int thickness)
    {
        var edge = end - start;
        var angle = MathF.Atan2(edge.Y, edge.X);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
            null,
            color,
            angle,
            Vector2.Zero,
            SpriteEffects.None,
            0f);
    }
}
