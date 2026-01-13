#nullable disable

using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;
using SnapDoc.Resources.Languages;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Views;

public partial class PopupTextEdit : Popup<TextEditReturn>, INotifyPropertyChanged
{
    private float fontSize;
    public float FontSize
    {
        get => fontSize;
        set
        {
            if (fontSize != value)
            {
                fontSize = value;
                OnPropertyChanged();
            }
        }
    }

    private RectangleTextAlignment fontAlignment;
    public RectangleTextAlignment FontAlignment
    {
        get => fontAlignment;
        set
        {
            if (fontAlignment != value)
            {
                fontAlignment = value;
                OnPropertyChanged();
            }
        }
    }

    private RectangleTextStyle textStyle;
    public RectangleTextStyle TextStyle
    {
        get => textStyle;
        set
        {
            if (textStyle != value)
            {
                textStyle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBold));
                OnPropertyChanged(nameof(IsItalic));
            }
        }
    }

    public bool IsBold => TextStyle.HasFlag(RectangleTextStyle.Bold);
    public bool IsItalic => TextStyle.HasFlag(RectangleTextStyle.Italic);


    private bool autoSize;
    public bool AutoSize
    {
        get => autoSize;
        set
        {
            if (autoSize != value)
            {
                autoSize = value;
                OnPropertyChanged();
            }
        }
    }

    private string inputTxt;
    public string InputTxt
    {
        get => inputTxt;
        set
        {
            if (inputTxt != value)
            {
                inputTxt = value;
                OnPropertyChanged();
            }
        }
    }

    public PopupTextEdit(float fontSize = 24, RectangleTextAlignment fontAlignment = RectangleTextAlignment.Center, RectangleTextStyle textStyle  = RectangleTextStyle.Normal, bool autoSize = false, string inputTxt = "", string okText = null, string cancelText = null)
    {
        InitializeComponent();
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        FontSize = fontSize;
        FontAlignment = fontAlignment;
        TextStyle = textStyle;
        AutoSize = autoSize;
        InputTxt = inputTxt;

        BindingContext = this;
    }

    [RelayCommand]
    private void SetAlignment(RectangleTextAlignment alignment)
    {
        FontAlignment = alignment;
    }

    [RelayCommand]
    private void ToggleTextStyle(RectangleTextStyle style)
    {
 
        TextStyle ^= style;

        if (TextStyle == 0)
            TextStyle = RectangleTextStyle.Normal;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(new TextEditReturn(FontSize, FontAlignment, TextStyle, AutoSize, InputTxt));
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