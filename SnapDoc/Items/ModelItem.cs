#nullable disable
using CommunityToolkit.Mvvm.ComponentModel;
using SnapDoc.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Items;

/// <summary>
/// Basisklasse fuer alle ViewModel-Wrapper, die ein Modell aus SnapDoc.Models kapseln
/// (PlanItem, PinItem, ...). Uebernimmt die PropertyChanged-Weiterleitung vom Modell
/// und stellt einen kompakten Setter bereit.
/// </summary>
public abstract class ModelItem<TModel> : ObservableObject, IDisposable where TModel : ObservableObject
{
    protected readonly TModel Model;
    private bool _disposed;

    protected ModelItem(TModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Model.PropertyChanged += OnModelChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;

        Model.PropertyChanged -= OnModelChanged;
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        => OnModelPropertyChanged(e.PropertyName);

    /// <summary>Aenderungen am Modell in die UI durchreichen.</summary>
    protected virtual void OnModelPropertyChanged(string propertyName) { }

    /// <summary>
    /// Schreibt einen Wert ins Modell und benachrichtigt die UI - inklusive
    /// abhaengiger (berechneter) Properties ueber <paramref name="alsoNotify"/>.
    /// </summary>
    protected bool SetModel<T>(T current, T value, Action<T> setter, string[] alsoNotify = null,
    [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return false;

        setter(value);
        OnPropertyChanged(propertyName);

        if (alsoNotify != null)
            foreach (var p in alsoNotify)
                OnPropertyChanged(p);

        SaveManager.NotifyDataChanged(); // Autosave, entprellt
        return true;
    }

    /// <summary>Mehrere Properties auf einmal neu auswerten lassen.</summary>
    protected void Notify(params string[] propertyNames)
    {
        foreach (var p in propertyNames)
            OnPropertyChanged(p);
    }
}
