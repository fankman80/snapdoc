using System.Text;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace SnapDoc.Services.Cloud;

/// <summary>Remote-Datei bzw. -Ordner, wie ihn der Sync-Code sieht.</summary>
public sealed record CloudItem(
    string DriveId,
    string ItemId,
    string Name,
    bool IsFolder,
    long Size,
    DateTimeOffset? Modified,
    string? ETag,
    string? ModifiedBy);

/// <summary>Wird geworfen, wenn jemand anderes die Datei inzwischen geaendert hat (HTTP 412).</summary>
public sealed class CloudConflictException(string name)
    : Exception($"'{name}' wurde inzwischen von jemand anderem geaendert.");

/// <summary>
/// Alle Drive-Zugriffe laufen ueber /drives/{driveId}/items/{itemId}.
/// Kein /me/drive/root:/pfad mehr – dadurch funktionieren eigene Ordner,
/// geteilte Ordner und SharePoint-Bibliotheken mit demselben Code.
/// </summary>
public sealed class GraphDriveService(GraphServiceClient graph)
{
    // -----------------------------------------------------------------
    // Speicherort bestimmen
    // -----------------------------------------------------------------

    /// <summary>Ordner im eigenen OneDrive (wird bei Bedarf erstellt), z.B. "SnapDoc".</summary>
    public async Task<CloudLocation> GetOrCreateOwnFolderAsync(string folderName, CancellationToken ct = default)
    {
        var drive = await graph.Me.Drive.GetAsync(cancellationToken: ct)
                    ?? throw new InvalidOperationException("Kein OneDrive fuer dieses Konto gefunden.");
        var root = await graph.Drives[drive.Id].Root.GetAsync(cancellationToken: ct)
                   ?? throw new InvalidOperationException("OneDrive-Stammordner nicht gefunden.");

        var folder = await GetOrCreateFolderAsync(drive.Id!, root.Id!, folderName, ct);
        return new CloudLocation(drive.Id!, folder.ItemId, folder.Name, CloudLocationKind.OwnOneDrive);
    }

    /// <summary>
    /// Aus einem Freigabelink (OneDrive privat, OneDrive Business oder SharePoint) den Ordner aufloesen.
    /// Benoetigt keine zusaetzlichen Scopes ausser Files.ReadWrite.All.
    /// </summary>
    public async Task<CloudLocation> ResolveShareLinkAsync(string sharingUrl, CancellationToken ct = default)
    {
        var item = await graph.Shares[EncodeSharingUrl(sharingUrl.Trim())].DriveItem
                       .GetAsync(cancellationToken: ct)
                   ?? throw new InvalidOperationException("Link konnte nicht aufgeloest werden.");

        if (item.Folder == null)
            throw new InvalidOperationException("Der Link zeigt auf eine Datei, nicht auf einen Ordner.");

        var driveId = item.ParentReference?.DriveId
                      ?? throw new InvalidOperationException("DriveId fehlt in der Antwort.");

        // OneDrive Business liegt auf "<tenant>-my.sharepoint.com", SharePoint-Sites auf "<tenant>.sharepoint.com"
        var isSharePoint = item.ParentReference.DriveType == "documentLibrary"
                           && item.WebUrl?.Contains("-my.sharepoint.com", StringComparison.OrdinalIgnoreCase) == false;
        var kind = isSharePoint ? CloudLocationKind.SharePoint : CloudLocationKind.SharedFolder;

        return new CloudLocation(driveId, item.Id!, item.Name ?? "SnapDoc", kind,
            item.CreatedBy?.User?.DisplayName, item.WebUrl);
    }

    /// <summary>
    /// Geteilte Ordner, die der Benutzer per "Zu Meine Dateien hinzufuegen" eingebunden hat.
    /// Sie erscheinen im eigenen Root als RemoteItem und zeigen auf das Drive der anderen Person.
    /// </summary>
    public async Task<IReadOnlyList<CloudLocation>> GetShortcutFoldersAsync(CancellationToken ct = default)
    {
        var drive = await graph.Me.Drive.GetAsync(cancellationToken: ct);
        var root = await graph.Drives[drive!.Id].Root.GetAsync(cancellationToken: ct);

        var result = new List<CloudLocation>();
        await foreach (var item in EnumerateChildrenRawAsync(drive.Id!, root!.Id!, ct))
        {
            var remote = item.RemoteItem;
            if (remote?.Folder == null || remote.ParentReference?.DriveId == null)
                continue;

            result.Add(new CloudLocation(
                remote.ParentReference.DriveId, remote.Id!, item.Name ?? remote.Name ?? "?",
                CloudLocationKind.SharedFolder,
                remote.Shared?.Owner?.User?.DisplayName, remote.WebUrl));
        }
        return result;
    }

    /// <summary>Prueft, ob der gespeicherte Ort noch existiert und erreichbar ist.</summary>
    public async Task<bool> IsReachableAsync(CloudLocation location, CancellationToken ct = default)
    {
        try
        {
            var item = await graph.Drives[location.DriveId].Items[location.ItemId].GetAsync(cancellationToken: ct);
            return item?.Folder != null;
        }
        catch (ODataError e) when (e.ResponseStatusCode is 403 or 404)
        {
            return false; // Freigabe entzogen oder Ordner geloescht
        }
    }

    // -----------------------------------------------------------------
    // Ordner / Dateien – alles relativ zu (DriveId, ItemId)
    // -----------------------------------------------------------------

    public async Task<CloudItem> GetOrCreateFolderAsync(string driveId, string parentId, string name, CancellationToken ct = default)
    {
        var existing = await TryGetChildAsync(driveId, parentId, name, ct);
        if (existing != null)
            return existing.IsFolder
                ? existing
                : throw new InvalidOperationException($"'{name}' existiert bereits als Datei.");

        var request = new DriveItem
        {
            Name = name,
            Folder = new Folder(),
            AdditionalData = new Dictionary<string, object> { ["@microsoft.graph.conflictBehavior"] = "fail" }
        };

        try
        {
            var created = await graph.Drives[driveId].Items[parentId].Children.PostAsync(request, cancellationToken: ct);
            return ToCloudItem(created!, driveId);
        }
        catch (ODataError e) when (e.ResponseStatusCode == 409)
        {
            // Der andere Benutzer hat den Ordner gleichzeitig erstellt
            return (await TryGetChildAsync(driveId, parentId, name, ct))!;
        }
    }

    /// <summary>Kind per Name/relativem Pfad, z.B. "Projekte/123/project.json". Null wenn nicht vorhanden.</summary>
    public async Task<CloudItem?> TryGetChildAsync(string driveId, string parentId, string relativePath, CancellationToken ct = default)
    {
        try
        {
            var item = await graph.Drives[driveId].Items[parentId].ItemWithPath(relativePath)
                .GetAsync(cancellationToken: ct);
            return item == null ? null : ToCloudItem(item, driveId);
        }
        catch (ODataError e) when (e.ResponseStatusCode == 404)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<CloudItem> ListChildrenAsync(
        string driveId, string folderId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in EnumerateChildrenRawAsync(driveId, folderId, ct))
            yield return ToCloudItem(item, driveId);
    }

    public async Task<Stream> DownloadAsync(string driveId, string itemId, CancellationToken ct = default) =>
        await graph.Drives[driveId].Items[itemId].Content.GetAsync(cancellationToken: ct)
        ?? throw new InvalidOperationException("Leere Antwort beim Download.");

    /// <summary>
    /// Laedt eine Datei in (driveId, parentId) hoch.
    /// expectedETag: ETag der zuletzt gesehenen Version. Ist die Datei inzwischen geaendert,
    /// wird CloudConflictException geworfen statt still zu ueberschreiben.
    /// </summary>
    public async Task<CloudItem> UploadAsync(
        string driveId, string parentId, string name, Stream content,
        string? expectedETag = null, CancellationToken ct = default)
    {
        try
        {
            var uploaded = await graph.Drives[driveId].Items[parentId].ItemWithPath(name).Content
                .PutAsync(content, cfg =>
                {
                    if (!string.IsNullOrEmpty(expectedETag))
                        cfg.Headers.Add("If-Match", expectedETag);
                }, ct);

            return ToCloudItem(uploaded!, driveId);
        }
        catch (ODataError e) when (e.ResponseStatusCode == 412)
        {
            throw new CloudConflictException(name);
        }
    }

    public async Task DeleteAsync(string driveId, string itemId, string? expectedETag = null, CancellationToken ct = default)
    {
        try
        {
            await graph.Drives[driveId].Items[itemId].DeleteAsync(cfg =>
            {
                if (!string.IsNullOrEmpty(expectedETag))
                    cfg.Headers.Add("If-Match", expectedETag);
            }, ct);
        }
        catch (ODataError e) when (e.ResponseStatusCode == 404)
        {
            // Bereits vom anderen Benutzer geloescht – Ziel erreicht
        }
        catch (ODataError e) when (e.ResponseStatusCode == 412)
        {
            throw new CloudConflictException(itemId);
        }
    }

    // -----------------------------------------------------------------
    // Hilfsfunktionen
    // -----------------------------------------------------------------

    private async IAsyncEnumerable<DriveItem> EnumerateChildrenRawAsync(
        string driveId, string folderId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var builder = graph.Drives[driveId].Items[folderId].Children;
        var page = await builder.GetAsync(cfg => cfg.QueryParameters.Top = 200, ct);

        while (page != null)
        {
            foreach (var item in page.Value ?? [])
                yield return item;

            if (string.IsNullOrEmpty(page.OdataNextLink))
                break;

            page = await builder.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: ct);
        }
    }

    private static CloudItem ToCloudItem(DriveItem item, string fallbackDriveId) => new(
        item.ParentReference?.DriveId ?? fallbackDriveId,
        item.Id!,
        item.Name ?? string.Empty,
        item.Folder != null,
        item.Size ?? 0,
        item.LastModifiedDateTime,
        item.ETag,
        item.LastModifiedBy?.User?.DisplayName);

    /// <summary>Freigabelink -> Share-ID gemaess Graph: "u!" + base64url ohne Padding.</summary>
    private static string EncodeSharingUrl(string url)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(url));
        return "u!" + base64.TrimEnd('=').Replace('/', '_').Replace('+', '-');
    }
}
