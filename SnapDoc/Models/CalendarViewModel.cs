using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Models;

public partial class CalendarViewModel : BindableObject
{
    public ObservableCollection<CalendarDay> Days { get; } = [];

    private DateTime _currentMonth;
    public string MonthName => _currentMonth.ToString("MMMM yyyy");

    public CalendarViewModel()
    {
        // Genau 42 Elemente einmalig anlegen (7 Spalten x 6 Zeilen)
        for (int i = 0; i < 42; i++)
        {
            Days.Add(new CalendarDay());
        }
    }

    public void GenerateCalendar(DateTime date, DateTime? selectedDate = null)
    {
        _currentMonth = date;

        var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

        int startDayOffset = (int)firstDayOfMonth.DayOfWeek - 1;
        if (startDayOffset < 0) startDayOffset = 6;

        var prevMonth = firstDayOfMonth.AddMonths(-1);
        var daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

        int index = 0;

        // Vormonat
        for (int i = startDayOffset - 1; i >= 0; i--)
        {
            var dayDate = new DateTime(prevMonth.Year, prevMonth.Month, daysInPrevMonth - i);
            UpdateDay(index++, dayDate, isCurrentMonth: false, selectedDate);
        }

        // Aktueller Monat
        for (int i = 1; i <= daysInMonth; i++)
        {
            var dayDate = new DateTime(date.Year, date.Month, i);
            UpdateDay(index++, dayDate, isCurrentMonth: true, selectedDate);
        }

        // Folgemonat
        var nextMonth = firstDayOfMonth.AddMonths(1);
        int nextMonthDay = 1;
        while (index < 42)
        {
            var dayDate = new DateTime(nextMonth.Year, nextMonth.Month, nextMonthDay++);
            UpdateDay(index++, dayDate, isCurrentMonth: false, selectedDate);
        }

        OnPropertyChanged(nameof(MonthName));
    }

    public void SelectDate(DateTime newSelectedDate)
    {
        foreach (var day in Days)
        {
            day.IsSelected = (day.Date.Date == newSelectedDate.Date);
        }
    }

    private void UpdateDay(int index, DateTime date, bool isCurrentMonth, DateTime? selectedDate)
    {
        var day = Days[index];
        day.Date = date;
        day.IsCurrentMonth = isCurrentMonth;
        day.IsSelected = selectedDate.HasValue && date.Date == selectedDate.Value.Date;
    }
}

public partial class CalendarDay : INotifyPropertyChanged
{
    private DateTime _date;
    public DateTime Date
    {
        get => _date;
        set
        {
            if (_date != value)
            {
                _date = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DayNumber));
            }
        }
    }

    private bool _isCurrentMonth;
    public bool IsCurrentMonth
    {
        get => _isCurrentMonth;
        set { if (_isCurrentMonth != value) { _isCurrentMonth = value; OnPropertyChanged(); } }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public string DayNumber => Date.Day.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}