#nullable disable

using System.Windows.Input;

namespace SnapDoc.ViewModels;

public partial class UnifiedTapBehavior : Behavior<View>
{
    // ==========================
    // BindableProperties
    // ==========================

    public static readonly BindableProperty SingleTapCommandProperty =
        BindableProperty.Create(
            nameof(SingleTapCommand),
            typeof(ICommand),
            typeof(UnifiedTapBehavior));

    public static readonly BindableProperty DoubleTapCommandProperty =
        BindableProperty.Create(
            nameof(DoubleTapCommand),
            typeof(ICommand),
            typeof(UnifiedTapBehavior));

    public ICommand SingleTapCommand
    {
        get => (ICommand)GetValue(SingleTapCommandProperty);
        set => SetValue(SingleTapCommandProperty, value);
    }

    public ICommand DoubleTapCommand
    {
        get => (ICommand)GetValue(DoubleTapCommandProperty);
        set => SetValue(DoubleTapCommandProperty, value);
    }

    // ==========================
    // Settings
    // ==========================

    public int DoubleTapDelay { get; set; } = 250;

    private CancellationTokenSource _tapCts;
    private bool _waitingForSecondTap;

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        bindable.GestureRecognizers.Add(tap);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        base.OnDetachingFrom(bindable);
        _tapCts?.Cancel();
    }

    // ==========================
    // Tap Logic
    // ==========================

    private async void OnTapped(object sender, EventArgs e)
    {
        _tapCts?.Cancel();
        _tapCts = new CancellationTokenSource();
        var token = _tapCts.Token;

        if (_waitingForSecondTap)
        {
            _waitingForSecondTap = false;
            Execute(DoubleTapCommand, sender);
            return;
        }

        _waitingForSecondTap = true;

        try
        {
            await Task.Delay(DoubleTapDelay, token);

            if (_waitingForSecondTap)
            {
                _waitingForSecondTap = false;
                Execute(SingleTapCommand, sender);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void Execute(ICommand command, object sender)
    {
        if (command == null)
            return;

        if (sender is BindableObject bo)
            command.Execute(bo.BindingContext);
    }
}