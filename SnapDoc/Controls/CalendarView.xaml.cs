using SnapDoc.Models;

namespace SnapDoc.Controls;

public partial class CalendarView : ContentView
{
    private readonly CalendarViewModel _viewModel;
    private DateTime _currentDate;

    public event EventHandler<DateTime>? DayTapped;

    public static readonly BindableProperty SelectedDateProperty =
        BindableProperty.Create(
            nameof(SelectedDate),
            typeof(DateTime),
            typeof(CalendarView),
            DateTime.Today,
            propertyChanged: OnStartDateChanged);

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

        _currentDate = SelectedDate;
        _viewModel.GenerateCalendar(_currentDate, SelectedDate);
    }

    private static void OnStartDateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CalendarView control && newValue is DateTime date)
        {
            control._currentDate = date;
            control._viewModel.GenerateCalendar(date, date);
        }
    }

    private void OnPreviousMonthClicked(object sender, EventArgs e)
    {
        _currentDate = _currentDate.AddMonths(-1);
        _viewModel.GenerateCalendar(_currentDate, SelectedDate);
    }

    private void OnNextMonthClicked(object sender, EventArgs e)
    {
        _currentDate = _currentDate.AddMonths(1);
        _viewModel.GenerateCalendar(_currentDate, SelectedDate);
    }

    private void OnDayTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is CalendarDay selectedDay)
        {
            SelectedDate = selectedDay.Date;
            _viewModel.SelectDate(selectedDay.Date);
            DayTapped?.Invoke(this, selectedDay.Date);
        }
    }
}