#nullable disable
using CommunityToolkit.Maui.Extensions;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.DrawingTool;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.Views;

namespace SnapDoc.Controls;

public partial class StyleTemplateEditor : ContentView
{
    public event EventHandler<StylePickerItem> TemplateSelected;

    private StylePickerItem _current;
    private const float PreviewMargin = 4f;

    public StyleTemplateEditor()
    {
        InitializeComponent();
    }

    public void Load(StylePickerItem currentStyle)
    {
        _current = currentStyle;
        NameEntry.Text = "";

        Rebuild();
    }

    private static List<StylePickerItem> LoadItems() =>
        SettingsService.Instance.StyleTemplateItems?.ToList() ?? [];

    private static void SaveItems(List<StylePickerItem> items)
    {
        SettingsService.Instance.StyleTemplateItems = [.. items];
        SettingsService.Instance.SaveSettings();
    }

    private void Rebuild()
    {
        TemplateList.Children.Clear();

        var items = LoadItems();

        if (items.Count == 0)
        {
            TemplateList.Children.Add(new Label
            {
                Text = "Noch keine Vorlagen vorhanden.",
                TextColor = Colors.Gray,
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 8)
            });

            return;
        }

        foreach (var item in items)
            TemplateList.Children.Add(CreateRow(item));
    }

    private Grid CreateRow(StylePickerItem item)
    {
        var preview = new SKCanvasView
        {
            IgnorePixelScaling = false,
            InputTransparent = true
        };

        preview.PaintSurface += (_, e) => DrawPreview(e, item);

        var label = new Label
        {
            Text = item.Text,
            TextColor = Color.FromArgb(item.TextColor),
            FontSize = 14,
            FontAttributes = GetFontAttributes(item.TextStyle),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            InputTransparent = true
        };

        var cell = new Grid
        {
            HeightRequest = 52,
            Children =
        {
            preview,
            label
        }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
            TemplateSelected?.Invoke(this, item);

        cell.GestureRecognizers.Add(tap);

        var delete = new Button
        {
            Text = MaterialIcons.Delete,
            FontFamily = "MaterialOutlined",
            FontSize = 24,
            TextColor = ColorSwatchBar.Accent,
            BackgroundColor = Colors.Transparent,
            Padding = 0,
            WidthRequest = 44,
            HeightRequest = 44
        };

        delete.Clicked += async (_, _) =>
            await DeleteAsync(item);

        var row = new Grid
        {
            ColumnDefinitions =
        {
            new(GridLength.Star),
            new(GridLength.Auto)
        },
            ColumnSpacing = 8
        };

        row.Add(cell, 0);
        row.Add(delete, 1);

        return row;
    }

    private static FontAttributes GetFontAttributes(RectangleTextStyle style)
    {
        FontAttributes attributes = FontAttributes.None;

        if (style.HasFlag(RectangleTextStyle.Bold))
            attributes |= FontAttributes.Bold;

        if (style.HasFlag(RectangleTextStyle.Italic))
            attributes |= FontAttributes.Italic;

        return attributes;
    }

    private static void DrawPreview(SKPaintSurfaceEventArgs e, StylePickerItem item)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float w = e.Info.Width;
        float h = e.Info.Height;

        if (w <= 0 || h <= 0)
            return;

        DrawCheckerboard(canvas, w, h);

        float density = (float)Settings.DisplayDensity;
        float strokeWidth = item.LineWidth * density;
        float halfStroke = strokeWidth / 2f;

        if (item.IsCloud)
        {
            DrawCloudPreview(canvas, w, h, item, density, halfStroke);
            return;
        }

        var d = CreatePreviewDrawable(item);

        float padding = halfStroke + PreviewMargin;

        d.SetFromDrag(
            new SKPoint(padding, padding),
            new SKPoint(w - padding, h - padding));

        d.Draw(canvas);
    }

    private static void DrawCheckerboard(SKCanvas canvas, float width, float height)
    {
        const float size = 8f;

        using var lightPaint = new SKPaint
        {
            Color = new SKColor(245, 245, 245),
            Style = SKPaintStyle.Fill
        };

        using var darkPaint = new SKPaint
        {
            Color = new SKColor(220, 220, 220),
            Style = SKPaintStyle.Fill
        };

        canvas.DrawRect(0, 0, width, height, lightPaint);

        int columns = (int)MathF.Ceiling(width / size);
        int rows = (int)MathF.Ceiling(height / size);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if ((x + y) % 2 == 0)
                    continue;

                canvas.DrawRect(
                    x * size,
                    y * size,
                    size,
                    size,
                    darkPaint);
            }
        }
    }

    private static void DrawCloudPreview(SKCanvas canvas, float width, float height, StylePickerItem item, float density, float halfStroke)
    {
        var d = CreatePreviewDrawable(item);

        d.IsCloud = true;
        float radiusPx = height * 0.12f;
        d.CloudRadius = radiusPx / density;
        float padding = radiusPx + halfStroke + PreviewMargin;

        d.SetFromDrag(
            new SKPoint(padding, padding),
            new SKPoint(
                width - padding,
                height - padding));

        d.Draw(canvas);
    }

    private static InteractiveRectangleDrawable CreatePreviewDrawable(StylePickerItem item)
    {
        return new InteractiveRectangleDrawable
        {
            FillColor =
                Color.FromArgb(item.BackgroundColor).ToSKColor(),

            LineColor =
                Color.FromArgb(item.BorderColor).ToSKColor(),

            TextColor =
                Color.FromArgb(item.TextColor).ToSKColor(),

            LineThickness = item.LineWidth,
            StrokeStyle = item.StrokeStyle ?? "",

            IsHatchEffect = item.IsHatchEffect,
            HatchStrokeWidth = item.HatchStrokeWidth,
            HatchStrokeSpace = item.HatchStrokeSpace,
            HatchRotation = item.HatchRotation,

            // Wird für die Wolkenvorschau überschrieben
            IsCloud = false,

            DisplayHandles = false,
            IsDrawn = true,

            // Kein Text im Drawable.
            // Der Template-Name bleibt das MAUI-Label.
            Text = ""
        };
    }

    private void OnAddClicked(object sender, EventArgs e)
    {
        if (_current == null)
            return;

        var items = LoadItems();

        string name =
            string.IsNullOrWhiteSpace(NameEntry.Text)
                ? $"{AppResources.vorlagen} {items.Count + 1}"
                : NameEntry.Text.Trim();

        items.Add(new StylePickerItem
        {
            Text = name,

            BackgroundColor = _current.BackgroundColor,
            BorderColor = _current.BorderColor,
            TextColor = _current.TextColor,

            LineWidth = _current.LineWidth,
            StrokeStyle = _current.StrokeStyle,

            IsHatchEffect = _current.IsHatchEffect,
            HatchStrokeWidth = _current.HatchStrokeWidth,
            HatchStrokeSpace = _current.HatchStrokeSpace,
            HatchRotation = _current.HatchRotation,

            IsCloud = _current.IsCloud,
            CloudRadius = _current.CloudRadius,
            CloudInciseDeg = _current.CloudInciseDeg,

            TextStyle = _current.TextStyle
        });

        SaveItems(items);

        NameEntry.Text = "";
        NameEntry.Unfocus();

        Rebuild();
    }

    private async Task DeleteAsync(StylePickerItem item)
    {
        var popup = new PopupDualResponse(
            AppResources.wollen_sie_diese_vorlage_wirklich_loeschen);

        var result = await PopupTracker.RunAsync(() =>
            Shell.Current.ShowPopupAsync<DualPopupResult>(
                popup,
                Settings.PopupOptions));

        if (result?.Result is not DualPopupResult.Ok)
            return;

        var items = LoadItems();

        items.Remove(item);

        SaveItems(items);
        Rebuild();
    }
}