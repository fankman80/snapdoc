#nullable disable
using CommunityToolkit.Maui.Extensions;
using SnapDoc.Resources.Languages;
using System.Collections;
using System.Collections.Specialized;

namespace SnapDoc.Controls;

public partial class CustomRadioPicker : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(CustomRadioPicker), null, propertyChanged: OnItemsSourceChanged);
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(CustomRadioPicker), null, BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(CustomRadioPicker), -1, BindingMode.TwoWay, propertyChanged: OnSelectedIndexChanged);
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(CustomRadioPicker), AppResources.bitte_waehlen, propertyChanged: OnControlStateChanged);
    public static readonly BindableProperty ControlPaddingProperty = BindableProperty.Create(nameof(ControlPadding), typeof(Thickness), typeof(CustomRadioPicker), new Thickness(10, 11));
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(BorderedEntry), defaultValue: (string)Entry.FontFamilyProperty.DefaultValue);
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(Label), Label.TextColorProperty.DefaultValue);
    public static readonly BindableProperty ArrowColorProperty = BindableProperty.Create(nameof(ArrowColor), typeof(Color), typeof(CustomRadioPicker), Colors.DarkGray);
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(CustomRadioPicker), Colors.DarkGray);
    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(CustomRadioPicker), 1.0);
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(Label), Label.FontSizeProperty.DefaultValue);
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(CustomRadioPicker), new CornerRadius(8.0));
    public static readonly BindableProperty EmptyTextProperty = BindableProperty.Create(nameof(EmptyText), typeof(string), typeof(CustomRadioPicker), null, propertyChanged: OnControlStateChanged);
  
    public IEnumerable ItemsSource { get => (IEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string FontFamily { get => (string)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public Color ArrowColor { get => (Color)GetValue(ArrowColorProperty); set => SetValue(ArrowColorProperty, value); }
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    public double BorderThickness { get => (double)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public Thickness ControlPadding { get => (Thickness)GetValue(ControlPaddingProperty); set => SetValue(ControlPaddingProperty, value); }
    private bool _isSynchronizing = false;
    public event EventHandler<EventArgs> SelectedIndexChanged;

    private INotifyCollectionChanged _observedCollection;

    public CustomRadioPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => AttachCollection(ItemsSource as INotifyCollectionChanged);
        Unloaded += (_, _) => AttachCollection(null);
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
        if (bindable is not CustomRadioPicker control)
            return;

        if (control.IsLoaded)
            control.AttachCollection(newValue as INotifyCollectionChanged);

        control.SynchronizeFromItem();
        control.UpdateDisplay();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomRadioPicker control)
        {
            control.SynchronizeFromItem();
            control.UpdateDisplay();
        }
    }

    private void AttachCollection(INotifyCollectionChanged collection)
    {
        if (ReferenceEquals(_observedCollection, collection))
            return;

        _observedCollection?.CollectionChanged -= OnCollectionChanged;

        _observedCollection = collection;

        _observedCollection?.CollectionChanged += OnCollectionChanged;

        // Änderungen nachholen, die während Unloaded passiert sind
        SynchronizeFromItem();
        UpdateDisplay();
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Nur Index und Anzeige anpassen, SelectedItem nicht verändern
        SynchronizeFromItem();
        UpdateDisplay();
    }

    private void SynchronizeFromItem()
    {
        if (_isSynchronizing)
            return;

        _isSynchronizing = true;

        try
        {
            if (ItemsSource == null)
                return;

            if (SelectedItem == null)
            {
                SelectedIndex = -1;
                return;
            }

            int index = 0;

            foreach (var item in ItemsSource)
            {
                if (Equals(item, SelectedItem))
                {
                    SelectedIndex = index;
                    return;
                }

                index++;
            }

            SelectedIndex = -1;
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void SynchronizeFromIndex()
    {
        if (_isSynchronizing)
            return;

        _isSynchronizing = true;

        try
        {
            if (ItemsSource == null)
            {
                SelectedItem = null;
                return;
            }

            if (SelectedIndex < 0)
            {
                SelectedItem = null;
                return;
            }

            int index = 0;

            foreach (var item in ItemsSource)
            {
                if (index == SelectedIndex)
                {
                    SelectedItem = item;
                    return;
                }

                index++;
            }

            SelectedItem = null;
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void UpdateDisplay()
    {
        if (!HasItems())
        {
            DisplayLabel.Text = EmptyText ?? Placeholder;
            Opacity = 0.5;
            return;
        }

        Opacity = 1.0;

        var text = SelectedIndex >= 0 ? SelectedItem?.ToString() : null;
        DisplayLabel.Text = string.IsNullOrWhiteSpace(text) ? Placeholder : text;
    }

    private async void OnClicked(object sender, EventArgs e)
    {
        if (!HasItems())
            return; // Kein leeres Popup öffnen

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
                foreach (var item in ItemsSource)
                {
                    if ((item?.ToString() ?? string.Empty) == selectedValue)
                    {
                        SelectedItem = item;
                        break;
                    }
                }
            }
        }
    }

    private bool HasItems()
    {
        if (ItemsSource == null)
            return false;

        var enumerator = ItemsSource.GetEnumerator();
        try { return enumerator.MoveNext(); }
        finally { (enumerator as IDisposable)?.Dispose(); }
    }
}