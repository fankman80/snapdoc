using SnapDoc.Models;

namespace SnapDoc.Controls;

public partial class CalendarView : ContentView
{
    private readonly CalendarViewModel _viewModel;

    public event EventHandler<DateTime>? DayTapped;

    public static readonly BindableProperty SelectedDateProperty =
        BindableProperty.Create(nameof(SelectedDate), typeof(DateTime), typeof(CalendarView), DateTime.Today, propertyChanged: OnStartDateChanged);

    public DateTime SelectedDate
    {
        get => (DateTime)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public CalendarView()
    {
        InitializeComponent();
        _viewModel = new CalendarViewModel();
        BindingContext = _viewModel;

        // Initial mit dem Startdatum generieren
        _viewModel.GenerateCalendar(SelectedDate);
    }

    private static void OnStartDateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CalendarView control && newValue is DateTime date)
        {
            control._viewModel.GenerateCalendar(date);
        }
    }

    private void OnDaySelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is CalendarDay selectedDay)
        {
            DayTapped?.Invoke(this, selectedDay.Date);
        }

        if (sender is CollectionView cv)
        {
            cv.SelectedItem = null;
        }
    }
}