using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

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

        // 1. Schriftart und Text-Metriken (Modern SkiaSharp)
        using var typeface = SKTypeface.FromFamilyName("Arial");
        using var font = new SKFont(typeface, 16 * density);

        // Textgröße messen
        var textWidth = font.MeasureText(Text, out var textBounds);

        float bubbleWidth = textWidth + (padding * 2);
        float bubbleHeight = font.Size + (padding * 2); // Oder font.Metrics.CapHeight nutzen

        // --- NEU: Hier definierst du den Abstand in MAUI-Einheiten ---
        float bubbleGap = 10 * density; // 10 Pixel Abstand zwischen Spitze und Blase
        float hManual = (float)HorizontalOffset * density;
        float vManual = (float)VerticalOffset * density;

        // 2. Ausrichtung berechnen
        // Wenn targetX links ist, addieren wir hManual, wenn rechts, subtrahieren wir es.
        float offsetX = targetX < info.Width / 2
                        ? (tailSize + hManual)
                        : -(bubbleWidth + tailSize + hManual);

        // Das Gleiche für die vertikale Verschiebung
        float offsetY = targetY < info.Height / 2
                        ? (tailSize + vManual)
                        : -(bubbleHeight + tailSize + vManual);

        var bubbleRect = new SKRect(
            targetX + offsetX,
            targetY + offsetY,
            targetX + offsetX + bubbleWidth,
            targetY + offsetY + bubbleHeight);

        // 3. Pfad für Sprechblase erstellen
        using var rectPath = new SKPath();
        rectPath.AddRoundRect(bubbleRect, cornerRadius, cornerRadius);

        using var tailPath = new SKPath();
        tailPath.MoveTo(skTargetPoint);

        float inset = 30 * density;
        float tailWidthAtBase = 30 * density;
        float overlap = 1.0f * density;

        float tipBaseStart;
        if (offsetX > 0)
        {
            tipBaseStart = bubbleRect.Left + inset;
        }
        else
        {
            tipBaseStart = bubbleRect.Right - inset - tailWidthAtBase;
        }

        // Hier korrigieren wir den Ankerpunkt: 
        // Wir ziehen das 'overlap' ab oder addieren es, je nach Lage.
        float verticalAttachPoint = targetY + offsetY + (offsetY > 0 ? 0 : bubbleHeight);
        float deepAttachPoint = offsetY > 0 ? verticalAttachPoint + overlap : verticalAttachPoint - overlap;

        tailPath.LineTo(tipBaseStart, deepAttachPoint);
        tailPath.LineTo(tipBaseStart + tailWidthAtBase, deepAttachPoint);
        tailPath.Close();

        // JETZT VERSCHMELZEN:
        // Wir nutzen SKPath.Op um aus zwei Pfaden einen neuen, kombinierten Pfad zu machen.
        var combinedPath = rectPath.Op(tailPath, SKPathOp.Union);

        // Falls Op fehlschlägt (null zurückgibt), nehmen wir zur Sicherheit den rectPath
        var finalPath = combinedPath ?? rectPath;

        // 4. Zeichnen (jetzt mit finalPath)
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

        // Wichtig: combinedPath muss entsorgt werden, da Op ein neues Objekt erstellt
        combinedPath?.Dispose();

        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        // Text zeichnen (wie bisher):
        canvas.DrawText(Text, bubbleRect.Left + padding, bubbleRect.Bottom - padding, font, textPaint);
    }
}