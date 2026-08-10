using System.Collections.ObjectModel;

namespace SnapDoc.Models;

public partial class CalendarViewModel : BindableObject
{
    public ObservableCollection<CalendarDay> Days { get; set; } = new();

    private DateTime _currentMonth;
    public string MonthName => _currentMonth.ToString("MMMM yyyy");

    public CalendarViewModel()
    {
        _currentMonth = DateTime.Today;
        GenerateCalendar(_currentMonth);
    }

    public void GenerateCalendar(DateTime date)
    {
        _currentMonth = date;
        Days.Clear();

        var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

        int startDayOffset = (int)firstDayOfMonth.DayOfWeek - 1;
        if (startDayOffset < 0) startDayOffset = 6;
        var prevMonth = firstDayOfMonth.AddMonths(-1);
        var daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
        for (int i = startDayOffset - 1; i >= 0; i--)
        {
            Days.Add(new CalendarDay
            {
                Date = new DateTime(prevMonth.Year, prevMonth.Month, daysInPrevMonth - i),
                IsCurrentMonth = false
            });
        }

        for (int i = 1; i <= daysInMonth; i++)
        {
            Days.Add(new CalendarDay
            {
                Date = new DateTime(date.Year, date.Month, i),
                IsCurrentMonth = true
            });
        }

        int remainingDays = 42 - Days.Count;
        var nextMonth = firstDayOfMonth.AddMonths(1);
        for (int i = 1; i <= remainingDays; i++)
        {
            Days.Add(new CalendarDay
            {
                Date = new DateTime(nextMonth.Year, nextMonth.Month, i),
                IsCurrentMonth = false
            });
        }

        OnPropertyChanged(nameof(MonthName));
    }
}

public class CalendarDay
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool HasEvent { get; set; }
    public string DayNumber => Date.Day.ToString();
}