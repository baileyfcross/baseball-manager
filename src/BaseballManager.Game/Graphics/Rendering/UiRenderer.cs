using Microsoft.Xna.Framework.Graphics;

namespace BaseballManager.Game.Graphics.Rendering;

public sealed class UiRenderer
{
    public UiRenderer(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice;
    }

    public GraphicsDevice GraphicsDevice { get; }
}
