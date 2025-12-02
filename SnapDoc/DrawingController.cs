using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc;

public partial class DrawingController(TransformViewModel transformVm, double density) : IDisposable
{
    private SKCanvasView? canvasView;
    public CombinedDrawable? CombinedDrawable { get; private set; }
    public DrawMode DrawMode { get; set; } = DrawMode.None;
    private int? activeIndex = null;
    private DateTime? lastClickTime;
    private SKPoint? lastClickPosition;
    private readonly bool scaleHandlesWithTransform = true;

    // BoundingBox
    public float MinX { get; private set; }
    public float MinY { get; private set; }
    public float MaxX { get; private set; }
    public float MaxY { get; private set; }

    public SKCanvasView CreateCanvasView()
    {
        var view = new SKCanvasView
        {
            BackgroundColor = Colors.Transparent,
            EnableTouchEvents = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        Attach(view);
        return view;
    }

    public void Attach(SKCanvasView view)
    {
        Detach();

        canvasView = view ?? throw new ArgumentNullException(nameof(view));
        canvasView.PaintSurface += OnPaintSurface;
        canvasView.Touch += OnTouch;
        canvasView.InvalidateSurface();
    }

    public void Detach()
    {
        if (canvasView == null) return;
        canvasView.PaintSurface -= OnPaintSurface;
        canvasView.Touch -= OnTouch;
        canvasView = null;
    }

    public void InitializeDrawing(SKColor lineColor,
        float lineThickness, SKColor fillColor, 
        float handleRadius, float pointRadius,
        SKColor pointColor, SKColor startPointColor,
        bool scaleHandlesWithTransform = true)
    {
        CombinedDrawable = new CombinedDrawable
        {
            FreeDrawable = new InteractiveFreehandDrawable
            {
                LineColor = lineColor,
                LineThickness = lineThickness * (float)density
            },
            PolyDrawable = new InteractivePolylineDrawable
            {
                FillColor = fillColor,
                LineColor = lineColor,
                PointColor = pointColor,
                StartPointColor = startPointColor,
                LineThickness = lineThickness * (float)density,
                HandleRadius = scaleHandlesWithTransform ? handleRadius / (float)transformVm.Scale * (float)density : handleRadius * (float)density,
                PointRadius = scaleHandlesWithTransform ? pointRadius / (float)transformVm.Scale * (float)density : pointRadius * (float)density
            }
        };

        ResetBoundingBox();
        canvasView?.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        CombinedDrawable?.Draw(canvas);
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        var p = e.Location;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed: OnStartInteraction(p); break;
            case SKTouchAction.Moved: OnDragInteraction(p); break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled: OnEndInteraction(); break;
        }

        // Event nur als handled markieren, wenn Free-Modus oder Poly-Modus und Punkt aktiv
        if (DrawMode == DrawMode.Free || (DrawMode == DrawMode.Poly && activeIndex != null))
            e.Handled = true; // blockiert andere Views
        else
            e.Handled = false; // lässt Touch durchgehen

        canvasView?.InvalidateSurface();
    }

    private void OnStartInteraction(SKPoint p)
    {
        if (CombinedDrawable == null) return;

        if (DrawMode == DrawMode.Poly)
        {
            var poly = CombinedDrawable.PolyDrawable;

            var now = DateTime.Now;
            if (lastClickTime.HasValue &&
                (now - lastClickTime.Value).TotalMilliseconds <= SettingsService.Instance.DoubleClickThresholdMs &&
                lastClickPosition.HasValue &&
                Distance(p, lastClickPosition.Value) <= SettingsService.Instance.PolyLineHandleRadius * (float)density)
            {
                DeletePointAt(p);
                lastClickTime = null;
                lastClickPosition = null;
                canvasView?.InvalidateSurface();
                return;
            }

            lastClickTime = now;
            lastClickPosition = p;

            activeIndex = poly.FindPointIndex(p.X, p.Y);
            if (activeIndex != null)
            {
                transformVm.IsPanningEnabled = false;
                transformVm.IsPinchingEnabled = false;
            }

            if (!poly.IsClosed)
            {
                if (poly.Points.Count > 2 && Distance(p, poly.Points[0]) <= poly.HandleRadius)
                {
                    poly.TryClosePolygon(p.X, p.Y);
                }
                else if (activeIndex == null)
                {
                    poly.Points.Add(p);
                    ResizeBoundingBox(p);
                }
            }

            canvasView?.InvalidateSurface();
        }
        else if (DrawMode == DrawMode.Free)
        {
            var free = CombinedDrawable.FreeDrawable;
            free.StartStroke();
            free.AddPoint(p);
            ResizeBoundingBox(p);
            canvasView?.InvalidateSurface();
        }
    }

    private void OnDragInteraction(SKPoint p)
    {
        if (CombinedDrawable == null) return;

        if (DrawMode == DrawMode.Poly && activeIndex != null)
        {
            CombinedDrawable.PolyDrawable.Points[(int)activeIndex] = p;
            ResizeBoundingBox(p);
        }
        else if (DrawMode == DrawMode.Free)
        {
            CombinedDrawable.FreeDrawable.AddPoint(p);
            ResizeBoundingBox(p);
        }

        canvasView?.InvalidateSurface();
    }

    private void OnEndInteraction()
    {
        if (DrawMode == DrawMode.Poly)
        {
            activeIndex = null;
            transformVm.IsPanningEnabled = true;
            transformVm.IsPinchingEnabled = true;
        }
        else if (DrawMode == DrawMode.Free)
        {
            CombinedDrawable?.FreeDrawable.EndStroke();
        }

        canvasView?.InvalidateSurface();
    }

    public void ResizePolyHandles()
    {
        if (CombinedDrawable == null || DrawMode != DrawMode.Poly) return;

        var poly = CombinedDrawable.PolyDrawable;

        if (scaleHandlesWithTransform)
        {
            poly.HandleRadius = (float)(SettingsService.Instance.PolyLineHandleTouchRadius / transformVm.Scale * density);
            poly.PointRadius = (float)(SettingsService.Instance.PolyLineHandleRadius / transformVm.Scale * density);
        }
        else
        {
            poly.HandleRadius = (float)(SettingsService.Instance.PolyLineHandleTouchRadius * density);
            poly.PointRadius = (float)(SettingsService.Instance.PolyLineHandleRadius * density);
        }

        canvasView?.InvalidateSurface();
    }

    private void DeletePointAt(SKPoint p)
    {
        var poly = CombinedDrawable?.PolyDrawable;
        if (poly == null) return;

        for (int i = 0; i < poly.Points.Count; i++)
        {
            if (Distance(p, poly.Points[i]) <= poly.HandleRadius)
            {
                poly.Points.RemoveAt(i);
                if (poly.Points.Count <= 2)
                    poly.Reset();
                ResetBoundingBox();
                foreach (var pt in poly.Points)
                    ResizeBoundingBox(pt);
                return;
            }
        }
    }

    public void UpdateDrawingStyles(SKColor lineColor, float lineWidth, float fillOpacity)
    {
        if (CombinedDrawable == null)
            return;

        // Freehand aktualisieren
        var free = CombinedDrawable.FreeDrawable;
        if (free != null)
        {
            free.LineColor = lineColor;
            free.LineThickness = lineWidth * (float)density;
        }

        // Polyline aktualisieren
        var poly = CombinedDrawable.PolyDrawable;
        if (poly != null)
        {
            poly.LineColor = lineColor;
            poly.FillColor = lineColor.WithAlpha((byte)(fillOpacity * 255));
            poly.LineThickness = lineWidth * (float)density;
        }

        canvasView?.InvalidateSurface();
    }

    private static float Distance(SKPoint a, SKPoint b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public void DrawWithoutHandles(SKCanvas canvas)
    {
        if (CombinedDrawable == null) return;

        var poly = CombinedDrawable.PolyDrawable;
        var old = poly.DisplayHandles;
        poly.DisplayHandles = false;
        CombinedDrawable.Draw(canvas);
        poly.DisplayHandles = old;
    }

    public bool IsEmpty()
        => CombinedDrawable == null ||
           (CombinedDrawable.PolyDrawable.Points.Count == 0 && CombinedDrawable.FreeDrawable.Strokes.Count == 0);

    public void Reset()
    {
        CombinedDrawable?.Reset();
        ResetBoundingBox();
        canvasView?.InvalidateSurface();
    }

    // ================= BoundingBox =================
    private void ResetBoundingBox()
    {
        MinX = float.MaxValue;
        MinY = float.MaxValue;
        MaxX = float.MinValue;
        MaxY = float.MinValue;
    }

    private void ResizeBoundingBox(SKPoint p)
    {
        if (p.X < MinX) MinX = p.X;
        if (p.X > MaxX) MaxX = p.X;
        if (p.Y < MinY) MinY = p.Y;
        if (p.Y > MaxY) MaxY = p.Y;
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }

    public SKRect? GetBoundingBoxRect()
    {
        if (MinX == float.MaxValue || MinY == float.MaxValue || MaxX == float.MinValue || MaxY == float.MinValue)
            return null; // keine Punkte vorhanden
        return new SKRect(MinX, MinY, MaxX, MaxY);
    }
}
