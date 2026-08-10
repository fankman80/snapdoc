namespace SnapDoc.Controls;

public partial class BorderedExpander : ContentView
{
    public static readonly BindableProperty HeaderTextProperty =
        BindableProperty.Create(nameof(HeaderText), typeof(string), typeof(BorderedExpander), string.Empty);

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public BorderedExpander()
    {
        InitializeComponent();
    }
}