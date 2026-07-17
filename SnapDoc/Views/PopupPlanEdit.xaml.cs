#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using SnapDoc.Controls;
using SnapDoc.Resources.Languages;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Views;

public partial class PopupPlanEdit : Popup<PlanEditReturn>, INotifyPropertyChanged
{
    private bool? lockAction = null;
    public PopupPlanEdit(string name, string desc, bool gray, string planColor, bool export = true, string okText = null, string cancelText = null)
    {
        InitializeComponent();
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        name_entry.Text = name;
        desc_entry.Text = desc;
        allow_export.IsToggled = export;
        SelectedColor = String.IsNullOrEmpty(planColor) ? Colors.White : Color.FromArgb(planColor);

        if (gray)
            grayscaleButtonText.Text = AppResources.farben_hinzufuegen;
        else
            grayscaleButtonText.Text = AppResources.farben_entfernen;

        BindingContext = this;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(new PlanEditReturn(name_entry.Text, desc_entry.Text, allow_export.IsToggled, PlanRotate, SelectedColor.ToArgbHex(), lockAction)); }
        catch (InvalidOperationException) { }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_diesen_plan_wirklich_loeschen, okText: AppResources.loeschen, alert: true);
        var result = await Shell.Current.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        try { await CloseAsync(new PlanEditReturn("Delete", null, true, PlanRotate, SelectedColor.ToArgbHex(), lockAction)); }
        catch (InvalidOperationException) { }
    }

    private async void OnGrayscaleClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(new PlanEditReturn("Grayscale", null, true, PlanRotate, SelectedColor.ToArgbHex(), lockAction)); }
        catch (InvalidOperationException) { }
    }

    private async void OnColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedColor, fillOpacity: (byte)(SelectedColor.Alpha * 255), fillOpacityVisibility: true);
        var result = await Shell.Current.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        SelectedColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(1.0f / 255.0f * result.Result.FillOpacity);
    }

    private void PlanRotateLeft(object sender, EventArgs e)
    {
        PlanRotate -= 90;

        if (PlanRotate < 0)
            PlanRotate = 360 + PlanRotate;
    }

    private void PlanRotateRight(object sender, EventArgs e)
    {
        PlanRotate += 90;

        if (PlanRotate > 270)
            PlanRotate = 0;
    }

    private async void OnLockedClicked(object sender, EventArgs e)
    {
        lockAction = true;
        await SnackbarExtensions.ShowSafeAsync(AppResources.alle_pins_gesperrt, includeDelay: true);
    }

    private async void OnUnlockedClicked(object sender, EventArgs e)
    {
        lockAction = false;
        await SnackbarExtensions.ShowSafeAsync(AppResources.alle_pins_entsperrt, includeDelay: true);
    }

    private int _planRotate = 0;
    public int PlanRotate
    {
        get => _planRotate;
        set
        {
            if (_planRotate != value)
            {
                _planRotate = value;
                OnPropertyChanged(nameof(PlanRotate));
            }
        }
    }

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

    public new event PropertyChangedEventHandler PropertyChanged;
    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
