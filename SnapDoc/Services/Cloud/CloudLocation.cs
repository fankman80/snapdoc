using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapDoc.Services.Cloud;

/// <summary>Art des Speicherorts – nur fuer Anzeige/Diagnose, die Logik nutzt immer DriveId+ItemId.</summary>
public enum CloudLocationKind
{
    OwnOneDrive,     // eigenes OneDrive (privat oder Business)
    SharedFolder,    // von anderer Person freigegebener Ordner
    SharePoint       // Dokumentbibliothek einer SharePoint-Site
}

/// <summary>
/// Eindeutiger Sync-Ordner, unabhaengig von /me/drive.
/// DriveId + ItemId funktionieren fuer eigenes OneDrive, geteilte Ordner und SharePoint gleich.
/// </summary>
public sealed record CloudLocation(
    string DriveId,
    string ItemId,
    string DisplayName,
    CloudLocationKind Kind,
    string? OwnerName = null,
    string? WebUrl = null);

[JsonSerializable(typeof(CloudLocation))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal sealed partial class CloudLocationJsonContext : JsonSerializerContext;

/// <summary>Speichert den gewaehlten Sync-Ordner pro Konto (AOT-sicher via Source Generation).</summary>
public static class CloudLocationStore
{
    private static string Key(string accountId) => $"sync_location_{accountId}";

    public static void Save(string accountId, CloudLocation location) =>
        Preferences.Default.Set(Key(accountId),
            JsonSerializer.Serialize(location, CloudLocationJsonContext.Default.CloudLocation));

    public static CloudLocation? Load(string accountId)
    {
        var json = Preferences.Default.Get(Key(accountId), string.Empty);
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize(json, CloudLocationJsonContext.Default.CloudLocation);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Clear(string accountId) => Preferences.Default.Remove(Key(accountId));
}
