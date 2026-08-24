namespace SnapDoc.Models;

public class CloudItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public bool IsBackNavigation => Id == "..";
    public bool ShowChevron => IsFolder && !IsBackNavigation;

    // Pfeil-Symbol für den Rücksprung, sonst Ordner oder Datei
    public string Icon => IsBackNavigation
        ? MaterialIcons.Subdirectory_arrow_left
        : IsFolder
            ? MaterialIcons.Folder
            : MaterialIcons.Description;

    // Besser lesbarer Text für den Zurück-Eintrag
    public string DisplayName => IsBackNavigation
        ? "Übergeordneter Ordner"
        : Name;
}