#nullable disable
using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupCalendarView : Popup<string>
{
    public PopupCalendarView(DateTime date)
    {
        InitializeComponent();

        calendar.SelectedDate = date;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        string formattedDate = calendar.SelectedDate?.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

        await CloseAsync(formattedDate);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private async void OnTodayClicked(object sender, EventArgs e)
    {
        calendar.SelectedDate = DateTime.Today;
    }
}