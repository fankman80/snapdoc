#nullable disable
using CommunityToolkit.Maui.Extensions;
using SnapDoc.Resources.Languages;
using System.Collections;

namespace SnapDoc.Controls;

public partial class CustomRadioPicker : ContentView
{
    // Kern-Eigenschaften für Daten und Popup
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(CustomRadioPicker), null, propertyChanged: OnItemsSourceChanged);
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(CustomRadioPicker), null, BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    // SelectedIndex Property
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(CustomRadioPicker), -1, BindingMode.TwoWay, propertyChanged: OnSelectedIndexChanged);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(CustomRadioPicker), AppResources.bitte_waehlen, propertyChanged: OnControlStateChanged);
    public static readonly BindableProperty ControlPaddingProperty = BindableProperty.Create(nameof(ControlPadding), typeof(Thickness), typeof(CustomRadioPicker), new Thickness(10, 12));

    // Styling-Eigenschaften für XAML
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(Label), Label.TextColorProperty.DefaultValue);
    public static readonly BindableProperty ArrowColorProperty = BindableProperty.Create(nameof(ArrowColor), typeof(Color), typeof(CustomRadioPicker), Colors.DarkGray);
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(CustomRadioPicker), Colors.DarkGray);
    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(CustomRadioPicker), 1.0);
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(Label), Label.FontSizeProperty.DefaultValue);
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(CustomRadioPicker), new CornerRadius(8.0));

    public IEnumerable ItemsSource { get => (IEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public Color ArrowColor { get => (Color)GetValue(ArrowColorProperty); set => SetValue(ArrowColorProperty, value); }
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    public double BorderThickness { get => (double)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public Thickness ControlPadding { get => (Thickness)GetValue(ControlPaddingProperty); set => SetValue(ControlPaddingProperty, value); }
    private bool _isSynchronizing = false;
    public event EventHandler<EventArgs> SelectedIndexChanged;

    public CustomRadioPicker()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomRadioPicker control)
        {
            control.SynchronizeFromIndex();
            control.UpdateDisplay();
            control.SelectedIndexChanged?.Invoke(control, EventArgs.Empty);
        }
    }

    private static void OnControlStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomRadioPicker control)
            control.UpdateDisplay();
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomRadioPicker control)
            control.SynchronizeFromItem();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomRadioPicker control)
        {
            control.SynchronizeFromItem();
            control.UpdateDisplay();
        }
    }

    private void SynchronizeFromItem()
    {
        if (_isSynchronizing) return;
        _isSynchronizing = true;

        if (ItemsSource == null || SelectedItem == null)
        {
            SelectedIndex = -1;
            _isSynchronizing = false;
            return;
        }

        int index = 0;
        bool found = false;
        foreach (var item in ItemsSource)
        {
            if (Equals(item, SelectedItem))
            {
                SelectedIndex = index;
                found = true;
                break;
            }
            index++;
        }

        if (!found) SelectedIndex = -1;
        _isSynchronizing = false;
    }

    private void SynchronizeFromIndex()
    {
        if (_isSynchronizing) return;
        _isSynchronizing = true;

        if (ItemsSource == null || SelectedIndex < 0)
        {
            SelectedItem = null;
            _isSynchronizing = false;
            return;
        }

        int index = 0;
        bool found = false;
        foreach (var item in ItemsSource)
        {
            if (index == SelectedIndex)
            {
                SelectedItem = item;
                found = true;
                break;
            }
            index++;
        }

        if (!found) SelectedItem = null;
        _isSynchronizing = false;
    }

    private void UpdateDisplay()
    {
        DisplayLabel.Text = SelectedItem?.ToString() ?? Placeholder;
    }

    private async void OnClicked(object sender, EventArgs e)
    {
        if (ItemsSource == null) return;

        var items = new List<string>();
        foreach (var item in ItemsSource)
        {
            items.Add(item?.ToString() ?? string.Empty);
        }

        var currentSelection = SelectedItem?.ToString() ?? string.Empty;
        var popup = new Views.PopupRadioPicker(items, currentSelection);

        Page mainPage = Shell.Current ?? (Application.Current?.Windows.Count > 0 ? Shell.Current : null);
        if (mainPage is Shell shell) mainPage = shell.CurrentPage;

        if (mainPage is ContentPage currentPage)
        {
            var popupResult = await currentPage.ShowPopupAsync<string>(popup, Settings.PopupOptions);

            if (popupResult?.Result is string selectedValue)
            {
                int index = 0;
                foreach (var item in ItemsSource)
                {
                    if ((item?.ToString() ?? string.Empty) == selectedValue)
                    {
                        SelectedItem = item;
                        break;
                    }
                    index++;
                }
            }
        }
    }
}