using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace SnapDoc.Controls;

public partial class SkiaBackgroundView : SKCanvasView
{
    private SKBitmap? _bitmap;

    public static readonly BindableProperty ImagePathProperty = BindableProperty.Create(
        nameof(ImagePath), typeof(string), typeof(SkiaBackgroundView), null,
        propertyChanged: async (b, o, n) => await ((SkiaBackgroundView)b).LoadBitmapAsync((string)n));

    public string ImagePath { get => (string)GetValue(ImagePathProperty); set => SetValue(ImagePathProperty, value); }

    private async Task LoadBitmapAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        _bitmap = await Task.Run(() => SKBitmap.Decode(path));
        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_bitmap == null)
            return;

        canvas.DrawBitmap(_bitmap, new SKRect(0, 0, _bitmap.Width, _bitmap.Height));
    }
}