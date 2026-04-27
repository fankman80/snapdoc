using Mopups.Pages;

namespace SnapDoc;

public partial class MyBusyPage : PopupPage
{
    public MyBusyPage(string message)
    {
        InitializeComponent();

        BusyTextLabel.Text = message;
    }
}