#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SnapDoc.Controls;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using static SnapDoc.Helper;

namespace SnapDoc.Views;

public partial class PopupIconEdit : Popup<string>, INotifyPropertyChanged
{
    // --- Konstanten fuer die Vorschau ---
    private const float CanvasPadding = 12f;   // Rand, damit das Kreuz bei Anchor 0/1 nicht abgeschnitten wird
    private const float CrossRadius = 11f;      // halbe Kreuzgroesse in DIP
    private const string BlinkAnimationName = "BlinkCross";
    private static readonly SKSamplingOptions IconSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);
    private bool _isDragging;
    private SKBitmap _iconBitmap;
    private float _blink = 1f;                 // 0..1, steuert die Helligkeit des Kreuzes
    private bool _suppressTextSync;            // verhindert Rueckkopplung Anchor <-> Entry
    private long? _activePointer;
    public IconItem iconItem;
    public int IconPreviewWidth { get; set; } = 120;
    public int IconPreviewHeight { get; set; }

    // --- AutoComplete / Kategorien ---
    private readonly List<string> _allCategories = [];
    public ObservableCollection<string> FilteredCategories { get; set; } = [];

    private bool _isCategorySuggestionsVisible;
    public bool IsCategorySuggestionsVisible
    {
        get => _isCategorySuggestionsVisible;
        set
        {
            if (_isCategorySuggestionsVisible != value)
            {
                _isCategorySuggestionsVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public PopupIconEdit(IconItem _iconItem)
    {
        InitializeComponent();

        iconItem = _iconItem;

        iconName.Text = iconItem.DisplayName;
        iconCategory.Text = iconItem.Category;

        _allCategories = SettingsService.Instance.IconCategories ?? [];
        FilteredCategories.Clear();
        IsCategorySuggestionsVisible = false;

        IconPreviewHeight = (int)(IconPreviewWidth * iconItem.IconSize.Height / iconItem.IconSize.Width);

        _ = LoadIconBitmapAsync();

        Anchor_X = iconItem.AnchorPoint.X;
        Anchor_Y = iconItem.AnchorPoint.Y;

        IconScale = iconItem.IconScale;
        allowRotate.IsToggled = iconItem.IsRotationLocked;
        allowAutoScale.IsToggled = iconItem.IsAutoScaleLocked;
        setDefault.IsToggled = iconItem.IsDefaultIcon;
        SelectedColor = new Color(iconItem.PinColor.Red, iconItem.PinColor.Green, iconItem.PinColor.Blue);

        if (iconItem.IsCustomIcon)
            deleteIcon.IsVisible = true;

        BindingContext = this;

        StartBlinking();
    }

    // ------------------------------------------------------------------
    //  Vorschau / SkiaSharp
    // ------------------------------------------------------------------

    private async Task LoadIconBitmapAsync()
    {
        _iconBitmap = await IconBitmapLoader.LoadAsync(iconItem.DisplayIconPath);
        iconCanvas?.InvalidateSurface();
    }

    private void OnPaintIconPreview(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        // DIP -> Pixel
        float scale = iconCanvas.Width > 0
            ? e.Info.Width / (float)iconCanvas.Width
            : 1f;

        float pad = CanvasPadding * scale;
        float imgW = IconPreviewWidth * scale;
        float imgH = IconPreviewHeight * scale;

        var dest = new SKRect(pad, pad, pad + imgW, pad + imgH);

        if (_iconBitmap != null)
            canvas.DrawBitmap(_iconBitmap, dest, IconSampling);

        // Anchor exakt im Bildkoordinatensystem
        float x = dest.Left + (float)Anchor_X * imgW;
        float y = dest.Top + (float)Anchor_Y * imgH;
        float r = CrossRadius * scale;

        // Halo, damit das Kreuz auf jedem Icon sichtbar bleibt
        using var halo = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3.5f * scale,
            Color = SKColors.White.WithAlpha(200),
            IsAntialias = true
        };
        DrawCross(canvas, x, y, r, halo);

        // Kreuz, blinkend
        byte v = (byte)(_blink * 255);
        using var cross = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f * scale,
            Color = new SKColor(v, v, v),
            IsAntialias = true
        };
        DrawCross(canvas, x, y, r, cross);
        canvas.DrawCircle(x, y, r * 0.45f, cross);
    }

    private static void DrawCross(SKCanvas c, float x, float y, float r, SKPaint p)
    {
        c.DrawLine(x - r, y, x + r, y, p);
        c.DrawLine(x, y - r, x, y + r, p);
    }

    private void StartBlinking()
    {
        var blink = new Animation
        {
            { 0, 0.5, new Animation(v => { _blink = (float)v; iconCanvas.InvalidateSurface(); }, 0, 1) },
            { 0.5, 1, new Animation(v => { _blink = (float)v; iconCanvas.InvalidateSurface(); }, 1, 0) }
        };

        blink.Commit(iconCanvas, BlinkAnimationName, length: 400, repeat: () => true);
    }

    private void Cleanup()
    {
        iconCanvas?.AbortAnimation(BlinkAnimationName);
        _iconBitmap?.Dispose();
        _iconBitmap = null;
    }

    // ------------------------------------------------------------------
    //  Anchor
    // ------------------------------------------------------------------

    private double anchor_X;
    public double Anchor_X
    {
        get => anchor_X;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);
            if (anchor_X == clamped)
                return;

            anchor_X = clamped;
            OnPropertyChanged();

            if (!_suppressTextSync)
                SetTextSilently(nameof(Anchor_X_Text), ref anchor_X_Text, Format(clamped));

            iconCanvas?.InvalidateSurface();
        }
    }

    private double anchor_Y;
    public double Anchor_Y
    {
        get => anchor_Y;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);
            if (anchor_Y == clamped)
                return;

            anchor_Y = clamped;
            OnPropertyChanged();

            if (!_suppressTextSync)
                SetTextSilently(nameof(Anchor_Y_Text), ref anchor_Y_Text, Format(clamped));

            iconCanvas?.InvalidateSurface();
        }
    }

    private string anchor_X_Text;
    public string Anchor_X_Text
    {
        get => anchor_X_Text;
        set
        {
            if (anchor_X_Text == value)
                return;

            anchor_X_Text = value;
            OnPropertyChanged();

            if (TryParseAnchor(value, out double parsed))
            {
                _suppressTextSync = true;
                Anchor_X = parsed;
                _suppressTextSync = false;
            }
        }
    }

    private string anchor_Y_Text;
    public string Anchor_Y_Text
    {
        get => anchor_Y_Text;
        set
        {
            if (anchor_Y_Text == value)
                return;

            anchor_Y_Text = value;
            OnPropertyChanged();

            if (TryParseAnchor(value, out double parsed))
            {
                _suppressTextSync = true;
                Anchor_Y = parsed;
                _suppressTextSync = false;
            }
        }
    }

    private static bool TryParseAnchor(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string Format(double value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);

    private void SetTextSilently(string propertyName, ref string field, string value)
    {
        if (field == value)
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    // ------------------------------------------------------------------
    //  Uebrige Eigenschaften
    // ------------------------------------------------------------------

    private Color selectedColor;
    public Color SelectedColor
    {
        get => selectedColor;
        set
        {
            if (selectedColor != value)
            {
                selectedColor = value;
                OnPropertyChanged();
            }
        }
    }

    private double iconScale;
    public double IconScale
    {
        get => iconScale;
        set
        {
            if (iconScale != value)
            {
                iconScale = value;
                OnPropertyChanged();
            }
        }
    }

    // ------------------------------------------------------------------
    //  AutoComplete Kategorie
    // ------------------------------------------------------------------

    private void OnCategoryTextChanged(object sender, TextChangedEventArgs e)
    {
        var keyword = e.NewTextValue;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            FilteredCategories.Clear();
            IsCategorySuggestionsVisible = false;
            return;
        }

        FilteredCategories.Clear();
        foreach (var item in _allCategories.Where(x => x.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            FilteredCategories.Add(item);

        IsCategorySuggestionsVisible = FilteredCategories.Count > 0;
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is string selectedCategory)
        {
            iconCategory.Text = selectedCategory;
            IsCategorySuggestionsVisible = false;

            if (sender is CollectionView cv)
                cv.SelectedItem = null;
        }
    }

    // ------------------------------------------------------------------
    //  Farbwahl
    // ------------------------------------------------------------------

    private async void OnColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedColor);
        var result = await Shell.Current.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedColor = Color.FromArgb(result.Result.ColorHex);
    }

    // ------------------------------------------------------------------
    //  Schliessen
    // ------------------------------------------------------------------

    private async void OnOkClicked(object sender, EventArgs e)
    {
        var file = Path.GetFileName(iconItem.FileName);
        string returnValue = null;

        if (!deleteIcon.IsToggled)
        {
            var updatedItem = new IconItem(
                file,
                iconName.Text,
                new Point(Anchor_X, Anchor_Y),
                iconItem.IconSize,
                allowRotate.IsToggled,
                allowAutoScale.IsToggled,
                iconItem.IsCustomIcon,
                new SKColor((byte)(SelectedColor.Red * 255), (byte)(SelectedColor.Green * 255), (byte)(SelectedColor.Blue * 255)),
                IconScale,
                iconCategory.Text,
                SettingsService.Instance.DefaultPinIcon == file
            );

            Helper.UpdateIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), updatedItem);
            IconLookup.AddOrUpdate(updatedItem);
            returnValue = file;

            if (updatedItem.IsCustomIcon)
                CustomIconSync.Upload(updatedItem);

            if (setDefault.IsToggled)
            {
                SettingsService.Instance.DefaultPinIcon = file;
                SettingsService.Instance.SaveSettings();
            }
        }
        else
        {
            var iconFile = Path.Combine(Settings.DataDirectory, "customicons", file);

            if (File.Exists(iconFile))
            {
                if (iconItem.IsDefaultIcon)
                {
                    SettingsService.Instance.DefaultPinIcon = Settings.IconData.FirstOrDefault().FileName;
                    SettingsService.Instance.SaveSettings();
                }

                File.Delete(iconFile);
                Helper.DeleteIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), file);
                IconLookup.Remove(file);
                _ = SaveManager.DeleteCloudFileAsync($"{ProjectItem.Current.CustomIconsFolder}/{file}");
                _ = SaveManager.DeleteCloudFileAsync($"{ProjectItem.Current.CustomIconsFolder}/{Path.ChangeExtension(file, ".json")}");
                returnValue = "deleted";
            }
        }

        Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
        SettingsService.Instance.IconCategories = iconCategories;
        IconLookup.Initialize(Settings.IconData);

        Cleanup();

        try { await CloseAsync(returnValue); }
        catch (InvalidOperationException) { }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        Cleanup();

        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    private void OnCanvasTouch(object sender, SKTouchEventArgs e)
    {
        float scale = iconCanvas.Width > 0
        ? (float)(iconCanvas.CanvasSize.Width / iconCanvas.Width)
        : 1f;

        float pad = CanvasPadding * scale;
        float imgW = IconPreviewWidth * scale;
        float imgH = IconPreviewHeight * scale;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                if (_activePointer != null) break;
                _activePointer = e.Id;
                _isDragging = true;
                UpdateAnchorFromTouch(e.Location, pad, imgW, imgH);
                break;

            case SKTouchAction.Moved:
                if (_isDragging && e.Id == _activePointer)
                    UpdateAnchorFromTouch(e.Location, pad, imgW, imgH);
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _isDragging = false;
                _activePointer = null;
                break;
        }

        e.Handled = true;
    }

    private void UpdateAnchorFromTouch(SKPoint p, float pad, float imgW, float imgH)
    {
        Anchor_X = Math.Round((p.X - pad) / imgW, 2);
        Anchor_Y = Math.Round((p.Y - pad) / imgH, 2);
    }

    // ------------------------------------------------------------------
    //  INotifyPropertyChanged
    // ------------------------------------------------------------------

    public new event PropertyChangedEventHandler PropertyChanged;

    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
