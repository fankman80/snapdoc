#nullable enable
using SnapDoc.Models;
using SnapDoc.Services;

namespace SnapDoc.Reporting;

/// <summary>
/// Einzige Stelle, die die SnapDoc-Modelle (GlobalJson, SyncOps, SettingsService) kennt.
/// Alles andere im Export arbeitet nur noch mit den Schnappschuss-Records aus ReportDataContext.cs.
/// </summary>
public static class ReportDataBuilder
{
    private const string MapImagePrefix = "MAP_IMG_";

    public static ReportDataContext Build(JsonDataModel data, SettingsService settings)
    {
        var options = BuildOptions(data, settings);
        var plans = new List<ExportPlan>();
        int number = 1; // fortlaufende Pin-Nummer über alle exportierten Pläne

        foreach (var planEntry in SyncOps.LivePlans(data))
        {
            var plan = planEntry.Value;
            if (!plan.AllowExport)
                continue;

            var pins = new List<ExportPin>();
            if (plan.Pins != null)
            {
                foreach (var pinEntry in SyncOps.LivePins(plan))
                {
                    var pin = pinEntry.Value;
                    if (!pin.IsAllowExport)
                        continue;
                    pins.Add(BuildPin(pin, number++));
                }
            }

            plans.Add(new ExportPlan(
                Key: planEntry.Key,
                Name: plan.Name ?? string.Empty,
                Description: plan.Description ?? string.Empty,
                File: plan.File ?? string.Empty,
                ImageSize: new SizeF((float)plan.ImageSize.Width, (float)plan.ImageSize.Height),
                IsGrayscale: plan.IsGrayscale,
                IsWebMap: planEntry.Key.Contains("webmap", StringComparison.OrdinalIgnoreCase),
                MeasureUnit: plan.MeasureUnit ?? string.Empty,
                ModifiedAt: plan.ModifiedAt,
                Pins: pins));
        }

        var project = new ProjectInfo(
            ClientName: data.Client_name ?? string.Empty,
            ObjectAddress: data.Object_address ?? string.Empty,
            WorkingTitle: data.Working_title ?? string.Empty,
            ProjectNr: data.Project_nr ?? string.Empty,
            ObjectName: data.Object_name ?? string.Empty,
            ProjectManager: data.Project_manager ?? string.Empty,
            CreationDate: data.Creation_date,
            TitleImageFile: data.TitleImage,
            TitleImageSize: new SizeF((float)data.TitleImageSize.Width, (float)data.TitleImageSize.Height),
            ModifiedAt: data.ModifiedAt);

        IReadOnlyList<ExportPriority> priorities = BuildPriorities(settings, plans);

        return new ReportDataContext(project, options, plans, priorities);
    }

    private static ExportPin BuildPin(Pin pin, int number)
    {
        var fotos = SyncOps.LiveFotos(pin).Select(x => x.Value).ToList();
        var geo = pin.GeoLocation;

        string? mapImage = fotos
            .FirstOrDefault(f => f.File.Contains(MapImagePrefix, StringComparison.OrdinalIgnoreCase))?.File;

        var photos = fotos
            .Where(f => f.AllowExport && !f.File.Contains(MapImagePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(photo => new ExportFoto(
                    File: photo.File,
                    ImageSize: new SizeF(
                        (float)photo.ImageSize.Width,
                        (float)photo.ImageSize.Height),
                    HasOverlay: photo.HasOverlay,
                    CapturedAt: photo.DateTime,
                    ModifiedAt: photo.ModifiedAt))
            .ToList();

        string priorityKey = pin.PriorityKey ?? string.Empty;
        string? priorityColor = PriorityService.ColorOf(priorityKey);

        // Wie bisher: "#AARRGGBB" → "RRGGBB"
        string colorText = pin.PinColor.ToString();
        string colorHex = colorText.Length > 3 ? colorText[3..] : "000000";

        string icon = pin.PinIcon ?? string.Empty;

        return new ExportPin(
            Number: number,
            Name: pin.PinName ?? string.Empty,
            Description: pin.PinDesc ?? string.Empty,
            Location: pin.PinLocation ?? string.Empty,
            PriorityKey: priorityKey,
            PriorityColorHex: priorityColor?.Replace("#", ""),
            Wgs84: pin.GeoLocation?.WGS84?.ToString(),
            Ch1903: pin.GeoLocation?.CH1903?.ToString(),
            Latitude: geo?.WGS84?.Latitude,
            Longitude: geo?.WGS84?.Longitude,
            Ch1903X: geo?.CH1903?.X,
            Ch1903Y: geo?.CH1903?.Y,
            GeoAccuracy: geo?.Accuracy,
            GeoTimestamp: geo?.Timestamp,
            CapturedAt: pin.DateTime,
            IconFile: icon,
            IconCacheKey: pin.IsCustomIcon ? Path.Combine("customicons", icon) : icon,
            IsCustomPin: pin.IsCustomPin,
            IsCustomIcon: pin.IsCustomIcon,
            IsWebMapPin: pin.IsWebMapPin,
            IsLockAutoScale: pin.IsLockAutoScale,
            PinScale: (float)pin.PinScale,
            Rotation: (float)pin.PinRotation,
            Position: new PointF((float)pin.Pos.X, (float)pin.Pos.Y),
            Anchor: new PointF((float)pin.Anchor.X, (float)pin.Anchor.Y),
            Size: new SizeF((float)pin.Size.Width, (float)pin.Size.Height),
            ColorHex: colorHex,
            MapImageFile: mapImage,
            Photos: photos,
            ModifiedAt: pin.ModifiedAt);
    }

    private static ExportOptions BuildOptions(JsonDataModel data, SettingsService s)
    {
        string projectDir = Path.Combine(Settings.DataDirectory, s.ProjectPath);
        return new ExportOptions(
            ExportPlans: s.IsPlanExport,
            ExportPosImages: s.IsPosImageExport,
            ExportPhotos: s.IsImageExport,
            PhotoOverlay: s.IsFotoOverlayExport,
            MaxPlanSize: s.MaxPlanExportSize,
            PlanQuality: s.PlanQuality,
            MaxPhotoSize: s.MaxFotoExportSize,
            PhotoQuality: s.FotoQuality,
            PinExportSize: (float)s.PinExportSize,
            PinPosCropSize: (float)s.PinPosCropExportSize,
            PinLabelPrefix: s.PinLabelPrefix ?? string.Empty,
            PinLabelFontSize: s.PinLabelFontSize,
            DefaultPinIcon: s.DefaultPinIcon,
            DataDirectory: Settings.DataDirectory,
            ProjectDirectory: projectDir,
            PlanDirectory: Path.Combine(projectDir, data.PlanPath),
            ImageDirectory: Path.Combine(projectDir, data.ImagePath),
            CustomPinsRelativePath: Path.Combine(s.ProjectPath, data.CustomPinsPath));
    }

    private static IReadOnlyList<ExportPriority> BuildPriorities(SettingsService settings, IReadOnlyList<ExportPlan> plans)
    {
        List<ExportPin> pins = plans.SelectMany(plan => plan.Pins).ToList();

        var priorities = new List<ExportPriority>();

        foreach (var priority in settings.PriorityItems)
        {
            string name = priority.Key ?? string.Empty;
            int pinCount = pins.Count(pin => string.Equals(pin.PriorityKey, name, StringComparison.OrdinalIgnoreCase));
            string colorHex = priority.Color?.Replace("#", "") ?? string.Empty;
            priorities.Add(new ExportPriority( Name: name, ColorHex: colorHex, PinCount: pinCount));
        }

        return priorities;
    }
}
