using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.DrawingTool;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc.DrawingTool;

public partial class DrawingController(TransformViewModel transformVm, double density) : IDisposable
{
    public CombinedDrawable? CombinedDrawable { get; private set; }
    public DrawMode DrawMode { get; set; } = DrawMode.None;
    private SKCanvasView? canvasView;
    private int? activeIndex = null;
    private DateTime? lastClickTime;
    private SKPoint? lastClickPosition;
    private readonly bool scaleHandlesWithTransform = true;
    private SKPoint? rectDragStart;
    private bool isDraggingRectangle;
    private bool isRotatingRectangle = false;

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
        bool scaleHandlesWithTransform = true,
        float rotationAngle = 0f)
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
            },
            RectangleDrawable = new InteractiveRectangleDrawable
            {
                FillColor = fillColor,
                LineColor = lineColor,
                PointColor = pointColor,
                LineThickness = lineThickness * (float)density,
                HandleRadius = scaleHandlesWithTransform
                    ? handleRadius / (float)transformVm.Scale * (float)density
                    : handleRadius * (float)density,
                            PointRadius = scaleHandlesWithTransform
                    ? pointRadius / (float)transformVm.Scale * (float)density
                    : pointRadius * (float)density,
                AllowedAngleDeg = rotationAngle
            }
        };

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
        if (DrawMode == DrawMode.Free || (DrawMode == DrawMode.Poly && activeIndex != null) || (DrawMode == DrawMode.Rectangle))
            e.Handled = true;
        else
            e.Handled = false;

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
                    poly.TryClosePolygon(p.X, p.Y);
                else if (activeIndex == null)
                    poly.Points.Add(p);
            }

            canvasView?.InvalidateSurface();
        }
        else if (DrawMode == DrawMode.Free)
        {
            var free = CombinedDrawable.FreeDrawable;
            free.StartStroke();
            free.AddPoint(p);
            canvasView?.InvalidateSurface();
        }
        else if (DrawMode == DrawMode.Rectangle)
        {
            var rect = CombinedDrawable.RectangleDrawable;
            if (rect == null) return;

            if (!rect.IsDrawn)
            {
                rectDragStart = p;
                isDraggingRectangle = true;
                rect.SetFromDrag(p, p);
            }
            else if (rect.IsOverRotationHandle(p))
            {
                transformVm.IsPanningEnabled = false;
                transformVm.IsPinchingEnabled = false;
                isRotatingRectangle = true;
            }
            else
            {
                activeIndex = rect.FindPointIndex(p.X, p.Y);

                if (activeIndex != null)
                {
                    transformVm.IsPanningEnabled = false;
                    transformVm.IsPinchingEnabled = false;
                }
                else
                {
                    rectDragStart = p;
                    isDraggingRectangle = true;
                    //rect.SetFromDrag(p, p);
                }
            }

            canvasView?.InvalidateSurface();
        }
    }

    private void OnDragInteraction(SKPoint p)
    {
        if (CombinedDrawable == null) return;

        if (DrawMode == DrawMode.Poly && activeIndex != null)
            CombinedDrawable.PolyDrawable.Points[(int)activeIndex] = p;
        else if (DrawMode == DrawMode.Free)
            CombinedDrawable.FreeDrawable.AddPoint(p);
        else if (DrawMode == DrawMode.Rectangle)
        {
            var rect = CombinedDrawable.RectangleDrawable;
            if (rect == null) return;

            if (isRotatingRectangle)
                rect.SetRotationFromPoint(p);
            else if (activeIndex != null)
                rect.MovePoint(activeIndex.Value, p);
            else if (isDraggingRectangle && rectDragStart.HasValue)
                rect.SetFromDrag(rectDragStart.Value, p);
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
            CombinedDrawable?.FreeDrawable.EndStroke();
        else if (DrawMode == DrawMode.Rectangle)
        {
            if (isDraggingRectangle && !rect.IsDrawn)
                rect.IsDrawn = true; // markiere Rechteck als gezeichnet
                
            activeIndex = null;
            rectDragStart = null;
            isDraggingRectangle = false;
            isRotatingRectangle = false;

            transformVm.IsPanningEnabled = true;
            transformVm.IsPinchingEnabled = true;
        }

        canvasView?.InvalidateSurface();
    }

    public void ResizeHandles()
    {
        if (CombinedDrawable == null)
            return;

        // Polyline
        var poly = CombinedDrawable.PolyDrawable;
        if (poly != null && DrawMode == DrawMode.Poly)
        {
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
        }

        // Rectangle
        var rect = CombinedDrawable.RectangleDrawable;
        if (rect != null && DrawMode == DrawMode.Rectangle)
        {
            if (scaleHandlesWithTransform)
            {
                rect.HandleRadius = (float)(SettingsService.Instance.PolyLineHandleTouchRadius / transformVm.Scale * density);
                rect.PointRadius = (float)(SettingsService.Instance.PolyLineHandleRadius / transformVm.Scale * density);
            }
            else
            {
                rect.HandleRadius = (float)(SettingsService.Instance.PolyLineHandleTouchRadius * density);
                rect.PointRadius = (float)(SettingsService.Instance.PolyLineHandleRadius * density);
            }
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

        // Rectangle aktualisieren
        var rect = CombinedDrawable.RectangleDrawable;
        if (rect != null)
        {
            rect.LineColor = lineColor;
            rect.FillColor = lineColor.WithAlpha((byte)(fillOpacity * 255));
            rect.LineThickness = lineWidth * (float)density;
        }

        canvasView?.InvalidateSurface();
    }

    private static float Distance(SKPoint a, SKPoint b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public void DrawWithoutHandles(SKCanvas canvas)
    {
        if (CombinedDrawable == null)
            return;

        var poly = CombinedDrawable.PolyDrawable;
        var rect = CombinedDrawable.RectangleDrawable;
        bool polyOld = false;
        bool rectOld = false;

        if (poly != null)
        {
            polyOld = poly.DisplayHandles;
            poly.DisplayHandles = false;
        }

        if (rect != null)
        {
            rectOld = rect.DisplayHandles;
            rect.DisplayHandles = false;
        }

        CombinedDrawable.Draw(canvas);

        poly?.DisplayHandles = polyOld;
        rect?.DisplayHandles = rectOld;
    }

    public bool IsEmpty()
    {
        if (CombinedDrawable == null)
            return true;

        bool polyEmpty =
            CombinedDrawable.PolyDrawable == null ||
            CombinedDrawable.PolyDrawable.Points.Count == 0;

        bool freeEmpty =
            CombinedDrawable.FreeDrawable == null ||
            CombinedDrawable.FreeDrawable.Strokes.Count == 0;

        bool rectEmpty =
            CombinedDrawable.RectangleDrawable == null ||
            CombinedDrawable.RectangleDrawable.Points.Length == 0;

        return polyEmpty && freeEmpty && rectEmpty;
    }

    public void Reset()
    {
        CombinedDrawable?.Reset();
        canvasView?.InvalidateSurface();
    }

    public SKRect? CalculateBoundingBox()
    {
        if (CombinedDrawable == null)
            return null;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        bool hasPoints = false;

        // === Polyline Punkte ===
        var poly = CombinedDrawable.PolyDrawable;
        if (poly != null && poly.Points.Count > 0)
        {
            foreach (var pt in poly.Points)
            {
                hasPoints = true;
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }
        }

        // === Freehand Punkte ===
        var free = CombinedDrawable.FreeDrawable;
        if (free != null && free.Strokes.Count > 0)
        {
            foreach (var stroke in free.Strokes)
            {
                foreach (var pt in stroke)
                {
                    hasPoints = true;
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.Y > maxY) maxY = pt.Y;
                }
            }
        }

        // === Rectangle Punkte ===
        var rect = CombinedDrawable.RectangleDrawable;
        if (rect != null && rect.Points.Length == 4)
        {
            foreach (var pt in rect.Points)
            {
                hasPoints = true;
                minX = Math.Min(minX, pt.X);
                maxX = Math.Max(maxX, pt.X);
                minY = Math.Min(minY, pt.Y);
                maxY = Math.Max(maxY, pt.Y);
            }
        }

        if (!hasPoints)
            return null;

        return new SKRect(minX, minY, maxX, maxY);
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
