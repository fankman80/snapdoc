#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp.Views.Maui;
using SnapDoc.DrawingTool;
using System.Runtime.CompilerServices;

namespace SnapDoc.Controls;

public partial class DrawingToolbar : ContentView
{
    public event EventHandler RedrawRequested;
    public event EventHandler Erased;
    public event EventHandler<DrawMode> DrawModeChanged;
    public event EventHandler<double> BarsHeightChanged;
    private DrawingController _controller;
    private bool _syncingCloud;
    public void Attach(DrawingController controller) => _controller = controller;
    public double BarsHeight => Bars.Height > 0 ? Bars.Height : 0;
    private BottomSheet[] AllSheets => [PenSheet, FillSheet, CloudSheet, TextSheet];
    public bool IsAnySheetOpen => AllSheets.Any(s => s.IsOpen);
    private Color selectedBorderColor = new(0, 153, 0, 255);
    public Color SelectedBorderColor { get => selectedBorderColor; set => Set(ref selectedBorderColor, value); }
    private Color selectedFillColor = new(202, 255, 150, 128);
    public Color SelectedFillColor { get => selectedFillColor; set => Set(ref selectedFillColor, value); }
    private Color selectedTextColor = new(0, 0, 0, 255);
    public Color SelectedTextColor { get => selectedTextColor; set => Set(ref selectedTextColor, value); }
    public int LineWidth { get; private set; } = 3;
    public string StrokeStyle { get; private set; } = "";
    public bool IsHatchEffect { get; private set; }
    public float HatchStrokeWitdh { get; private set; } = 2f;
    public float HatchStrokeSpace { get; private set; } = 8f;
    public float HatchRotation { get; private set; } = 45f;
    public float CloudRadius { get; private set; } = 20f;
    public float CloudInciseDeg { get; private set; } = 15f;
    public DrawMode DrawMode { get; private set; } = DrawMode.None;
    private string activeTool = "";
    public string ActiveTool { get => activeTool; set => Set(ref activeTool, value); }
    private string activeSheet = "";
    public string ActiveSheet { get => activeSheet; set => Set(ref activeSheet, value); }
    private bool isFillOptionVisible;
    public bool IsFillOptionVisible { get => isFillOptionVisible; set => Set(ref isFillOptionVisible, value); }
    private bool isTextOptionVisible;
    public bool IsTextOptionVisible { get => isTextOptionVisible; set => Set(ref isTextOptionVisible, value); }
    private bool isCloudOptionVisible;
    public bool IsCloudOptionVisible { get => isCloudOptionVisible; set => Set(ref isCloudOptionVisible, value); }

    private bool isCloudActive;
    public bool IsCloudActive
    {
        get => isCloudActive;
        set
        {
            if (Set(ref isCloudActive, value) && !_syncingCloud)
                ApplyCloudToDrawable(value);
        }
    }

    public DrawingToolbar()
    {
        InitializeComponent();
        Root.BindingContext = this;

        foreach (var sheet in AllSheets)
        {
            var s = sheet;
            s.Closed += (_, _) => { if (!IsAnySheetOpen) ActiveSheet = ""; };
        }

        OptionBarScroll.SizeChanged += (_, _) => OptionBarHost.MinimumWidthRequest = OptionBarScroll.Width;
        ToolBarScroll.SizeChanged += (_, _) => ToolBarHost.MinimumWidthRequest = ToolBarScroll.Width;
        Bars.SizeChanged += (_, _) => BarsHeightChanged?.Invoke(this, BarsHeight);
    }

    public void InitializeDrawing(bool forceReset)
    {
        _controller.InitializeDrawing(
            SelectedBorderColor.ToSKColor(),
            SelectedFillColor.ToSKColor(),
            SelectedTextColor.ToSKColor(),
            LineWidth, StrokeStyle,
            IsHatchEffect, HatchStrokeWitdh, HatchStrokeSpace, HatchRotation,
            rotationAngle: 0f,
            forceReset: forceReset);

        if (forceReset)
            SetDrawMode(DrawMode.Rect);
    }

    public void LoadStyle(Color border, Color fill, Color text, int lineWidth, string strokeStyle,
                      bool hatch, float hatchWidth, float hatchSpace, float hatchAngle,
                      float? cloudRadius = null, float? cloudInciseDeg = null)
    {
        SelectedBorderColor = border;
        SelectedFillColor = fill;
        SelectedTextColor = text;
        LineWidth = lineWidth;
        StrokeStyle = strokeStyle ?? "";
        IsHatchEffect = hatch;
        HatchStrokeWitdh = hatchWidth;
        HatchStrokeSpace = hatchSpace;
        HatchRotation = hatchAngle;

        if (cloudRadius is float r) CloudRadius = r;
        if (cloudInciseDeg is float i) CloudInciseDeg = i;
    }

    public void Reset()
    {
        DrawMode = DrawMode.None;
        ActiveTool = "";
        ActiveSheet = "";
    }

    public bool CloseOpenSheet()
    {
        var open = AllSheets.FirstOrDefault(s => s.IsOpen);
        if (open == null) return false;
        _ = open.CloseAsync();
        return true;
    }

    public async Task CloseSheetsAsync(BottomSheet except = null)
    {
        foreach (var s in AllSheets)
            if (s != except && s.IsOpen)
                await s.CloseAsync();
    }

    public void SetDrawMode(DrawMode mode)
    {
        if (mode == DrawMode.None || _controller == null) return;

        DrawMode = mode;
        _controller.DrawMode = mode;
        ActiveTool = mode.ToString();

        IsFillOptionVisible = mode is DrawMode.Rect or DrawMode.Oval or DrawMode.Poly or DrawMode.Arrow;
        IsTextOptionVisible = mode is DrawMode.Rect or DrawMode.Oval;
        IsCloudOptionVisible = mode is DrawMode.Poly or DrawMode.Rect or DrawMode.Oval;

        var combined = _controller.CombinedDrawable;

        // Zustand der Form übernehmen, ohne ihn zurückzuschreiben
        _syncingCloud = true;
        IsCloudActive = mode switch
        {
            DrawMode.Rect => combined?.RectDrawable?.IsCloud ?? false,
            DrawMode.Oval => combined?.OvalDrawable?.IsCloud ?? false,
            DrawMode.Poly => combined?.PolyDrawable?.IsCloud ?? false,
            _ => false
        };
        _syncingCloud = false;

        if (combined != null)
        {
            combined.PolyDrawable.DisplayHandles = mode == DrawMode.Poly;
            combined.RectDrawable.DisplayHandles = mode == DrawMode.Rect;
            combined.OvalDrawable.DisplayHandles = mode == DrawMode.Oval;
            combined.ArrowDrawable.DisplayHandles = mode == DrawMode.Arrow;
        }
        _controller.RulerDrawable.DisplayHandles = mode == DrawMode.Ruler;

        // Sheets schliessen, die zum neuen Werkzeug nicht passen
        if (!IsFillOptionVisible && FillSheet.IsOpen) _ = FillSheet.CloseAsync();
        if (!IsTextOptionVisible && TextSheet.IsOpen) _ = TextSheet.CloseAsync();
        if (!IsCloudOptionVisible && CloudSheet.IsOpen)
            _ = CloudSheet.CloseAsync();
        else if (CloudSheet.IsOpen)
            CloudEditor.Load(IsCloudActive, CloudRadius, CloudInciseDeg);

        DrawModeChanged?.Invoke(this, mode);
        Redraw();
    }


    [RelayCommand] private void SelectTool(string key)
    {
        if (Enum.TryParse(key, out DrawMode mode))
            SetDrawMode(mode);
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        _controller.Reset();
        SetDrawMode(DrawMode == DrawMode.None ? DrawMode.Rect : DrawMode);
        Erased?.Invoke(this, EventArgs.Empty);
    }

    private async Task ToggleSheetAsync(BottomSheet sheet, string key, Action load)
    {
        if (sheet.IsOpen)
        {
            await sheet.CloseAsync();
            return;
        }
        await CloseSheetsAsync(except: sheet);
        load?.Invoke();
        ActiveSheet = key;
        await sheet.OpenAsync();
    }

    private async void PenClicked(object sender, EventArgs e) =>
        await ToggleSheetAsync(PenSheet, "Pen", () => PenEditor.Load(SelectedBorderColor, LineWidth, StrokeStyle));

    private async void FillClicked(object sender, EventArgs e) =>
        await ToggleSheetAsync(FillSheet, "Fill", () =>
            FillEditor.Load(SelectedFillColor, IsHatchEffect, HatchStrokeWitdh, HatchStrokeSpace, HatchRotation));

    private async void CloudClicked(object sender, EventArgs e) =>
        await ToggleSheetAsync(CloudSheet, "Cloud", () => CloudEditor.Load(IsCloudActive, CloudRadius, CloudInciseDeg));

    private async void TextClicked(object sender, EventArgs e)
    {
        var combined = _controller?.CombinedDrawable;
        if (combined == null || DrawMode is not (DrawMode.Rect or DrawMode.Oval)) return;

        await ToggleSheetAsync(TextSheet, "Text", () =>
        {
            if (DrawMode == DrawMode.Rect)
            {
                var d = combined.RectDrawable;
                TextEditor.Load(d.TextSize, d.TextAlignment, d.TextStyle, d.AutoSizeText, d.Text, d.TextPadding);
            }
            else
            {
                var d = combined.OvalDrawable;
                TextEditor.Load(d.TextSize, d.TextAlignment, d.TextStyle, d.AutoSizeText, d.Text, d.TextPadding);
            }
        });

        if (TextSheet.IsOpen)
            TextEditor.FocusInput();
    }

    private void OnPenChanged(object sender, EventArgs e)
    {
        SelectedBorderColor = PenEditor.LineColor;
        LineWidth = PenEditor.LineWidth;
        StrokeStyle = PenEditor.StrokeStyle;
        ApplyStyles();
    }

    private void OnFillChanged(object sender, EventArgs e)
    {
        var f = FillEditor;
        SelectedFillColor = f.FillColor;
        IsHatchEffect = f.IsHatchEffect;
        HatchStrokeWitdh = f.HatchStrokeWitdh;
        HatchStrokeSpace = f.HatchStrokeSpace;
        HatchRotation = f.HatchRotation;
        ApplyStyles();
    }

    private void OnCloudChanged(object sender, EventArgs e)
    {
        var c = CloudEditor;
        IsCloudActive = c.IsCloudActive;   // schreibt über ApplyCloudToDrawable in die Form
        CloudRadius = c.CloudRadius;
        CloudInciseDeg = c.CloudInciseDeg;
        ApplyStyles();
    }

    private void OnTextColorChanged(object sender, EventArgs e) => ApplyStyles();

    private void OnTextEditChanged(object sender, EventArgs e)
    {
        var combined = _controller?.CombinedDrawable;
        if (combined == null) return;

        var t = TextEditor;
        if (DrawMode == DrawMode.Rect)
        {
            var d = combined.RectDrawable;
            d.Text = t.InputTxt; d.TextSize = t.FontSize; d.TextAlignment = t.FontAlignment;
            d.TextStyle = t.TextStyle; d.AutoSizeText = t.AutoSize; d.TextPadding = t.TextPadding;
        }
        else if (DrawMode == DrawMode.Oval)
        {
            var d = combined.OvalDrawable;
            d.Text = t.InputTxt; d.TextSize = t.FontSize; d.TextAlignment = t.FontAlignment;
            d.TextStyle = t.TextStyle; d.AutoSizeText = t.AutoSize; d.TextPadding = t.TextPadding;
        }
        Redraw();
    }

    private void ApplyStyles()
    {
        _controller?.UpdateDrawingStyles(
            SelectedBorderColor.ToSKColor(),
            SelectedFillColor.ToSKColor(),
            SelectedTextColor.ToSKColor(),
            LineWidth,
            StrokeStyle,
            IsHatchEffect,
            HatchStrokeWitdh,
            HatchStrokeSpace,
            HatchRotation,
            CloudRadius,
            CloudInciseDeg);
        Redraw();
    }

    private void ApplyCloudToDrawable(bool isCloud)
    {
        var combined = _controller?.CombinedDrawable;
        if (combined == null) return;

        switch (_controller.DrawMode)
        {
            case DrawMode.Rect: combined.RectDrawable.IsCloud = isCloud; break;
            case DrawMode.Oval: combined.OvalDrawable.IsCloud = isCloud; break;
            case DrawMode.Poly: combined.PolyDrawable.IsCloud = isCloud; break;
        }
        Redraw();
    }

    private bool _redrawPending;
    private void Redraw()
    {
        if (_redrawPending)
            return;

        _redrawPending = true;

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(16), () =>
        {
            _redrawPending = false;
            RedrawRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
