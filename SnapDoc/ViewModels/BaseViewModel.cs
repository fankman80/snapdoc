namespace SnapDoc.ViewModels;

public partial class BaseViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

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