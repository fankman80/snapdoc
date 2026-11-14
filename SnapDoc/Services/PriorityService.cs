#nullable disable
using SnapDoc.Models;
using static SnapDoc.Models.SyncStampExtensions;

namespace SnapDoc.Services;

/// <summary>
/// Prioritäten: lokale Liste (SettingsService) + projektbezogene Liste (Projektdatei).
/// Die lokale Liste wird durch Projekte nie verändert.
/// </summary>
public static class PriorityService
{
    public const string DefaultColor = "#808080";

    private static List<PriorityItem> Local => SettingsService.Instance.PriorityItems;

    private static List<PriorityItem> Project
    {
        get
        {
            var data = GlobalJson.Data;
            if (data == null) return [];
            data.PriorityItems ??= [];
            return data.PriorityItems;
        }
    }

    private static bool Same(string a, string b)
        => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static PriorityItem Find(IEnumerable<PriorityItem> items, string key)
        => string.IsNullOrWhiteSpace(key) ? null : items?.FirstOrDefault(i => Same(i.Key, key));

    private static IEnumerable<Pin> AllPins(JsonDataModel data)
        => data?.Plans?.Values
               .Where(p => p?.Pins != null)
               .SelectMany(p => p.Pins.Values)
               .Where(p => p != null) ?? [];

    // ---------------------------------------------------------------
    //  Anzeige
    // ---------------------------------------------------------------

    /// <summary>Leer + Projekt-Prioritäten + lokale, die im Projekt fehlen.</summary>
    public static List<string> PickerKeys()
    {
        var keys = new List<string> { "" };

        foreach (var item in Project.Concat(Local))
        {
            if (string.IsNullOrWhiteSpace(item?.Key)) continue;
            if (keys.Any(k => Same(k, item.Key))) continue;
            keys.Add(item.Key);
        }

        return keys;
    }

    /// <summary>Projektfarbe hat Vorrang vor der lokalen.</summary>
    public static string ColorOf(string key)
    {
        string color = Find(Project, key)?.Color;
        if (string.IsNullOrWhiteSpace(color))
            color = Find(Local, key)?.Color;

        return string.IsNullOrWhiteSpace(color) ? null : color;
    }

    // ---------------------------------------------------------------
    //  Bearbeiten (SetPin)
    // ---------------------------------------------------------------

    /// <summary>Neue Priorität lokal und im Projekt anlegen (oder vorhandene liefern).</summary>
    public static PriorityItem AddOrGet(string key)
    {
        key = key.Trim();
        var now = DateTimeOffset.UtcNow;

        var project = Find(Project, key);
        var local = Find(Local, key);
        string color = project?.Color ?? local?.Color ?? DefaultColor;

        if (local == null)
            Local.Add(new PriorityItem { Key = project?.Key ?? key, Color = color, ModifiedAt = now });

        if (project == null)
        {
            project = new PriorityItem { Key = local?.Key ?? key, Color = color, ModifiedAt = now };
            Project.Add(project);
        }

        return project;
    }

    public static void SetColor(string key, string color)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var list in new[] { Local, Project })
        {
            if (Find(list, key) is { } item)
            {
                item.Color = color;
                item.ModifiedAt = now;
            }
        }
    }

    /// <summary>Umbenennen in beiden Listen und bei allen Pins des Projekts.</summary>
    public static string Rename(string oldKey, string newKey)
    {
        newKey = newKey.Trim();
        var now = DateTimeOffset.UtcNow;

        foreach (var list in new[] { Local, Project })
        {
            var item = Find(list, oldKey);
            if (item == null) continue;

            var target = Find(list, newKey);
            if (target != null && !ReferenceEquals(target, item))
            {
                list.Remove(item);            // Name existiert schon -> zusammenführen
            }
            else
            {
                item.Key = newKey;
                item.ModifiedAt = now;
            }
        }

        string finalKey = Find(Project, newKey)?.Key ?? newKey;

        foreach (var pin in AllPins(GlobalJson.Data).Where(p => Same(p.PriorityKey, oldKey)))
        {
            pin.PriorityKey = finalKey;
            pin.Touch();
        }

        return finalKey;
    }

    /// <summary>Löschen in beiden Listen, Pins verlieren die Priorität.</summary>
    public static void Delete(string key)
    {
        Local.RemoveAll(i => !string.IsNullOrWhiteSpace(i.Key) && Same(i.Key, key));
        Project.RemoveAll(i => Same(i.Key, key));

        foreach (var pin in AllPins(GlobalJson.Data).Where(p => Same(p.PriorityKey, key)))
        {
            pin.PriorityKey = string.Empty;
            pin.Touch();
        }
    }

    // ---------------------------------------------------------------
    //  Speichern / Sync (SaveManager)
    // ---------------------------------------------------------------

    /// <summary>
    /// Beim Speichern: alle lokalen Prioritäten ins Projekt übernehmen.
    /// Bei gleichem Key nur überschreiben, wenn die lokale Farbe neuer ist.
    /// </summary>
    public static bool IncludeLocalPriorities(JsonDataModel data)
    {
        if (data == null) return false;
        data.PriorityItems ??= [];

        bool changed = false;

        foreach (var local in Local.ToList())
        {
            if (string.IsNullOrWhiteSpace(local?.Key)) continue;

            var existing = Find(data.PriorityItems, local.Key);

            if (existing == null)
            {
                data.PriorityItems.Add(new PriorityItem
                {
                    Key = local.Key,
                    Color = local.Color ?? DefaultColor,
                    ModifiedAt = local.ModifiedAt
                });
                changed = true;
            }
            else if (local.ModifiedAt > existing.ModifiedAt && local.Color != existing.Color)
            {
                existing.Color = local.Color;
                existing.ModifiedAt = local.ModifiedAt;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Projektlisten vereinigen. Lokale Settings bleiben unberührt.</summary>
    public static bool Merge(JsonDataModel local, JsonDataModel cloud)
    {
        if (local == null || cloud?.PriorityItems == null) return false;
        local.PriorityItems ??= [];

        bool changed = false;

        foreach (var c in cloud.PriorityItems.ToList())
        {
            if (string.IsNullOrWhiteSpace(c?.Key)) continue;

            var l = Find(local.PriorityItems, c.Key);

            if (l == null)
            {
                local.PriorityItems.Add(new PriorityItem { Key = c.Key, Color = c.Color, ModifiedAt = c.ModifiedAt });
                changed = true;
            }
            else if (c.ModifiedAt > l.ModifiedAt)
            {
                l.Color = c.Color;
                l.ModifiedAt = c.ModifiedAt;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Alte Projekte: Index -> Key (anhand der lokalen Liste) und
    /// sicherstellen, dass jeder verwendete Key in der Projektliste steht.
    /// </summary>
    public static bool MigratePins(JsonDataModel data)
    {
        if (data?.Plans == null) return false;

        var legacy = Local.ToList();
        data.PriorityItems ??= [];
        bool changed = false;

        using var _ = SyncStampGate.Suspend();   // Migration soll keine Stempel erzeugen

        foreach (var pin in AllPins(data))
        {
            if (pin.PriorityKey == null)
            {
                int index = pin.LegacyPinPriority ?? 0;

                pin.PriorityKey = index > 0 && index < legacy.Count
                    ? legacy[index].Key ?? string.Empty
                    : string.Empty;
                changed = true;
            }

            // Alten Index nicht mehr zurückschreiben
            if (pin.LegacyPinPriority != null)
            {
                pin.LegacyPinPriority = null;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(pin.PriorityKey) &&
                Find(data.PriorityItems, pin.PriorityKey) == null)
            {
                data.PriorityItems.Add(new PriorityItem
                {
                    Key = pin.PriorityKey,
                    Color = Find(legacy, pin.PriorityKey)?.Color ?? DefaultColor
                });
                changed = true;
            }
        }

        return changed;
    }
}