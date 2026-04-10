using BaseballManager.Game.Screens.LiveMatch;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.Graphics.Rendering.LiveMatch;

public sealed class FieldRenderer
{
    private const float BasePathFeet = 90f;
    private const float MoundDistanceFeet = 60.5f;
    private const float LeftFieldLineFeet = 330f;
    private const float LeftCenterFeet = 370f;
    private const float CenterFieldFeet = 400f;
    private const float RightCenterFeet = 370f;
    private const float RightFieldLineFeet = 330f;
    private const float WarningTrackDepthFeet = 14f;

    private static readonly (float AngleDegrees, float DistanceFeet)[] FenceProfile =
    [
        (-45f, LeftFieldLineFeet),
        (-30f, 355f),
        (-18f, LeftCenterFeet),
        (-8f, 388f),
        (0f, CenterFieldFeet),
        (8f, 388f),
        (18f, RightCenterFeet),
        (30f, 355f),
        (45f, RightFieldLineFeet)
    ];

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
        var sidebarWidth = Math.Clamp((int)(viewport.Width * 0.31f), 300, 420);
        var fieldBounds = new Rectangle(
            24,
            74,
            Math.Max(620, viewport.Width - sidebarWidth - 48),
            Math.Max(430, viewport.Height - 116));

        var homePlate = new Vector2(fieldBounds.Center.X, fieldBounds.Bottom - Math.Clamp(fieldBounds.Height * 0.07f, 30f, 42f));
        var foulHalfWidthFeet = LeftFieldLineFeet / MathF.Sqrt(2f);
        var requiredWidthFeet = (foulHalfWidthFeet * 2f) + 80f;
        var requiredHeightFeet = CenterFieldFeet + 82f;
        var scale = MathF.Min(
            (fieldBounds.Width - 36f) / requiredWidthFeet,
            (fieldBounds.Height - 24f) / requiredHeightFeet);

        var baseOffset = BasePathFeet / MathF.Sqrt(2f);
        var firstBase = ToScreenPoint(homePlate, scale, baseOffset, baseOffset);
        var secondBase = ToScreenPoint(homePlate, scale, 0f, BasePathFeet * MathF.Sqrt(2f));
        var thirdBase = ToScreenPoint(homePlate, scale, -baseOffset, baseOffset);
        var mound = ToScreenPoint(homePlate, scale, 0f, MoundDistanceFeet);
        var infieldCenter = ToScreenPoint(homePlate, scale, 0f, 74f);
        var catcher = homePlate + new Vector2(0f, Math.Max(12f, scale * 9f));
        var batter = homePlate + new Vector2(-Math.Max(16f, scale * 10f), Math.Max(8f, scale * 4f));
        var leftPole = ToScreenPoint(homePlate, scale, -foulHalfWidthFeet, foulHalfWidthFeet);
        var rightPole = ToScreenPoint(homePlate, scale, foulHalfWidthFeet, foulHalfWidthFeet);
        var fencePoints = BuildFencePoints(homePlate, scale, 0f);
        var warningTrackPoints = BuildFencePoints(homePlate, scale, -WarningTrackDepthFeet);

        var fielderPositions = new Dictionary<string, Vector2>
        {
            ["P"] = mound,
            ["C"] = catcher,
            ["1B"] = ToScreenPoint(homePlate, scale, 84f, 96f),
            ["2B"] = ToScreenPoint(homePlate, scale, 42f, 150f),
            ["SS"] = ToScreenPoint(homePlate, scale, -42f, 150f),
            ["3B"] = ToScreenPoint(homePlate, scale, -84f, 96f),
            ["LF"] = ToScreenPoint(homePlate, scale, -156f, 248f),
            ["CF"] = ToScreenPoint(homePlate, scale, 0f, 306f),
            ["RF"] = ToScreenPoint(homePlate, scale, 156f, 248f)
        };

        _spriteBatch.Begin();

        DrawFilledRectangle(fieldBounds, new Color(30, 104, 58));
        DrawMowingPattern(fieldBounds);
        DrawArc(homePlate, scale, 150f, -45f, 45f, new Color(42, 122, 74, 55), Math.Max(3, (int)(scale * 3f)));
        DrawArc(homePlate, scale, 240f, -45f, 45f, new Color(22, 88, 50, 45), Math.Max(3, (int)(scale * 3f)));
        DrawArc(homePlate, scale, 325f, -45f, 45f, new Color(42, 122, 74, 55), Math.Max(3, (int)(scale * 3f)));

        DrawPolyline(fencePoints, new Color(182, 140, 92), Math.Max(9, (int)(scale * 8f)));
        DrawPolyline(warningTrackPoints, new Color(162, 118, 78), Math.Max(7, (int)(scale * 6f)));
        DrawPolyline(fencePoints, new Color(44, 70, 56), Math.Max(4, (int)(scale * 3.2f)));
        DrawPolyline(fencePoints.Select(point => point + new Vector2(0f, -2f)).ToList(), new Color(115, 150, 130, 180), 2);

        DrawLine(homePlate, leftPole, new Color(205, 175, 122), Math.Max(8, (int)(scale * 6f)));
        DrawLine(homePlate, rightPole, new Color(205, 175, 122), Math.Max(8, (int)(scale * 6f)));
        DrawLine(homePlate, leftPole, Color.White, 3);
        DrawLine(homePlate, rightPole, Color.White, 3);

        var dirtColor = new Color(174, 134, 91);
        var darkerDirt = new Color(160, 120, 80);
        var basePathThickness = Math.Max(12, (int)(scale * 12f));
        DrawLine(homePlate, firstBase, dirtColor, basePathThickness);
        DrawLine(firstBase, secondBase, dirtColor, basePathThickness);
        DrawLine(secondBase, thirdBase, dirtColor, basePathThickness);
        DrawLine(thirdBase, homePlate, dirtColor, basePathThickness);
        DrawFilledCircle(infieldCenter, Math.Max(28, (int)(scale * 58f)), dirtColor);
        DrawFilledCircle(mound, Math.Max(10, (int)(scale * 10f)), darkerDirt);
        DrawFilledCircle(homePlate + new Vector2(0f, 2f), Math.Max(14, (int)(scale * 18f)), dirtColor);

        DrawLine(homePlate, firstBase, Color.White, 3);
        DrawLine(firstBase, secondBase, Color.White, 3);
        DrawLine(secondBase, thirdBase, Color.White, 3);
        DrawLine(thirdBase, homePlate, Color.White, 3);

        DrawBase(homePlate + new Vector2(0f, 2f), Math.Max(9, (int)(scale * 8f)), Color.White, 0f);
        DrawBase(firstBase, Math.Max(10, (int)(scale * 9f)), Color.White, MathF.PI / 4f);
        DrawBase(secondBase, Math.Max(10, (int)(scale * 9f)), Color.White, MathF.PI / 4f);
        DrawBase(thirdBase, Math.Max(10, (int)(scale * 9f)), Color.White, MathF.PI / 4f);

        foreach (var pair in fielderPositions)
        {
            var isHighlighted = string.Equals(pair.Key, viewModel.HighlightedFielder, StringComparison.OrdinalIgnoreCase);
            var radius = isHighlighted ? Math.Max(11, (int)(scale * 8f)) : Math.Max(8, (int)(scale * 6f));
            var color = isHighlighted ? Color.Gold : new Color(64, 123, 214);
            DrawPlayerMarker(pair.Value, radius, color);
        }

        DrawPlayerMarker(batter, Math.Max(8, (int)(scale * 6f)), new Color(221, 128, 55));
        DrawPlayerMarker(catcher, Math.Max(8, (int)(scale * 5.5f)), new Color(84, 84, 84));

        if (viewModel.RunnerOnFirst)
        {
            DrawPlayerMarker(firstBase + new Vector2(Math.Max(12f, scale * 10f), -Math.Max(5f, scale * 4f)), Math.Max(7, (int)(scale * 5.5f)), Color.Gold);
        }

        if (viewModel.RunnerOnSecond)
        {
            DrawPlayerMarker(secondBase + new Vector2(0f, -Math.Max(12f, scale * 10f)), Math.Max(7, (int)(scale * 5.5f)), Color.Gold);
        }

        if (viewModel.RunnerOnThird)
        {
            DrawPlayerMarker(thirdBase + new Vector2(-Math.Max(12f, scale * 10f), -Math.Max(5f, scale * 4f)), Math.Max(7, (int)(scale * 5.5f)), Color.Gold);
        }

        if (viewModel.BallVisible)
        {
            var alpha = (byte)Math.Clamp((int)(viewModel.BallHighlightAlpha * 255f), 60, 255);
            var ballPosition = new Vector2(
                fieldBounds.X + (fieldBounds.Width * Math.Clamp(viewModel.BallXNormalized, 0.08f, 0.92f)),
                fieldBounds.Y + (fieldBounds.Height * Math.Clamp(viewModel.BallYNormalized, 0.06f, 0.90f)));
            DrawFilledCircle(ballPosition + new Vector2(1f, 2f), 6, new Color(0, 0, 0, 90));
            DrawFilledCircle(ballPosition, 5, new Color((byte)255, (byte)250, (byte)250, alpha));
        }

        _spriteBatch.End();
    }

    private static Vector2 ToScreenPoint(Vector2 homePlate, float scale, float xFeet, float yFeet)
    {
        return new Vector2(homePlate.X + (xFeet * scale), homePlate.Y - (yFeet * scale));
    }

    private static List<Vector2> BuildFencePoints(Vector2 homePlate, float scale, float offsetFeet)
    {
        return FenceProfile
            .Select(sample =>
            {
                var distance = Math.Max(250f, sample.DistanceFeet + offsetFeet);
                var angleRadians = MathHelper.ToRadians(sample.AngleDegrees);
                var xFeet = MathF.Sin(angleRadians) * distance;
                var yFeet = MathF.Cos(angleRadians) * distance;
                return ToScreenPoint(homePlate, scale, xFeet, yFeet);
            })
            .ToList();
    }

    private void DrawMowingPattern(Rectangle fieldBounds)
    {
        const int stripeCount = 8;
        var stripeWidth = Math.Max(12, fieldBounds.Width / stripeCount);
        for (var index = 0; index < stripeCount; index++)
        {
            var stripeBounds = new Rectangle(fieldBounds.X + (index * stripeWidth), fieldBounds.Y, stripeWidth + 1, fieldBounds.Height);
            var stripeColor = index % 2 == 0
                ? new Color(42, 124, 72, 24)
                : new Color(20, 86, 48, 18);
            DrawFilledRectangle(stripeBounds, stripeColor);
        }
    }

    private void DrawPlayerMarker(Vector2 center, int radius, Color color)
    {
        DrawFilledCircle(center + new Vector2(1f, 2f), radius, new Color(0, 0, 0, 85));
        DrawFilledCircle(center, radius, color);
    }

    private void DrawPolyline(IReadOnlyList<Vector2> points, Color color, int thickness)
    {
        for (var index = 0; index < points.Count - 1; index++)
        {
            DrawLine(points[index], points[index + 1], color, thickness);
        }
    }

    private void DrawArc(Vector2 homePlate, float scale, float radiusFeet, float startAngleDegrees, float endAngleDegrees, Color color, int thickness)
    {
        var points = new List<Vector2>();
        const int segments = 22;
        for (var index = 0; index <= segments; index++)
        {
            var t = index / (float)segments;
            var angleRadians = MathHelper.ToRadians(MathHelper.Lerp(startAngleDegrees, endAngleDegrees, t));
            var xFeet = MathF.Sin(angleRadians) * radiusFeet;
            var yFeet = MathF.Cos(angleRadians) * radiusFeet;
            points.Add(ToScreenPoint(homePlate, scale, xFeet, yFeet));
        }

        DrawPolyline(points, color, thickness);
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
