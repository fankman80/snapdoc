using CommunityToolkit.Mvvm.Messaging;
using SnapDoc.Messages;
using SnapDoc.Models;

namespace SnapDoc.Services;

public static class SyncOps
{
    // === Lesen: nur lebende Objekte ================================

    public static IEnumerable<KeyValuePair<string, Plan>> LivePlans(JsonDataModel data)
        => data?.Plans?.Where(p => p.Value.IsLive()) ?? [];

    public static IEnumerable<KeyValuePair<string, Pin>> LivePins(Plan plan)
        => plan?.Pins?.Where(p => p.Value.IsLive()) ?? [];

    public static IEnumerable<KeyValuePair<string, Foto>> LiveFotos(Pin pin)
        => pin?.Fotos?.Where(f => f.Value.IsLive()) ?? [];

    public static int LivePinCount(Plan plan) => LivePins(plan).Count();

    public static bool TryGetLivePin(Plan? plan, string pinId, out Pin? pin)
    {
        pin = null;
        if (plan?.Pins == null || string.IsNullOrEmpty(pinId)) return false;
        if (!plan.Pins.TryGetValue(pinId, out var p) || p.IsDeleted()) return false;
        pin = p;
        return true;
    }

    public static bool TryGetLivePlan(string? planId, out Plan? plan)
    {
        plan = null;
        if (GlobalJson.Data?.Plans == null || string.IsNullOrEmpty(planId)) return false;
        if (!GlobalJson.Data.Plans.TryGetValue(planId, out var p) || p.IsDeleted()) return false;
        plan = p;
        return true;
    }

    // === Löschen: markieren statt entfernen ========================

    /// <summary>
    /// Markiert den Pin als gelöscht. Die zugehörigen Binärdateien werden
    /// NICHT hier gelöscht – das erledigt SetPin.DeletePinData, weil dort
    /// die Pfade bekannt sind.
    /// </summary>
    public static void DeletePin(string planId, string pinId, bool notify = true)
    {
        if (!TryGetLivePlan(planId, out var plan) || plan is null) return;
        if (!TryGetLivePin(plan, pinId, out var pin) || pin is null) return;

        // Fotos mit-markieren, sonst bleiben sie als Waisen in der Galerie
        foreach (var f in pin.Fotos?.Values ?? Enumerable.Empty<Foto>())
            f.MarkDeleted();

        pin.MarkDeleted();
        plan.PinCount = LivePinCount(plan);
        plan.Touch();

        if (notify)
            WeakReferenceMessenger.Default.Send(new PinDeletedMessage(pinId));

        SaveManager.NotifyDataChanged();
    }

    public static void DeletePlan(string planId)
    {
        if (!TryGetLivePlan(planId, out var plan) || plan is null) return;

        foreach (var pin in plan.Pins?.Values ?? Enumerable.Empty<Pin>())
        {
            foreach (var f in pin.Fotos?.Values ?? Enumerable.Empty<Foto>())
                f.MarkDeleted();
            pin.MarkDeleted();
        }

        plan.PinCount = 0;
        plan.MarkDeleted();

        WeakReferenceMessenger.Default.Send(new PlanDeletedMessage(planId));
        WeakReferenceMessenger.Default.Send(
            new RemoteDataChangedMessage(RemoteChangeType.PlanListUpdated));
        SaveManager.NotifyDataChanged();
    }

    public static void DeleteFoto(string planId, string pinId, string fotoId)
    {
        if (!TryGetLivePlan(planId, out var plan) || plan is null) return;
        if (!TryGetLivePin(plan, pinId, out var pin) || pin is null) return;
        if (pin.Fotos == null || !pin.Fotos.TryGetValue(fotoId, out var foto) || foto is null) return;

        foto.MarkDeleted();
        pin.Touch();
        SaveManager.NotifyDataChanged();
    }

    // === Aufräumen =================================================

    /// <summary>
    /// Entfernt Tombstones, die garantiert überall angekommen sind.
    /// NUR beim Projekt-Laden aufrufen, niemals während laufendem Sync –
    /// sonst verschwindet eine Tombstone, die ein offline gewesenes
    /// Gerät noch braucht.
    /// </summary>
    public static bool PurgeTombstones(JsonDataModel data, int keepDays = 90)
    {
        if (data?.Plans == null) return false;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-keepDays);
        bool changed = false;

        foreach (var planId in data.Plans.Keys.ToList())
        {
            var plan = data.Plans[planId];

            if (plan.DeletedAt is { } pd && pd < cutoff)
            {
                data.Plans.Remove(planId);
                changed = true;
                continue;
            }

            if (plan.Pins is null) continue;

            foreach (var pinId in plan.Pins.Keys.ToList())
            {
                var pin = plan.Pins[pinId];

                if (pin.DeletedAt is { } pind && pind < cutoff)
                {
                    plan.Pins.Remove(pinId);
                    changed = true;
                    continue;
                }

                if (pin.Fotos is null) continue;

                foreach (var fotoId in pin.Fotos.Keys.ToList())
                {
                    if (pin.Fotos[fotoId].DeletedAt is { } fd && fd < cutoff)
                    {
                        pin.Fotos.Remove(fotoId);
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }
}