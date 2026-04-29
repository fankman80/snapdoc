using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace SnapDoc.Controls;

using SkiaSharp.Views.Maui.Controls;

// WICHTIG: Nutze Microsoft.Maui.Graphics für die Property
using Point = Microsoft.Maui.Graphics.Point;

public partial class TooltipOverlay : SKCanvasView
{
    // Neue Property für das Ziel-Element
    public static readonly BindableProperty AnchorElementProperty =
        BindableProperty.Create(nameof(AnchorElement), typeof(VisualElement), typeof(TooltipOverlay), null,
            propertyChanged: OnAnchorElementChanged);

    public VisualElement AnchorElement
    {
        get => (VisualElement)GetValue(AnchorElementProperty);
        set => SetValue(AnchorElementProperty, value);
    }

    private static void OnAnchorElementChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (TooltipOverlay)bindable;
        if (newValue is VisualElement element)
        {
            // Sobald das Element fertig gelayoutet ist, Position berechnen
            element.SizeChanged += (s, e) => control.UpdatePositionFromAnchor();
            control.UpdatePositionFromAnchor();
        }
    }

    private Point GetAbsolutePosition(VisualElement element)
    {
        if (element == null) return new Point(0, 0);

        // Diese Methode gibt die Position relativ zum Fenster zurück
        // Funktioniert in MAUI meist am besten über diese Hilfskonstruktion:
        var result = new Point(0, 0);
        var parent = element;

        while (parent != null)
        {
            result = result.Offset(parent.X, parent.Y);
            // Shell-Besonderheit: Toolbar-Elemente haben oft keinen Parent mehr nach dem TitleView
            parent = parent.Parent as VisualElement;
        }
        return result;
    }

    public void UpdatePositionFromAnchor()
    {
        if (AnchorElement == null) return;

        Dispatcher.Dispatch(() =>
        {
            // 1. Position des Buttons auf dem gesamten Fenster
            var anchorPos = GetAbsolutePosition(AnchorElement);

            // 2. Position des Skia-Canvas auf dem Fenster
            var canvasPos = GetAbsolutePosition(this);

            // 3. Die Differenz ist der Punkt im Canvas
            // Wir ziehen canvasPos ab, um den Versatz durch Toolbar/StatusBar zu eliminieren
            float localX = (float)(anchorPos.X - canvasPos.X);
            float localY = (float)(anchorPos.Y - canvasPos.Y);

            TargetPoint = new Point(
                localX + (AnchorElement.Width / 2),
                localY + (AnchorElement.Height / 2)
            );

            InvalidateSurface();
        });
    }

    public static readonly BindableProperty TargetPointProperty =
        BindableProperty.Create(nameof(TargetPoint), typeof(Point), typeof(TooltipOverlay), new Point(0, 0),
            propertyChanged: (b, o, n) => ((TooltipOverlay)b).InvalidateSurface());

    public static readonly BindableProperty HorizontalOffsetProperty =
        BindableProperty.Create(nameof(HorizontalOffset), typeof(double), typeof(TooltipOverlay), 0.0,
            propertyChanged: (b, o, n) => ((TooltipOverlay)b).InvalidateSurface());

    public static readonly BindableProperty VerticalOffsetProperty =
        BindableProperty.Create(nameof(VerticalOffset), typeof(double), typeof(TooltipOverlay), 0.0,
            propertyChanged: (b, o, n) => ((TooltipOverlay)b).InvalidateSurface());

    public double HorizontalOffset { get => (double)GetValue(HorizontalOffsetProperty); set => SetValue(HorizontalOffsetProperty, value); }
    public double VerticalOffset { get => (double)GetValue(VerticalOffsetProperty); set => SetValue(VerticalOffsetProperty, value); }

    public Point TargetPoint
    {
        get => (Point)GetValue(TargetPointProperty);
        set => SetValue(TargetPointProperty, value);
    }
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(TooltipOverlay), string.Empty, propertyChanged: (b, o, n) => ((TooltipOverlay)b).InvalidateSurface());

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        if (string.IsNullOrEmpty(Text)) return;

        float density = (float)DeviceDisplay.Current.MainDisplayInfo.Density;

        float targetX = (float)TargetPoint.X * density;
        float targetY = (float)TargetPoint.Y * density;
        SKPoint skTargetPoint = new(targetX, targetY);

        var info = e.Info;
        float padding = 16 * density;
        float tailSize = 30 * density;
        float cornerRadius = 24 * density;

        using var typeface = SKTypeface.FromFamilyName("Arial");
        using var font = new SKFont(typeface, 16 * density);

        var textWidth = font.MeasureText(Text, out var textBounds);
        float bubbleWidth = textWidth + (padding * 2);
        float bubbleHeight = font.Size + (padding * 2);

        float hManual = (float)HorizontalOffset * density;
        float vManual = (float)VerticalOffset * density;

        float offsetX = targetX < info.Width / 2
                        ? (tailSize + hManual)
                        : -(bubbleWidth + tailSize + hManual);

        float offsetY = targetY < info.Height / 2
                        ? (tailSize + vManual)
                        : -(bubbleHeight + tailSize + vManual);

        var bubbleRect = new SKRect(
            targetX + offsetX,
            targetY + offsetY,
            targetX + offsetX + bubbleWidth,
            targetY + offsetY + bubbleHeight);

        // --- MODERN: Pfad für die Blase mit SKPathBuilder ---
        var rectBuilder = new SKPathBuilder();
        rectBuilder.AddRoundRect(bubbleRect, cornerRadius, cornerRadius);
        using var rectPath = rectBuilder.Detach();

        // --- MODERN: Pfad für die Spitze mit SKPathBuilder ---
        var tailBuilder = new SKPathBuilder();
        tailBuilder.MoveTo(skTargetPoint);

        float inset = 30 * density;
        float tailWidthAtBase = 30 * density;
        float overlap = 1.0f * density;

        float tipBaseStart = (offsetX > 0)
            ? bubbleRect.Left + inset
            : bubbleRect.Right - inset - tailWidthAtBase;

        float verticalAttachPoint = targetY + offsetY + (offsetY > 0 ? 0 : bubbleHeight);
        float deepAttachPoint = offsetY > 0 ? verticalAttachPoint + overlap : verticalAttachPoint - overlap;

        tailBuilder.LineTo(tipBaseStart, deepAttachPoint);
        tailBuilder.LineTo(tipBaseStart + tailWidthAtBase, deepAttachPoint);
        tailBuilder.Close();
        using var tailPath = tailBuilder.Detach();

        // Pfade verschmelzen
        var combinedPath = rectPath.Op(tailPath, SKPathOp.Union);
        var finalPath = combinedPath ?? rectPath;

        // Zeichnen
        using var fillPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawPath(finalPath, fillPaint);

        using var strokePaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2 * density,
            IsAntialias = true,
            StrokeJoin = SKStrokeJoin.Round
        };
        canvas.DrawPath(finalPath, strokePaint);

        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        // Text zeichnen (Nutze die Font-Instanz direkt)
        canvas.DrawText(Text, bubbleRect.Left + padding, bubbleRect.Bottom - padding, font, textPaint);

        combinedPath?.Dispose();
    }
}