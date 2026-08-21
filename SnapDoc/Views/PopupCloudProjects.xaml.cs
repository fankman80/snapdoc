using CommunityToolkit.Maui.Views;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class PopupCloudProjects : Popup<RemoteProjectDto?>
{
    private RemoteProjectDto? _selectedProject;

    public PopupCloudProjects(List<RemoteProjectDto> projects)
    {
        InitializeComponent();
        ProjectsListView.ItemsSource = projects;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedProject = e.CurrentSelection?.Count > 0
            ? e.CurrentSelection[0] as RemoteProjectDto
            : null;

        downloadButton.IsEnabled = _selectedProject != null;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        await CloseAsync(_selectedProject);
    }
}