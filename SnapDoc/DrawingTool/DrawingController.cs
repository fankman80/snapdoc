using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc.DrawingTool;

public partial class DrawingController(TransformViewModel transformVm, double density) : IDisposable
{
    public CombinedDrawable? CombinedDrawable { get; private set; }
    public DrawMode DrawMode { get; set; } = DrawMode.None;
    public double InitialRotation = 0f;
    private SKCanvasView? canvasView;
    private int? activeIndex = null;
    private DateTime? lastClickTime;
    private SKPoint? lastClickPosition;
    private readonly bool scaleHandlesWithTransform = true;
    private SKPoint? rectDragStart;
    private bool isRotatingRectangle = false;
    private SKPoint? rectResizeAnchor;


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
        float lineThickness, SKColor fillColor, SKColor textColor,
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
            RectDrawable = new InteractiveRectangleDrawable
            {
                FillColor = fillColor,
                LineColor = lineColor,
                PointColor = pointColor,
                TextColor = textColor,
                LineThickness = lineThickness * (float)density,
                HandleRadius = scaleHandlesWithTransform
                    ? handleRadius / (float)transformVm.Scale * (float)density
                    : handleRadius * (float)density,
                            PointRadius = scaleHandlesWithTransform
                    ? pointRadius / (float)transformVm.Scale * (float)density
                    : pointRadius * (float)density,
                AllowedAngleDeg = rotationAngle,
                Text = ""
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
        if (DrawMode == DrawMode.Free ||
           (DrawMode == DrawMode.Poly && activeIndex != null) ||
           (DrawMode == DrawMode.Rect && (activeIndex != null || rectDragStart.HasValue || isRotatingRectangle)))
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
                DisableViewTransforms();
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
            DisableViewTransforms();
        }
        else if (DrawMode == DrawMode.Rect)
        {
            var rect = CombinedDrawable.RectDrawable;
            if (rect == null)
                return;

            if (!rect.IsDrawn)
            {
                rectDragStart = p;
                DisableViewTransforms();
            }
            else if (rect.IsOverRotationHandle(p))
            {
                isRotatingRectangle = true;
                DisableViewTransforms();
            }
            else
            {
                activeIndex = rect.FindPointIndex(p.X, p.Y);
                if (activeIndex != null)
                {
                    rectResizeAnchor = rect.GetOppositePoint(activeIndex.Value);
                    DisableViewTransforms();
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
        else if (DrawMode == DrawMode.Rect)
        {
            var rect = CombinedDrawable.RectDrawable;
            if (rect == null) return;

            if (isRotatingRectangle)
            {
                rect.SetRotationFromPoint(p);
            }
            else if (activeIndex != null && rectResizeAnchor.HasValue)
            {
                rect.SetFromDrag(rectResizeAnchor.Value, p);
            }
            else if (rectDragStart.HasValue)
            {
                rect.SetFromDrag(rectDragStart.Value, p);
            }
        }

        canvasView?.InvalidateSurface();
    }

    private void OnEndInteraction()
    {
        if (DrawMode == DrawMode.Poly)
        {
            activeIndex = null;
        }
        else if (DrawMode == DrawMode.Free)
        {
            CombinedDrawable?.FreeDrawable.EndStroke();
        }
        else if (DrawMode == DrawMode.Rect)
        {
            var rect = CombinedDrawable?.RectDrawable;

            if (rect is { IsDrawn: false })
                rect.IsDrawn = true;

            rectResizeAnchor = null;
            activeIndex = null;
            isRotatingRectangle = false;
            rectDragStart = null;
        }

        EnableViewTransforms();

        canvasView?.InvalidateSurface();
    }

    private void EnableViewTransforms()
    {
        transformVm.IsPanningEnabled = true;
        transformVm.IsPinchingEnabled = true;
        transformVm.IsRotatingEnabled = true;
    }

    private void DisableViewTransforms()
    {
        transformVm.IsPanningEnabled = false;
        transformVm.IsPinchingEnabled = false;
        transformVm.IsRotatingEnabled = false;
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
        var rect = CombinedDrawable.RectDrawable;
        if (rect != null && DrawMode == DrawMode.Rect)
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

    public void UpdateDrawingStyles(SKColor lineColor, SKColor fillColor, SKColor textColor, float lineWidth)
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
            poly.FillColor = fillColor;
            poly.LineThickness = lineWidth * (float)density;
        }

        // Rectangle aktualisieren
        var rect = CombinedDrawable.RectDrawable;
        if (rect != null)
        {
            rect.LineColor = lineColor;
            rect.FillColor = fillColor;
            rect.TextColor = textColor;
            rect.LineThickness = lineWidth * (float)density;
        }

        canvasView?.InvalidateSurface();
    }

    private static float Distance(SKPoint a, SKPoint b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public bool IsEmpty()
    {
        if (CombinedDrawable == null)
            return true;

        bool polyEmpty =
            CombinedDrawable.PolyDrawable == null ||
            CombinedDrawable.PolyDrawable.Points.Count == 0;

        bool freeEmpty =
            CombinedDrawable.FreeDrawable == null ||
            CombinedDrawable.FreeDrawable.Points.Count == 0;

        bool rectEmpty =
            CombinedDrawable.RectDrawable == null ||
            !CombinedDrawable.RectDrawable.HasContent;

        return polyEmpty && freeEmpty && rectEmpty;
    }

    public void Reset()
    {
        CombinedDrawable?.Reset();
        canvasView?.InvalidateSurface();
    }

    private SKPoint GetRotationCenter()
    {
        if (canvasView == null)
            return new SKPoint(0,0);

        return new SKPoint(
            canvasView.CanvasSize.Width / 2f,
            canvasView.CanvasSize.Height / 2f
        );
    }

    public SKRect? CalculateBoundingBox(float rotationDeg)
    {
        if (CombinedDrawable == null)
            return null;

        var allPoints = new List<SKPoint>();

        if (CombinedDrawable.PolyDrawable != null)
            allPoints.AddRange(CombinedDrawable.PolyDrawable.Points);

        if (CombinedDrawable.FreeDrawable != null)
            foreach (var stroke in CombinedDrawable.FreeDrawable.Points)
                allPoints.AddRange(stroke);

        if (CombinedDrawable.RectDrawable is { IsDrawn: true, Points.Length: 4 })
            allPoints.AddRange(CombinedDrawable.RectDrawable.Points);

        if (allPoints.Count == 0)
            return null;

        IEnumerable<SKPoint> points = allPoints;

        if (Math.Abs(rotationDeg) > 0.001f)
        {
            var pivot = GetRotationCenter();

            var matrix = SKMatrix.CreateRotationDegrees(
                rotationDeg,
                pivot.X,
                pivot.Y);

            points = allPoints.Select(p => matrix.MapPoint(p));
        }

        float minX = points.Min(p => p.X);
        float maxX = points.Max(p => p.X);
        float minY = points.Min(p => p.Y);
        float maxY = points.Max(p => p.Y);

        return new SKRect(minX, minY, maxX, maxY);
    }

    public void DrawWithoutHandles(SKCanvas canvas)
    {
        if (CombinedDrawable == null)
            return;

        var poly = CombinedDrawable.PolyDrawable;
        var rect = CombinedDrawable.RectDrawable;
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

    public void RenderFinal(SKCanvas canvas, float rotationDeg)
    {
        if (CombinedDrawable == null)
            return;

        var pivot = GetRotationCenter();

        canvas.Save();

        if (Math.Abs(rotationDeg) > 0.001f)
        {
            canvas.Translate(pivot.X, pivot.Y);
            canvas.RotateDegrees(rotationDeg);
            canvas.Translate(-pivot.X, -pivot.Y);
        }

        DrawWithoutHandles(canvas);

        canvas.Restore();
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
