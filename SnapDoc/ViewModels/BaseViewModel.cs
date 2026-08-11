namespace SnapDoc.ViewModels;

public partial class BaseViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string busyText = "Bitte warten...";

    public bool IsNotBusy => !IsBusy;

    public virtual void OnAppearing() { }
    public virtual void OnDisappearing() { }

    internal event Func<string, Task>? DoDisplayAlert;
    internal event Func<BaseViewModel, bool, Task>? DoNavigate;

    public Task DisplayAlertAsync(string message)
        => DoDisplayAlert?.Invoke(message) ?? Task.CompletedTask;

    public Task NavigateAsync(BaseViewModel vm, bool showModal = false)
        => DoNavigate?.Invoke(vm, showModal) ?? Task.CompletedTask;
}