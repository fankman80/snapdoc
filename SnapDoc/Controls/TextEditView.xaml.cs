#nullable disable
using CommunityToolkit.Mvvm.Input;
using System.Runtime.CompilerServices;

namespace SnapDoc.Controls;

public partial class TextEditView : ContentView
{
    public event EventHandler TextChanged;

    private bool _loading;

    public TextEditView()
    {
        InitializeComponent();
        Root.BindingContext = this;
    }

    public void Load(float fontSize, RectangleTextAlignment fontAlignment, RectangleTextStyle textStyle,
                     bool autoSize, string inputTxt, int textPadding)
    {
        _loading = true;
        try
        {
            FontSize = fontSize;
            FontAlignment = fontAlignment;
            TextStyle = textStyle;
            AutoSize = autoSize;
            InputTxt = inputTxt ?? string.Empty;
            TextPadding = textPadding;
        }
        finally { _loading = false; }
    }

    public void FocusInput() => TextInput.Focus();

    private float fontSize;
    public float FontSize { get => fontSize; set => Set(ref fontSize, value); }

    private RectangleTextAlignment fontAlignment;
    public RectangleTextAlignment FontAlignment { get => fontAlignment; set => Set(ref fontAlignment, value); }

    private RectangleTextStyle textStyle;
    public RectangleTextStyle TextStyle
    {
        get => textStyle;
        set
        {
            if (Set(ref textStyle, value))
            {
                OnPropertyChanged(nameof(IsBold));
                OnPropertyChanged(nameof(IsItalic));
            }
        }
    }

    private int textPadding;
    public int TextPadding { get => textPadding; set => Set(ref textPadding, value); }

    private bool autoSize;
    public bool AutoSize { get => autoSize; set => Set(ref autoSize, value); }

    private string inputTxt;
    public string InputTxt { get => inputTxt; set => Set(ref inputTxt, value); }

    public bool IsBold => TextStyle.HasFlag(RectangleTextStyle.Bold);
    public bool IsItalic => TextStyle.HasFlag(RectangleTextStyle.Italic);

    [RelayCommand] private void SetAlignment(RectangleTextAlignment alignment) => FontAlignment = alignment;

    [RelayCommand] private void ToggleTextStyle(RectangleTextStyle style)
    {
        var s = TextStyle ^ style;
        TextStyle = s == 0 ? RectangleTextStyle.Normal : s;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);

        if (!_loading)
            TextChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
