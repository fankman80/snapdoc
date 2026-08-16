#nullable disable
using SnapDoc.Resources.Languages;
using System.ComponentModel;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class PopupLoupeSettings : INotifyPropertyChanged
{
    private readonly float _initialLoupeRadius;
    private readonly float _initialZoomFactor;

    public PopupLoupeSettings(string okText = null, string cancelText = null)
    {
        InitializeComponent();
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;

        _initialLoupeRadius = SettingsService.Instance.LoupeRadius;
        _initialZoomFactor = SettingsService.Instance.LoupeZoomFactor;

        BindingContext = this;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
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