#nullable disable

using Mopups.Pages;
using Mopups.Services;
using System.Collections.ObjectModel;

namespace bsm24.Views;

public partial class PopupTemplateManager : PopupPage
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
    public string ReturnValue { get; set; }
    public ObservableCollection<string> Documents { get; set; } = [];
    public string SelectedDocument { get; set; }

    public PopupTemplateManager()
    {
        InitializeComponent();
        LoadDocuments();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _taskCompletionSource = new TaskCompletionSource<string>();
    }

    private void LoadDocuments()
    {
        var folderPath = FileSystem.AppDataDirectory;
        var files = Directory.GetFiles(folderPath, "*.docx");

        foreach (var file in files)
        {
            Documents.Add(Path.GetFileName(file));
        }
    }

    private async void OnAddDocument(object sender, EventArgs e)
    {
        var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, new[] { "com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document" } },
            { DevicePlatform.Android, new[] { "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" } },
            { DevicePlatform.WinUI, new[] { ".doc", ".docx" } }
        });

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Wähle ein Word-Dokument",
            FileTypes = customFileType
        });

        if (result != null)
        {
            var folderPath = FileSystem.AppDataDirectory;
            var destinationPath = Path.Combine(folderPath, result.FileName);
            using (var stream = await result.OpenReadAsync())
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
            }
            Documents.Add(result.FileName);
        }
    }


    private void OnDeleteDocument(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(SelectedDocument))
        {
            var folderPath = FileSystem.AppDataDirectory;
            var filePath = Path.Combine(folderPath, SelectedDocument);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Documents.Remove(SelectedDocument);
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }
}
