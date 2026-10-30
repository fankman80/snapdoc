using SkiaSharp;
using SnapDoc.Controls;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc.DrawingTool;

public partial class DrawingController(TransformViewModel transformVm) : IDisposable, IPlanDrawingLayer
{
    public SKPoint? ActiveHandle { get; private set; }
    private int? activeIndex = null;
    private DateTime? lastClickTime;
    private SKPoint? lastClickPosition;
    private SKPoint? rectDragStart;
    private SKPoint? ovalDragStart;
    private SKPoint? arrowDragStart;
    private bool isRotatingRectangle = false;
    private bool isRotatingOval = false;
    private bool isRotatingArrow = false;
    private SKPoint? rectResizeAnchor;
    private SKPoint? ovalResizeAnchor;
    private SKPoint? arrowResizeAnchor;
    public CombinedDrawable? CombinedDrawable { get; private set; }
    public DrawingStyleDto? LoadedStyle { get; private set; }
    public DrawMode DrawMode { get; set; } = DrawMode.None;
    private static readonly bool UseTouchLift = DeviceInfo.Platform != DevicePlatform.WinUI;
    private SKPoint touchLift = SKPoint.Empty;
    private bool isLiftActive = false;
    private bool IsHandleDrag() => DrawMode switch
    {
        DrawMode.Poly => activeIndex != null,
        DrawMode.Rect => activeIndex != null && !isRotatingRectangle,
        DrawMode.Oval => activeIndex != null && !isRotatingOval,
        DrawMode.Arrow => activeIndex != null && !isRotatingArrow,
        _ => false
    };
    private SKPoint ComputeLift()
    {
        float density = (float)Settings.DisplayDensity;
        float x = SettingsService.Instance.TouchLiftDp.X * density;
        float y = SettingsService.Instance.TouchLiftDp.Y * density;

        float a = -viewRotationDeg * MathF.PI / 180f;
        float cos = MathF.Cos(a), sin = MathF.Sin(a);
        return new SKPoint((x * cos - y * sin) * viewScale,
                           (x * sin + y * cos) * viewScale);
    }
    public bool IsActive { get; set; }
    public event EventHandler? Invalidated;
    private void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);
    private float viewScale = 1f;        // Plan-Pixel pro Bildschirm-Pixel
    private float viewRotationDeg = 0f;
    private bool pressConsumed;

    public void SetViewTransform(float scale, float rotationDeg)
    {
        if (scale <= 0.0001f)
            return;

        viewRotationDeg = rotationDeg;

        float newViewScale = 1f / scale;

        if (MathF.Abs(newViewScale - viewScale) < 1e-7f)
            return;

        viewScale = newViewScale;

        ApplyHandleScale();
    }

    private void ApplyHandleScale()
    {
        Measure.ViewScale = viewScale;

        if (CombinedDrawable == null)
            return;

        float handleRadius = (float)SettingsService.Instance.PolyLineHandleTouchRadius * viewScale;

        float pointRadius = (float)SettingsService.Instance.PolyLineHandleRadius * viewScale;

        CombinedDrawable.PolyDrawable.HandleRadius = handleRadius;
        CombinedDrawable.PolyDrawable.PointRadius = pointRadius;

        CombinedDrawable.RectDrawable.HandleRadius = handleRadius;
        CombinedDrawable.RectDrawable.PointRadius = pointRadius;

        CombinedDrawable.OvalDrawable.HandleRadius = handleRadius;
        CombinedDrawable.OvalDrawable.PointRadius = pointRadius;

        CombinedDrawable.ArrowDrawable.HandleRadius = handleRadius;
        CombinedDrawable.ArrowDrawable.PointRadius = pointRadius;
    }

    public void Draw(SKCanvas canvas) => CombinedDrawable?.Draw(canvas);

    public bool OnPressed(SKPoint p)
    {
        pressConsumed = false;
        OnStartInteraction(p);
        return pressConsumed || DrawMode switch
        {
            DrawMode.Free => true,
            DrawMode.Poly => activeIndex != null,
            DrawMode.Rect => activeIndex != null || rectDragStart.HasValue || isRotatingRectangle,
            DrawMode.Oval => activeIndex != null || ovalDragStart.HasValue || isRotatingOval,
            DrawMode.Arrow => activeIndex != null || arrowDragStart.HasValue || isRotatingArrow,
            _ => false
        };
    }

    public void OnMoved(SKPoint p) => OnDragInteraction(p);
    public void OnReleased() => OnEndInteraction();

    public void OnTap(SKPoint p)
    {
        if (DrawMode == DrawMode.Poly && CombinedDrawable?.PolyDrawable is { IsClosed: false } poly)
        {
            poly.Points.Add(p);
            Invalidate();
        }
    }

    public void OnCancel()
    {
        ActiveHandle = null;

        OnEndInteraction();
        // Halb aufgezogene Formen verwerfen, sonst blockiert IsDrawn ein neues Aufziehen
        if (CombinedDrawable is { } d)
        {
            if (!d.RectDrawable.HasContent) d.RectDrawable.Reset();
            if (!d.OvalDrawable.HasContent) d.OvalDrawable.Reset();
            if (!d.ArrowDrawable.HasContent) d.ArrowDrawable.Reset();
        }
        Invalidate();
    }
    public float InitialRotation = 0f;
    public float MinX { get; private set; }
    public float MinY { get; private set; }
    public float MaxX { get; private set; }
    public float MaxY { get; private set; }
    public MeasureSettings Measure { get; } = new();

    public bool IsMeasureActive
    {
        get => Measure.IsMeasureActive;
        set { Measure.IsMeasureActive = value; Invalidate(); }
    }

    public float MeasureFactor
    {
        get => Measure.MeasureFactor;
        set
        {
            if (MathF.Abs(Measure.MeasureFactor - value) < 1e-9f) return;
            Measure.MeasureFactor = value;
            if (Measure.IsMeasureActive)
                Invalidate();
        }
    }

    private void ApplyMeasureSettings()
    {
        if (CombinedDrawable == null) return;
        CombinedDrawable.PolyDrawable.Measure = Measure;
        CombinedDrawable.RectDrawable.Measure = Measure;
        CombinedDrawable.OvalDrawable.Measure = Measure;
        CombinedDrawable.ArrowDrawable.Measure = Measure;
    }

    public void Detach() => IsActive = false;

    public void InitializeDrawing(SKColor lineColor,
                                SKColor fillColor,
                                SKColor textColor,
                                float lineThickness,
                                string strokeStyle,
                                bool isHatchEffect,
                                float hatchStrokeWitdh,
                                float hatchStrokeSpace,
                                float hatchRotation,
                                float rotationAngle = 0f,
                                bool forceReset = false)
    {

        if (forceReset || CombinedDrawable == null)
        {
            CombinedDrawable = new CombinedDrawable
            {
                FreeDrawable = new InteractiveFreehandDrawable(),
                PolyDrawable = new InteractivePolylineDrawable(),
                RectDrawable = new InteractiveRectangleDrawable(),
                OvalDrawable = new InteractiveOvalDrawable(),
                ArrowDrawable = new InteractiveArrowDrawable()
            };
        }

        // Freehand
        CombinedDrawable.FreeDrawable.LineColor = lineColor;
        CombinedDrawable.FreeDrawable.LineThickness = lineThickness;

        // Polyline
        var poly = CombinedDrawable.PolyDrawable;
        poly.FillColor = fillColor;
        poly.LineColor = lineColor;
        poly.StrokeStyle = strokeStyle;
        poly.PointColor = SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha);
        poly.StartPointColor = SKColor.Parse(SettingsService.Instance.PolyLineStartHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha);
        poly.LineThickness = lineThickness;
        poly.IsHatchEffect = isHatchEffect;
        poly.HatchRotation = hatchRotation;
        poly.HatchStrokeWitdh = hatchStrokeWitdh;
        poly.HatchStrokeSpace = hatchStrokeSpace;

        // Rectangle
        var rect = CombinedDrawable.RectDrawable;
        rect.FillColor = fillColor;
        rect.LineColor = lineColor;
        rect.PointColor = SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha);
        rect.TextColor = textColor;
        rect.LineThickness = lineThickness;
        rect.StrokeStyle = strokeStyle;
        rect.AllowedAngleDeg = rotationAngle;
        rect.IsHatchEffect = isHatchEffect;
        rect.HatchRotation = hatchRotation;
        rect.HatchStrokeWitdh = hatchStrokeWitdh;
        rect.HatchStrokeSpace = hatchStrokeSpace;

        // Oval
        var oval = CombinedDrawable.OvalDrawable;
        oval.FillColor = fillColor;
        oval.LineColor = lineColor;
        oval.PointColor = SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha);
        oval.TextColor = textColor;
        oval.LineThickness = lineThickness;
        oval.StrokeStyle = strokeStyle;
        oval.AllowedAngleDeg = rotationAngle;
        oval.IsHatchEffect = isHatchEffect;
        oval.HatchRotation = hatchRotation;
        oval.HatchStrokeWitdh = hatchStrokeWitdh;
        oval.HatchStrokeSpace = hatchStrokeSpace;

        // Arrow
        var arrow = CombinedDrawable.ArrowDrawable;
        arrow.FillColor = fillColor;
        arrow.LineColor = lineColor;
        arrow.PointColor = SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha);
        arrow.LineThickness = lineThickness;
        arrow.StrokeStyle = strokeStyle;
        arrow.AllowedAngleDeg = rotationAngle;
        arrow.IsHatchEffect = isHatchEffect;
        arrow.HatchRotation = hatchRotation;
        arrow.HatchStrokeWitdh = hatchStrokeWitdh;
        arrow.HatchStrokeSpace = hatchStrokeSpace;

        ApplyHandleScale();
        ApplyMeasureSettings();
        Invalidate();
    }

    private void OnStartInteraction(SKPoint p)
    {
        isLiftActive = false; 
        
        if (CombinedDrawable == null) return;

        if (DrawMode == DrawMode.Poly)
        {
            var poly = CombinedDrawable.PolyDrawable;
            var now = DateTime.Now;

            // Doppeltipp auf Punkt -> löschen
            if (lastClickTime.HasValue &&
                (now - lastClickTime.Value).TotalMilliseconds <= SettingsService.Instance.DoubleClickThresholdMs &&
                lastClickPosition.HasValue &&
                Distance(p, lastClickPosition.Value) <= poly.HandleRadius)
            {
                DeletePointAt(p);
                lastClickTime = null;
                lastClickPosition = null;
                pressConsumed = true;
                Invalidate();
                return;
            }
            lastClickTime = now;
            lastClickPosition = p;

            // Tipp auf Startpunkt -> schliessen
            if (!poly.IsClosed && poly.Points.Count > 2 && Distance(p, poly.Points[0]) <= poly.HandleRadius)
            {
                poly.TryClosePolygon(p.X, p.Y);
                pressConsumed = true;
                Invalidate();
                return;
            }

            // Punkt greifen
            activeIndex = poly.FindPointIndex(p.X, p.Y);
            if (activeIndex != null)
                poly.IsResizing = true;

            Invalidate();
        }
        else if (DrawMode == DrawMode.Free)
        {
            var free = CombinedDrawable.FreeDrawable;
            free.StartStroke();
            free.AddPoint(p);
            Invalidate();
            DisableViewTransforms();
        }
        else if (DrawMode == DrawMode.Rect)
        {
            var rect = CombinedDrawable.RectDrawable;

            if (rect == null) return;

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
                    rect.IsResizing = true;
                    DisableViewTransforms();
                }
            }
            Invalidate();
        }
        else if (DrawMode == DrawMode.Oval)
        {
            var oval = CombinedDrawable.OvalDrawable;

            if (oval == null) return;

            if (!oval.IsDrawn)
            {
                ovalDragStart = p;
                DisableViewTransforms();
            }
            else if (oval.IsOverRotationHandle(p))
            {
                isRotatingOval = true;
                DisableViewTransforms();
            }
            else
            {
                activeIndex = oval.FindPointIndex(p.X, p.Y);
                if (activeIndex != null)
                {
                    ovalResizeAnchor = oval.GetOppositePoint(activeIndex.Value);
                    DisableViewTransforms();
                }
            }
            Invalidate();
        }
        else if (DrawMode == DrawMode.Arrow)
        {
            var arrow = CombinedDrawable.ArrowDrawable;

            if (arrow == null) return;

            if (!arrow.IsDrawn)
            {
                arrowDragStart = p;
                DisableViewTransforms();
            }
            else if (arrow.IsOverRotationHandle(p))
            {
                isRotatingArrow = true;
                DisableViewTransforms();
            }
            else
            {
                activeIndex = arrow.FindPointIndex(p.X, p.Y);
                if (activeIndex != null)
                {
                    if (activeIndex < 4)
                        arrowResizeAnchor = arrow.GetOppositePoint(activeIndex.Value);
                    DisableViewTransforms();
                }
            }
            Invalidate();
        }
    }

    private void OnDragInteraction(SKPoint p)
    {
        if (CombinedDrawable == null) return;

        // Beim ersten Move eines Handle-Drags einmalig nach oben springen
        SKPoint h = p;
        if (UseTouchLift && IsHandleDrag())
        {
            if (!isLiftActive)
            {
                touchLift = ComputeLift();
                isLiftActive = true;
            }

            h = new SKPoint(p.X + touchLift.X, p.Y + touchLift.Y);
        }

        ActiveHandle = IsHandleDrag() ? h : null;

        if (DrawMode == DrawMode.Poly && activeIndex != null)
            CombinedDrawable.PolyDrawable.Points[(int)activeIndex] = h;
        else if (DrawMode == DrawMode.Free)
            CombinedDrawable.FreeDrawable.AddPoint(p);
        else if (DrawMode == DrawMode.Rect)
        {
            var rect = CombinedDrawable.RectDrawable;
            if (rect == null) return;

            if (isRotatingRectangle)
                rect.SetRotationFromPoint(p);
            else if (activeIndex != null && rectResizeAnchor.HasValue)
                rect.SetFromDrag(rectResizeAnchor.Value, h);
            else if (rectDragStart.HasValue)
                rect.SetFromDrag(rectDragStart.Value, p);
        }
        else if (DrawMode == DrawMode.Oval)
        {
            var oval = CombinedDrawable.OvalDrawable;
            if (oval == null) return;

            if (isRotatingOval)
                oval.SetRotationFromPoint(p);
            else if (activeIndex != null)
                oval.ResizeFromHandle(activeIndex.Value, h);
            else if (ovalDragStart.HasValue)
                oval.SetFromDrag(ovalDragStart.Value, p);
        }
        else if (DrawMode == DrawMode.Arrow)
        {
            var arrow = CombinedDrawable.ArrowDrawable;
            if (arrow == null) return;

            if (isRotatingArrow)
                arrow.SetRotationFromPoint(p);
            else if (activeIndex == 4)
                arrow.UpdateShape(h);
            else if (activeIndex != null && arrowResizeAnchor.HasValue)
                arrow.SetFromDrag(arrowResizeAnchor.Value, h);
            else if (arrowDragStart.HasValue)
                arrow.SetFromDrag(arrowDragStart.Value, p);
        }

        Invalidate();
    }

    private void OnEndInteraction()
    {
        ActiveHandle = null;

        if (DrawMode == DrawMode.Poly)
        {
            if (CombinedDrawable?.PolyDrawable is { } poly)
                poly.IsResizing = false;
            activeIndex = null;
        }
        else if (DrawMode == DrawMode.Free)
            CombinedDrawable?.FreeDrawable.EndStroke();
        else if (DrawMode == DrawMode.Rect)
        {
            var rect = CombinedDrawable?.RectDrawable;

            rect?.IsResizing = false;

            if (rect is { IsDrawn: false })
                rect.IsDrawn = true;

            rectResizeAnchor = null;
            activeIndex = null;
            isRotatingRectangle = false;
            rectDragStart = null;
        }
        else if (DrawMode == DrawMode.Oval)
        {
            var oval = CombinedDrawable?.OvalDrawable;

            if (oval is { IsDrawn: false })
                oval.IsDrawn = true;

            ovalResizeAnchor = null;
            activeIndex = null;
            isRotatingOval = false;
            ovalDragStart = null;
        }
        else if (DrawMode == DrawMode.Arrow)
        {
            var arrow = CombinedDrawable?.ArrowDrawable;

            if (arrow is { IsDrawn: false })
                arrow.IsDrawn = true;

            arrowResizeAnchor = null;
            activeIndex = null;
            isRotatingArrow = false;
            arrowDragStart = null;
        }

        isLiftActive = false;
        EnableViewTransforms();

        Invalidate();
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

    public void UpdateDrawingStyles(SKColor lineColor, SKColor fillColor, SKColor textColor, float lineWidth, string strokeStyle, bool isHatchEffect, float hatchStrokeWitdh, float hatchStrokeSpace, float hatchRotation, float cloudRadius, float cloudInciseDeg)
    {
        if (CombinedDrawable == null) return;

        // Freehand aktualisieren
        var free = CombinedDrawable.FreeDrawable;
        if (free != null)
        {
            free.LineColor = lineColor;
            free.LineThickness = lineWidth;
        }

        // Polyline aktualisieren
        var poly = CombinedDrawable.PolyDrawable;
        if (poly != null)
        {
            poly.LineColor = lineColor;
            poly.FillColor = fillColor;
            poly.LineThickness = lineWidth;
            poly.StrokeStyle = strokeStyle;
            poly.CloudRadius = cloudRadius;
            poly.CloudInciseDeg = cloudInciseDeg;
            poly.IsHatchEffect = isHatchEffect;
            poly.HatchRotation = hatchRotation;
            poly.HatchStrokeWitdh = hatchStrokeWitdh;
            poly.HatchStrokeSpace = hatchStrokeSpace;
        }

        // Rectangle aktualisieren
        var rect = CombinedDrawable.RectDrawable;
        if (rect != null)
        {
            rect.LineColor = lineColor;
            rect.FillColor = fillColor;
            rect.TextColor = textColor;
            rect.LineThickness = lineWidth;
            rect.StrokeStyle = strokeStyle;
            rect.CloudRadius = cloudRadius;
            rect.CloudInciseDeg = cloudInciseDeg;
            rect.IsHatchEffect = isHatchEffect;
            rect.HatchRotation = hatchRotation;
            rect.HatchStrokeWitdh = hatchStrokeWitdh;
            rect.HatchStrokeSpace = hatchStrokeSpace;
        }

        // Oval aktualisieren
        var oval = CombinedDrawable.OvalDrawable;
        if (oval != null)
        {
            oval.LineColor = lineColor;
            oval.FillColor = fillColor;
            oval.TextColor = textColor;
            oval.LineThickness = lineWidth;
            oval.StrokeStyle = strokeStyle;
            oval.CloudRadius = cloudRadius;
            oval.CloudInciseDeg = cloudInciseDeg;
            oval.IsHatchEffect = isHatchEffect;
            oval.HatchRotation = hatchRotation;
            oval.HatchStrokeWitdh = hatchStrokeWitdh;
            oval.HatchStrokeSpace = hatchStrokeSpace;
        }

        // Arrow aktualisieren
        var arrow = CombinedDrawable.ArrowDrawable;
        if (arrow != null)
        {
            arrow.LineColor = lineColor;
            arrow.FillColor = fillColor;
            arrow.LineThickness = lineWidth;
            arrow.StrokeStyle = strokeStyle;
            arrow.IsHatchEffect = isHatchEffect;
            arrow.HatchRotation = hatchRotation;
            arrow.HatchStrokeWitdh = hatchStrokeWitdh;
            arrow.HatchStrokeSpace = hatchStrokeSpace;
        }

        Invalidate();
    }

    private static float Distance(SKPoint a, SKPoint b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public bool IsEmpty()
    {
        if (CombinedDrawable == null)
            return true;

        bool polyEmpty =
            CombinedDrawable.PolyDrawable == null ||
            !CombinedDrawable.PolyDrawable.HasContent;

        bool freeEmpty =
            CombinedDrawable.FreeDrawable == null ||
            CombinedDrawable.FreeDrawable.Points.Count == 0;

        bool rectEmpty =
            CombinedDrawable.RectDrawable == null ||
            !CombinedDrawable.RectDrawable.HasContent;

        bool ovalEmpty =
            CombinedDrawable.OvalDrawable == null ||
            !CombinedDrawable.OvalDrawable.HasContent;

        bool arrowEmpty =
            CombinedDrawable.ArrowDrawable == null ||
            !CombinedDrawable.ArrowDrawable.HasContent;

        return polyEmpty && freeEmpty && rectEmpty && ovalEmpty && arrowEmpty;
    }

    public void Reset()
    {
        CombinedDrawable?.Reset();
        Invalidate();
    }

    public void SaveToFile(string path)
    {
        if (CombinedDrawable == null)
            return;

        DrawingPersistenceService.Save(path, CombinedDrawable, 0f);
    }

    public void LoadFromFile(string path, SKPoint position)
    {
        CombinedDrawable ??= new CombinedDrawable
        {
            FreeDrawable = new InteractiveFreehandDrawable(),
            PolyDrawable = new InteractivePolylineDrawable(),
            RectDrawable = new InteractiveRectangleDrawable(),
            OvalDrawable = new InteractiveOvalDrawable(),
            ArrowDrawable = new InteractiveArrowDrawable()
        };

        // Wir laden das DTO und befüllen das CombinedDrawable
        var dto = DrawingPersistenceService.Load(path, CombinedDrawable, position, this);
        ApplyMeasureSettings();

        if (dto != null)
        {
            InitialRotation = dto.InitialRotation;
            LoadedStyle = dto.Style;

            if (dto.Rect != null)
                DrawMode = DrawMode.Rect;
            else if (dto.Oval != null)
                DrawMode = DrawMode.Oval;
            else if (dto.Poly != null)
                DrawMode = DrawMode.Poly;
            else if (dto.Arrow != null)
                DrawMode = DrawMode.Arrow;
            else if (dto.Free != null)
                DrawMode = DrawMode.Free;
            else
                DrawMode = DrawMode.None;
        }
        else
            DrawMode = DrawMode.None;

        HideAllHandles();
        UpdateHandleVisibility();
        Invalidate();
    }

    private void HideAllHandles()
    {
        if (CombinedDrawable == null) return;

        CombinedDrawable.PolyDrawable?.DisplayHandles = false;
        CombinedDrawable.RectDrawable?.DisplayHandles = false;
        CombinedDrawable.OvalDrawable?.DisplayHandles = false;
        CombinedDrawable.ArrowDrawable?.DisplayHandles = false;
    }

    private void UpdateHandleVisibility()
    {
        if (CombinedDrawable == null) return;

        // Nur die Handles des aktiven Modus einblenden
        CombinedDrawable.RectDrawable.DisplayHandles = (DrawMode == DrawMode.Rect);
        CombinedDrawable.OvalDrawable.DisplayHandles = (DrawMode == DrawMode.Oval);
        CombinedDrawable.PolyDrawable.DisplayHandles = (DrawMode == DrawMode.Poly);
        CombinedDrawable.ArrowDrawable.DisplayHandles = (DrawMode == DrawMode.Arrow);
    }

    public SKRect? CalculatePlanBoundingBox()
    {
        if (CombinedDrawable == null)
            return null;

        var points = new List<SKPoint>();

        if (CombinedDrawable.PolyDrawable?.HasPoints == true)
            points.AddRange(CombinedDrawable.PolyDrawable.Points);

        if (CombinedDrawable.FreeDrawable?.HasContent == true)
            points.AddRange(
                CombinedDrawable.FreeDrawable.Points.SelectMany(stroke => stroke));

        if (CombinedDrawable.RectDrawable?.HasContent == true)
            points.AddRange(CombinedDrawable.RectDrawable.Points);

        if (CombinedDrawable.OvalDrawable?.HasContent == true)
            points.AddRange(CombinedDrawable.OvalDrawable.Points);

        if (CombinedDrawable.ArrowDrawable?.HasContent == true)
            points.AddRange(CombinedDrawable.ArrowDrawable.Points);

        if (points.Count == 0)
            return null;

        float minX = points.Min(p => p.X);
        float minY = points.Min(p => p.Y);
        float maxX = points.Max(p => p.X);
        float maxY = points.Max(p => p.Y);

        return new SKRect(minX, minY, maxX, maxY);
    }

    public void RenderForExport(SKCanvas canvas)
    {
        if (CombinedDrawable == null)
            return;

        var poly = CombinedDrawable.PolyDrawable;
        var rect = CombinedDrawable.RectDrawable;
        var oval = CombinedDrawable.OvalDrawable;
        var arrow = CombinedDrawable.ArrowDrawable;

        bool polyHandles = poly.DisplayHandles;
        bool rectHandles = rect.DisplayHandles;
        bool ovalHandles = oval.DisplayHandles;
        bool arrowHandles = arrow.DisplayHandles;

        bool polyResizing = poly.IsResizing;
        bool rectResizing = rect.IsResizing;

        try
        {
            poly.DisplayHandles = false;
            rect.DisplayHandles = false;
            oval.DisplayHandles = false;
            arrow.DisplayHandles = false;

            poly.IsResizing = false;
            rect.IsResizing = false;

            CombinedDrawable.Draw(canvas);
        }
        finally
        {
            poly.DisplayHandles = polyHandles;
            rect.DisplayHandles = rectHandles;
            oval.DisplayHandles = ovalHandles;
            arrow.DisplayHandles = arrowHandles;

            poly.IsResizing = polyResizing;
            rect.IsResizing = rectResizing;
        }
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
