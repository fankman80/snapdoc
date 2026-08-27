#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.DrawingTool;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SnapDoc.Views;

public partial class PopupStyleEditor : Popup<PopupStyleReturn>, INotifyPropertyChanged
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();

    public ObservableCollection<StylePickerItem> Items { get; } = new ObservableCollection<StylePickerItem>(SettingsService.Instance.StyleTemplateItems);

    private Color selectedFillColor;
    public Color SelectedFillColor
    {
        get => selectedFillColor;
        set
        {
            if (selectedFillColor != value)
            {
                selectedFillColor = value;
                OnPropertyChanged();
            }
        }
    }

    private Color selectedBorderColor;
    public Color SelectedBorderColor
    {
        get => selectedBorderColor;
        set
        {
            if (selectedBorderColor != value)
            {
                selectedBorderColor = value;
                OnPropertyChanged();
            }
        }
    }

    private Color selectedTextColor;
    public Color SelectedTextColor
    {
        get => selectedTextColor;
        set
        {
            if (selectedTextColor != value)
            {
                selectedTextColor = value;
                OnPropertyChanged();
            }
        }
    }

    private int lineWidth;
    public int LineWidth
    {
        get => lineWidth;
        set
        {
            if (lineWidth != value)
            {
                lineWidth = value;
                OnPropertyChanged();
            }
        }
    }

    private string strokeStyle;
    public string StrokeStyle
    {
        get => strokeStyle;
        set
        {
            if (strokeStyle != value)
            {
                strokeStyle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StrokeDashArray));
            }
        }
    }

    private string templateText;
    public string TemplateText
    {
        get => templateText;
        set
        {
            if (templateText != value)
            {
                templateText = value;
                OnPropertyChanged();
            }
        }
    }

    public double[] StrokeDashArray
    {
        get
        {
            if (string.IsNullOrWhiteSpace(StrokeStyle))
                return [];

            return Helper.ParseDashArray(StrokeStyle)?
                .Select(f => (double)f)
                .ToArray()
                ?? [];
        }
    }

    private float cloudRadius;
    public float CloudRadius
    {
        get => cloudRadius;
        set
        {
            if (cloudRadius != value)
            {
                cloudRadius = value;
                OnPropertyChanged();
            }
        }
    }

    private float cloudOverlap;
    public float CloudOverlap
    {
        get => cloudOverlap;
        set
        {
            if (cloudOverlap != value)
            {
                cloudOverlap = value;
                OnPropertyChanged();
            }
        }
    }

    private float cloudInciseDeg;
    public float CloudInciseDeg
    {
        get => cloudInciseDeg;
        set
        {
            if (cloudInciseDeg != value)
            {
                cloudInciseDeg = value;
                OnPropertyChanged();
            }
        }
    }

    private bool isCloudSettingsVisible;
    public bool IsCloudSettingsVisible
    {
        get => isCloudSettingsVisible;
        set
        {
            if (isCloudSettingsVisible != value)
            {
                isCloudSettingsVisible = value;
                OnPropertyChanged();
            }
        }
    }

    private bool isHatchEffect;
    public bool IsHatchEffect
    {
        get => isHatchEffect;
        set
        {
            if (isHatchEffect != value)
            {
                isHatchEffect = value;
                OnPropertyChanged();
            }
        }
    }

    private float hatchStrokeWitdh;
    public float HatchStrokeWitdh
    {
        get => hatchStrokeWitdh;
        set
        {
            if (hatchStrokeWitdh != value)
            {
                hatchStrokeWitdh = value;
                OnPropertyChanged();
            }
        }
    }

    private float hatchStrokeSpace;
    public float HatchStrokeSpace
    {
        get => hatchStrokeSpace;
        set
        {
            if (hatchStrokeSpace != value)
            {
                hatchStrokeSpace = value;
                OnPropertyChanged();
            }
        }
    }

    private float hatchRotation;
    public float HatchRotation
    {
        get => hatchRotation;
        set
        {
            if (hatchRotation != value)
            {
                hatchRotation = value;
                OnPropertyChanged();
            }
        }
    }

    public PopupStyleEditor(int lineWidth, string borderColor, string fillColor, string textColor, string strokeStyle, bool isHatchEffect, float hatchStrokeWitdh, float hatchStrokeSpace, float hatchRotation, float cloudRadius, float cloudInciseDeg, bool isCloudSettingsVisible = false, string okText = null, string cancelText = null)
    {
        InitializeComponent();

        BindingContext = this;

        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        LineWidth = lineWidth;
        StrokeStyle = strokeStyle;
        IsHatchEffect = isHatchEffect;
        HatchStrokeWitdh = hatchStrokeWitdh;
        HatchStrokeSpace = hatchStrokeSpace;
        HatchRotation = hatchRotation;
        TemplateText = "Text";
        CloudRadius = cloudRadius;
        CloudInciseDeg = cloudInciseDeg;
        IsCloudSettingsVisible = isCloudSettingsVisible;

        SelectedBorderColor = Color.FromArgb(borderColor);
        SelectedFillColor = Color.FromArgb(fillColor);
        SelectedTextColor = Color.FromArgb(textColor);
    }

    private void OnPreviewPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var width = e.Info.Width;
        var height = e.Info.Height;

        if (width <= 0 || height <= 0) return;

        var rectDrawable = new InteractiveRectangleDrawable
        {
            FillColor = SelectedFillColor.ToSKColor(),
            LineColor = SelectedBorderColor.ToSKColor(),
            TextColor = SelectedTextColor.ToSKColor(),

            LineThickness = (float)LineWidth,
            StrokeStyle = StrokeStyle ?? "",

            IsHatchEffect = IsHatchEffect,
            HatchRotation = (float)HatchRotation,
            HatchStrokeWitdh = (float)HatchStrokeWitdh,
            HatchStrokeSpace = (float)HatchStrokeSpace,

            IsCloud = false,
            CloudRadius = (float)CloudRadius,
            CloudInciseDeg = (float)CloudInciseDeg,
            DisplayHandles = false,
            IsDrawn = true
        };

        float halfStroke = (float)LineWidth / 2f;
        rectDrawable.SetFromDrag(
            new SKPoint(halfStroke, halfStroke),
            new SKPoint(width - halfStroke, height - halfStroke)
        );

        rectDrawable.Draw(canvas);
    }

    private void OnListItemPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (sender is SKCanvasView canvasView && canvasView.BindingContext is StylePickerItem item)
        {
            var width = e.Info.Width;
            var height = e.Info.Height;

            if (width <= 0 || height <= 0) return;

            var rectDrawable = new InteractiveRectangleDrawable
            {
                FillColor = Color.FromArgb(item.BackgroundColor).ToSKColor(),
                LineColor = Color.FromArgb(item.BorderColor).ToSKColor(),
                TextColor = Color.FromArgb(item.TextColor).ToSKColor(),

                LineThickness = item.LineWidth,
                StrokeStyle = item.StrokeStyle ?? "",

                IsHatchEffect = item.IsHatchEffect,
                HatchRotation = item.HatchRotation,
                HatchStrokeWitdh = item.HatchStrokeWitdh,
                HatchStrokeSpace = item.HatchStrokeSpace,
                DisplayHandles = false,
                IsDrawn = true
            };

            float halfStroke = (float)LineWidth / 2f;
            rectDrawable.SetFromDrag(
                new SKPoint(halfStroke, halfStroke),
                new SKPoint(width - halfStroke, height - halfStroke)
            );

            rectDrawable.Draw(canvas);
        }
    }

    private async void OnTemplateClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        StylePickerItem item = (StylePickerItem)button.BindingContext;

        SelectedFillColor = Color.FromArgb(item.BackgroundColor);
        SelectedBorderColor = Color.FromArgb(item.BorderColor);
        SelectedTextColor = Color.FromArgb(item.TextColor);
        LineWidth = item.LineWidth;
        StrokeStyle = item.StrokeStyle;
        IsHatchEffect = item.IsHatchEffect;
        HatchStrokeWitdh = item.HatchStrokeWitdh;
        HatchStrokeSpace = item.HatchStrokeSpace;
        HatchRotation = item.HatchRotation;
        TemplateText = item.Text;

        TemplatesExpander.IsExpanded = false;
    }

    private async void OnTemplateDeleteClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        StylePickerItem item = (StylePickerItem)button.BindingContext;

        var popup = new PopupDualResponse(AppResources.wollen_sie_diese_vorlage_wirklich_loeschen);
        var result = await Shell.Current.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
        if (result.Result is DualPopupResult.Ok)
        {
            Items.Remove(item);
            SettingsService.Instance.StyleTemplateItems = [.. Items];
            SettingsService.Instance.SaveSettings();
        }
    }

    private async void OnTemplateAddClicked(object sender, EventArgs e)
    {
        StylePickerItem item = new()
        {
            Text = TemplateText,
            BackgroundColor = SelectedFillColor.ToArgbHex(),
            BorderColor = SelectedBorderColor.ToArgbHex(),
            TextColor = SelectedTextColor.ToArgbHex(),
            LineWidth = LineWidth,
            StrokeStyle = StrokeStyle,
            IsHatchEffect = IsHatchEffect,
            HatchStrokeWitdh = HatchStrokeWitdh,
            HatchStrokeSpace = HatchStrokeSpace,
            HatchRotation = HatchRotation
        };

        Items.Add(item);

        SettingsService.Instance.StyleTemplateItems = [.. Items];
        SettingsService.Instance.SaveSettings();
    }

    private async void OnBorderColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedBorderColor, fillOpacity: (byte)(SelectedBorderColor.Alpha * 255), fillOpacityVisibility: true);
        var result = await Shell.Current.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedBorderColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(1f / 255f * result.Result.FillOpacity);
    }

    private async void OnFillColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedFillColor, fillOpacity: (byte)(SelectedFillColor.Alpha * 255), fillOpacityVisibility: true);
        var result = await Shell.Current.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedFillColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(1f / 255f * result.Result.FillOpacity);
    }

    private async void OnTextColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedTextColor, fillOpacity: (byte)(SelectedTextColor.Alpha * 255), fillOpacityVisibility: true);
        var result = await Shell.Current.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedTextColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(1f / 255f * result.Result.FillOpacity);
    }

    private void OnStrokeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (e.NewTextValue == null) return;

        var filtered = string.Concat(e.NewTextValue.Where(c => char.IsDigit(c) || c == ' '));
        StrokeStyle = MultipleSpacesRegex().Replace(filtered, " ");
    }

    private void FlipRotationAngleClick(object sender, EventArgs e)
    {
        HatchRotation *= -1;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(new PopupStyleReturn(SelectedBorderColor.ToArgbHex(), SelectedFillColor.ToArgbHex(), SelectedTextColor.ToArgbHex(), LineWidth, StrokeStyle, IsHatchEffect, HatchStrokeWitdh, HatchStrokeSpace, HatchRotation, CloudRadius, CloudInciseDeg)); }
        catch (InvalidOperationException) { }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    public new event PropertyChangedEventHandler PropertyChanged;

    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PreviewCanvas?.InvalidateSurface();
    }
}
