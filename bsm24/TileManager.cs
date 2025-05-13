using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace bsm24;

public class TileManager
{
    private readonly string imagePath;
    private readonly int tileSize;
    private readonly Dictionary<(int tileX, int tileY), Image<L8>> tileCache = new();

    public int Width { get; }
    public int Height { get; }

    public TileManager(string imagePath, int tileSize = 512)
    {
        this.imagePath = imagePath;
        this.tileSize = tileSize;

        var imageInfo = Image.Identify(imagePath);
        if (imageInfo == null)
            throw new InvalidOperationException("Bild konnte nicht identifiziert werden.");

        Width = imageInfo.Width;
        Height = imageInfo.Height;
    }

    public L8 GetPixel(int x, int y)
    {
        int tileX = x / tileSize;
        int tileY = y / tileSize;
        int localX = x % tileSize;
        int localY = y % tileSize;

        var key = (tileX, tileY);
        if (!tileCache.TryGetValue(key, out var tile))
        {
            tile = LoadTile(tileX, tileY);
            tileCache[key] = tile;
        }

        if (localX < 0 || localY < 0 || localX >= tile.Width || localY >= tile.Height)
            return new L8(0);

        return tile[localX, localY];
    }

    private Image<L8> LoadTile(int tileX, int tileY)
    {
        using var fullImage = Image.Load<L8>(imagePath);

        int startX = tileX * tileSize;
        int startY = tileY * tileSize;
        int width = Math.Min(tileSize, fullImage.Width - startX);
        int height = Math.Min(tileSize, fullImage.Height - startY);

        return fullImage.Clone(ctx => ctx.Crop(new Rectangle(startX, startY, width, height)));
    }

    public Image<L8> LoadBaseImage()
    {
        return Image.Load<L8>(imagePath);
    }
}