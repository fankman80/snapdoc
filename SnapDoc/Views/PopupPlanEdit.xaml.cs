#nullable disable

using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Views;

public partial class PopupPlanEdit : Popup<PlanEditReturn>, INotifyPropertyChanged
{
    public PopupPlanEdit(string name, string desc, bool gray, string planColor, bool export = true, string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
        name_entry.Text = name;
        desc_entry.Text = desc;
        allow_export.IsToggled = export;
        SelectedColor = String.IsNullOrEmpty(planColor) ? Colors.White : Color.FromArgb(planColor);

        if (gray)
            grayscaleButtonText.Text = "Farben hinzufügen";
        else
            grayscaleButtonText.Text = "Farben entfernen";

        BindingContext = this;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PlanEditReturn(name_entry.Text, desc_entry.Text, allow_export.IsToggled, PlanRotate, SelectedColor.ToArgbHex()));
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PlanEditReturn(PopupReturn.Delete, null, true, PlanRotate, SelectedColor.ToArgbHex()));
    }

    private async void OnGrayscaleClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PlanEditReturn(PopupReturn.Grayscale, null, true, PlanRotate, SelectedColor.ToArgbHex()));
    }

    private async void OnColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(0, SelectedColor, fillOpacity: (byte)(SelectedColor.Alpha * 255), lineWidthVisibility: false, fillOpacityVisibility: true);
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedColor = Color.FromArgb(result.Result.PenColorHex).WithAlpha(1f / 255f * result.Result.FillOpacity);
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
