namespace SnapDoc.ViewModels;

public partial class BaseViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string BusyText { get; set; } = "Bitte warten...";
    public bool IsNotBusy => !IsBusy;

    private FlyoutBehavior _previousBehavior = FlyoutBehavior.Flyout;

    partial void OnIsBusyChanged(bool value)
    {
        if (Shell.Current == null) return;

        if (value)
        {
            _previousBehavior = Shell.Current.FlyoutBehavior;
            Shell.Current.FlyoutBehavior = FlyoutBehavior.Disabled;
        }
        else
        {
            Shell.Current.FlyoutBehavior = _previousBehavior;
        }
    }

    public virtual void OnAppearing() { }
    public virtual void OnDisappearing() { }
    internal event Func<string, Task>? DoDisplayAlert;
    internal event Func<BaseViewModel, bool, Task>? DoNavigate;
    public Task DisplayAlertAsync(string message) => DoDisplayAlert?.Invoke(message) ?? Task.CompletedTask;
    public Task NavigateAsync(BaseViewModel vm, bool showModal = false) => DoNavigate?.Invoke(vm, showModal) ?? Task.CompletedTask;
}