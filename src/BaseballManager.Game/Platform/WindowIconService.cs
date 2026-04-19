using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;

namespace BaseballManager.Game.Platform;

internal static class WindowIconService
{
    public static void TryApplyWindowIcon(GraphicsDevice graphicsDevice, GameWindow window, string assetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (graphicsDevice == null || window == null || window.Handle == IntPtr.Zero || string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        try
        {
            using var stream = TitleContainer.OpenStream(assetPath);
            using var texture = Texture2D.FromStream(graphicsDevice, stream);
            ApplySdlWindowIcon(window.Handle, texture);
        }
        catch
        {
            // Icon loading should never block game startup.
        }
    }

    private static void ApplySdlWindowIcon(IntPtr windowHandle, Texture2D texture)
    {
        var colors = new Color[texture.Width * texture.Height];
        texture.GetData(colors);

        var pixels = new byte[colors.Length * 4];
        for (var index = 0; index < colors.Length; index++)
        {
            var offset = index * 4;
            pixels[offset] = colors[index].R;
            pixels[offset + 1] = colors[index].G;
            pixels[offset + 2] = colors[index].B;
            pixels[offset + 3] = colors[index].A;
        }

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var surface = SDL_CreateRGBSurfaceFrom(
                handle.AddrOfPinnedObject(),
                texture.Width,
                texture.Height,
                32,
                texture.Width * 4,
                0x000000ff,
                0x0000ff00,
                0x00ff0000,
                unchecked((int)0xff000000));

            if (surface == IntPtr.Zero)
            {
                return;
            }

            try
            {
                SDL_SetWindowIcon(windowHandle, surface);
            }
            finally
            {
                SDL_FreeSurface(surface);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_CreateRGBSurfaceFrom(
        IntPtr pixels,
        int width,
        int height,
        int depth,
        int pitch,
        int rMask,
        int gMask,
        int bMask,
        int aMask);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_SetWindowIcon(IntPtr window, IntPtr icon);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_FreeSurface(IntPtr surface);
}
