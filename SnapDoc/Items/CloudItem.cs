using CommunityToolkit.Mvvm.ComponentModel;
using SnapDoc.Services;

namespace SnapDoc.Models;

public partial class CloudItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public bool IsBackNavigation => Id == "..";
    public bool ShowChevron => IsFolder && !IsBackNavigation;
    public DateTimeOffset? LastModified { get; set; }
    public RemoteProjectDto? RemoteProject { get; set; }

    [ObservableProperty] public partial string? ObjectName { get; set; }

    partial void OnObjectNameChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    // Gibt das Datum und die Uhrzeit formatiert zurück
    public string LastModifiedText => LastModified.HasValue
        ? LastModified.Value.ToLocalTime().ToString("dd.MM.yyyy | HH:mm")
        : string.Empty;

    // Pfeil-Symbol für den Rücksprung, sonst Ordner oder Datei
    public string Icon => IsBackNavigation
        ? MaterialIcons.Subdirectory_arrow_left
        : IsFolder
            ? MaterialIcons.Folder
            : MaterialIcons.Description;

    public string DisplayName => IsBackNavigation
        ? "Übergeordneter Ordner"
        : (string.IsNullOrWhiteSpace(ObjectName) ? Name : ObjectName);
}
