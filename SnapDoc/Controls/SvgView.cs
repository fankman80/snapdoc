using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Svg.Skia;
using System.Text;

namespace SnapDoc.Controls;

public partial class SvgView : SKCanvasView
{
    private readonly SKSvg _svg = new();

    public enum ItemAspect { Original, AspectFit }

    // Quelle: Pfad zur SVG in den Resources/Raw
    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(nameof(Source), typeof(string), typeof(SvgView), null,
            propertyChanged: (bindable, oldVal, newVal) => ((SvgView)bindable).LoadAndColorSvg());

    // Die Farbe, die ersetzt werden soll (Standard: Grau)
    public static readonly BindableProperty TargetHexColorProperty =
        BindableProperty.Create(nameof(TargetHexColor), typeof(string), typeof(SvgView), "#999999",
            propertyChanged: (bindable, oldVal, newVal) => ((SvgView)bindable).LoadAndColorSvg());

    // Die neue Farbe (TintColor)
    public static readonly BindableProperty TintColorProperty =
        BindableProperty.Create(nameof(TintColor), typeof(Color), typeof(SvgView), null,
            propertyChanged: (bindable, oldVal, newVal) => ((SvgView)bindable).LoadAndColorSvg());

    public static readonly BindableProperty AspectProperty =
        BindableProperty.Create(nameof(Aspect), typeof(ItemAspect), typeof(SvgView), ItemAspect.AspectFit,
            propertyChanged: (bindable, oldVal, newVal) => ((SvgView)bindable).InvalidateSurface());

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string TargetHexColor
    {
        get => (string)GetValue(TargetHexColorProperty);
        set => SetValue(TargetHexColorProperty, value);
    }

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public ItemAspect Aspect
    {
        get => (ItemAspect)GetValue(AspectProperty);
        set => SetValue(AspectProperty, value);
    }

    private async void LoadAndColorSvg()
    {
        if (string.IsNullOrEmpty(Source)) return;

        try
        {
            // 1. Datei aus den App-Resources (Raw) laden
            using var stream = await FileSystem.OpenAppPackageFileAsync(Source);
            using var reader = new StreamReader(stream);
            string svgText = await reader.ReadToEndAsync();

            // 2. Farbe ersetzen, falls TintColor gesetzt ist
            if (TintColor != null)
            {
                string newHex = TintColor.ToRgbaHex();
                svgText = svgText.Replace(TargetHexColor, newHex, StringComparison.OrdinalIgnoreCase);
            }

            // 3. In SKSvg laden
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(svgText));
            _svg.Load(ms);

            InvalidateSurface();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SvgView Error: {ex.Message}");
        }
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_svg?.Picture == null) return;

        float canvasWidth = e.Info.Width;
        float canvasHeight = e.Info.Height;
        float svgWidth = _svg.Picture.CullRect.Width;
        float svgHeight = _svg.Picture.CullRect.Height;

        canvas.Save();

        if (Aspect == ItemAspect.AspectFit)
        {
            float scale = Math.Min(canvasWidth / svgWidth, canvasHeight / svgHeight);
            canvas.Translate((canvasWidth - svgWidth * scale) / 2f, (canvasHeight - svgHeight * scale) / 2f);
            canvas.Scale(scale);
        }

        canvas.DrawPicture(_svg.Picture);
        canvas.Restore();
    }
}