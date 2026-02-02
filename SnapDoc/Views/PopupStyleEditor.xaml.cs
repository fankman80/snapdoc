#nullable disable

using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Views;

public partial class PopupStyleEditor : Popup<PopupStyleReturn>, INotifyPropertyChanged
{
    public List<StylePickerItem> Items { get; } = SettingsService.Instance.StyleTemplateItems;

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
            }
        }
    }

    public PopupStyleEditor(int lineWidth, string borderColor, string fillColor, string textColor, string strokeStyle, string okText = null, string cancelText = null)
    {
        InitializeComponent();

        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        LineWidth = lineWidth;
        StrokeStyle = strokeStyle;

        SelectedBorderColor = Color.FromArgb(borderColor);
        SelectedFillColor = Color.FromArgb(fillColor);
        SelectedTextColor = Color.FromArgb(textColor);

        BindingContext = this;
    }

    private async void OnItemClicked(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is StylePickerItem item)
        {
            SelectedFillColor = Color.FromArgb(item.BackgroundColor);
            SelectedBorderColor = Color.FromArgb(item.BorderColor);
            SelectedTextColor = Color.FromArgb(item.TextColor);
            LineWidth = item.LineWidth;
            StrokeStyle = item.StrokeStyle;
        }
    }

    private async void OnBorderColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedBorderColor, fillOpacity: (byte)(SelectedBorderColor.Alpha * 255), fillOpacityVisibility: true);
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedBorderColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(1f / 255f * result.Result.FillOpacity);
    }

    private async void OnFillColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedFillColor, fillOpacity: (byte)(SelectedFillColor.Alpha * 255), fillOpacityVisibility: true);
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedFillColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(1f / 255f * result.Result.FillOpacity);
    }

    private async void OnTextColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedTextColor, fillOpacity: (byte)(SelectedTextColor.Alpha * 255), fillOpacityVisibility: true);
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedTextColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(1f / 255f * result.Result.FillOpacity);
    }

    private void OnStrokeTextChanged(object sender, TextChangedEventArgs e)
    {
        StrokeStyle = string.Concat(e.NewTextValue.Where(c => char.IsDigit(c) || c == ' '));
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PopupStyleReturn(SelectedBorderColor.ToArgbHex(), SelectedFillColor.ToArgbHex(), SelectedTextColor.ToArgbHex(), LineWidth, StrokeStyle));
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}