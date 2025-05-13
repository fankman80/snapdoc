using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace bsm24;

public class DynamicOverlayMask
{
    private Image<Rgba32>? overlay;
    public int OffsetX { get; private set; }
    public int OffsetY { get; private set; }

    public void EnsureContains(int x, int y)
    {
        if (overlay == null)
        {
            overlay = new Image<Rgba32>(256, 256);
            OffsetX = x - 128;
            OffsetY = y - 128;
            return;
        }

        int localX = x - OffsetX;
        int localY = y - OffsetY;

        if (localX >= 0 && localX < overlay.Width &&
            localY >= 0 && localY < overlay.Height)
            return;

        int newMinX = Math.Min(OffsetX, x - 128);
        int newMinY = Math.Min(OffsetY, y - 128);
        int newMaxX = Math.Max(OffsetX + overlay.Width, x + 128);
        int newMaxY = Math.Max(OffsetY + overlay.Height, y + 128);

        int newWidth = newMaxX - newMinX;
        int newHeight = newMaxY - newMinY;

        var newOverlay = new Image<Rgba32>(newWidth, newHeight);
        newOverlay.Mutate(ctx =>
        {
            ctx.DrawImage(overlay, new SixLabors.ImageSharp.Point(OffsetX - newMinX, OffsetY - newMinY), 1f);
        });

        overlay.Dispose();
        overlay = newOverlay;
        OffsetX = newMinX;
        OffsetY = newMinY;
    }

    public void Set(int x, int y, Rgba32 color)
    {
        EnsureContains(x, y);
        int localX = x - OffsetX;
        int localY = y - OffsetY;
        overlay![localX, localY] = color;
    }

    public Image<Rgba32>? GetImage() => overlay;
}
