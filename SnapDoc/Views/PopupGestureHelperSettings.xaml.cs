#nullable disable
using SnapDoc.Resources.Languages;
using System.ComponentModel;
using SnapDoc.Services;
using SkiaSharp;

namespace SnapDoc.Views;

public partial class PopupGestureHelperSettings : INotifyPropertyChanged
{
    private readonly float _initialLoupeRadius;
    private readonly float _initialZoomFactor;

    private int touchLiftX;
    public int TouchLiftX
    {
        get => touchLiftX;
        set
        {
            if (touchLiftX == value) return;
            touchLiftX = value;
            OnPropertyChanged();
        }
    }

    private int touchLiftY;
    public int TouchLiftY
    {
        get => touchLiftY;
        set
        {
            if (touchLiftY == value) return;
            touchLiftY = value;
            OnPropertyChanged();
        }
    }

    private string touchLiftXText;
    public string TouchLiftXText
    {
        get => touchLiftXText;
        set
        {
            if (touchLiftXText == value)
                return;

            touchLiftXText = value;
            OnPropertyChanged();
        }
    }

    private string touchLiftYText;
    public string TouchLiftYText
    {
        get => touchLiftYText;
        set
        {
            if (touchLiftYText == value)
                return;

            touchLiftYText = value;
            OnPropertyChanged();
        }
    }

    public PopupGestureHelperSettings(
        string okText = null,
        string cancelText = null)
    {
        InitializeComponent();

        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;

        _initialLoupeRadius = SettingsService.Instance.LoupeRadius;
        _initialZoomFactor = SettingsService.Instance.LoupeZoomFactor;

        var touchLift = SettingsService.Instance.TouchLiftDp;

        TouchLiftXText = ((int)Math.Round(touchLift.X)).ToString();
        TouchLiftYText = ((int)Math.Round(touchLift.Y)).ToString();

        BindingContext = this;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        if (!int.TryParse(TouchLiftXText, out int x))
            x = 0;

        if (!int.TryParse(TouchLiftYText, out int y))
            y = -40;

        x = Math.Clamp(x, -100, 100);
        y = Math.Clamp(y, -100, 100);

        SettingsService.Instance.TouchLiftDp = new SKPoint(x, y);

        try { await CloseAsync(); }
        catch (InvalidOperationException) { }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.LoupeRadius = _initialLoupeRadius;
        SettingsService.Instance.LoupeZoomFactor = _initialZoomFactor;

        try { await CloseAsync(); }
        catch (InvalidOperationException) { }
    }
}