using bsm24.Services;

namespace bsm24.ViewModels;
public class MainShellViewModel
{
    public GPSViewModel GPSViewModel { get; }
    public SettingsService SettingsService { get; }

    public MainShellViewModel()
    {
        GPSViewModel = GPSViewModel.Instance;
        SettingsService = SettingsService.Instance;
    }
}
