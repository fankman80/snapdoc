#nullable enable
using System.Globalization;

namespace SnapDoc.Reporting;

/// <summary>
/// Alle Platzhalter und Wiederholungsquellen von SnapDoc.
/// Einen neuen Platzhalter hinzufügen = eine Zeile in dieser Datei.
/// </summary>
public static class DefaultPlaceholders
{
    private static readonly string[] TextArgs = ["prefix", "suffix", "fallback", "uppercase", "lowercase", "trim"];
    private static readonly string[] NumberArgs = ["prefix", "suffix", "fallback", "format", "culture", "emptyIfZero"];
    private static readonly string[] SizeArgs = ["width", "height"];
    private static readonly string[] DateArgs = ["prefix", "suffix", "fallback", "format", "culture"];
    private static readonly string[] CountArgs = ["priority", "prefix", "suffix", "fallback"];
    private static readonly string[] PriorityRepeatArgs = ["usedOnly"];
    private static readonly string[] PinRepeatArgs = ["priority"];

    public static PlaceholderRegistry Create(ExportSession session, WordImageRenderer images, WordPlanRenderer plans)
    {
        var r = new PlaceholderRegistry();

        Text(r, "client_name", ScopeKind.Project, c => c.Data.Project.ClientName);
        Text(r, "object_address", ScopeKind.Project, c => c.Data.Project.ObjectAddress);
        Text(r, "working_title", ScopeKind.Project, c => c.Data.Project.WorkingTitle);
        Text(r, "project_nr", ScopeKind.Project, c => c.Data.Project.ProjectNr);
        Text(r, "object_name", ScopeKind.Project, c => c.Data.Project.ObjectName);
        Text(r, "project_manager", ScopeKind.Project, c => c.Data.Project.ProjectManager);
        Text(r, "linebreak", ScopeKind.Project, _ => "\n");
        Text(r, "pin_planName", ScopeKind.Plan | ScopeKind.Pin, c => c.Scope.Plan!.Name);
        Text(r, "pin_name", ScopeKind.Pin, c => c.Scope.Pin!.Name);
        Text(r, "pin_desc", ScopeKind.Pin, c => c.Scope.Pin!.Description);
        Text(r, "pin_location", ScopeKind.Pin, c => c.Scope.Pin!.Location);
        Text(r, "pin_createdBy", ScopeKind.Pin, c => c.Scope.Pin!.CreatedBy);
        Text(r, "pin_geolocWGS84", ScopeKind.Pin, c => c.Scope.Pin!.Wgs84);
        Text(r, "pin_geolocCH1903", ScopeKind.Pin, c => c.Scope.Pin!.Ch1903);
        Text(r, "plan_count", ScopeKind.Project, c => c.Data.Plans.Count(plan => !plan.IsWebMap).ToString());
        Text(r, "report_filename", ScopeKind.Project, _ => session.ReportFileName);
        Text(r, "template_name", ScopeKind.Project, _ => session.TemplateName);
        Text(r, "photo_filename", ScopeKind.Photo, c => Path.GetFileName( c.Scope.Photo!.File));
        Text(r, "plan_filename", ScopeKind.Plan, c => Path.GetFileName( c.Scope.Plan!.File));
        Text(r, "row_nr", ScopeKind.Project, c => c.Scope.RowNumber > 0 ? c.Scope.RowNumber.ToString() : null);
        Text(r, "row_count", ScopeKind.Project, c => c.Scope.RowCount > 0 ? c.Scope.RowCount.ToString() : null);
        Text(r, "plan_measureUnit", ScopeKind.Plan, c => c.Scope.Plan!.MeasureUnit);
        Text(r, "priority_name", ScopeKind.Priority, c => c.Scope.Priority!.Name);
        Text(r, "priority_color", ScopeKind.Priority, c => c.Scope.Priority!.ColorHex);
        Text(r, "plan_name", ScopeKind.Plan, c => c.Scope.Plan!.Name);
        Text(r, "plan_description", ScopeKind.Plan, c => c.Scope.Plan!.Description);
        Text(r, "photo_count", ScopeKind.Project, c =>
            {
                int count = c.Scope.Pin != null
                        ? c.Scope.Pin.Photos.Count
                        : c.Scope.Plan != null
                            ? c.Scope.Plan.Pins.Sum(pin => pin.Photos.Count)
                            : c.Data.AllPins.Sum(pin => pin.Photos.Count);
                return count.ToString();
            });


        r.Register(new PlaceholderDefinition("title_image", PlaceholderKind.Inline, ScopeKind.Project, SizeArgs, images.TitleImage));
        r.Register(new PlaceholderDefinition("plan_indexes", PlaceholderKind.Text, ScopeKind.Project, Array.Empty<string>(), plans.PlanIndexes));
        r.Register(new PlaceholderDefinition("plan_images", PlaceholderKind.Block, ScopeKind.Project, SizeArgs, plans.PlanImages));
        r.Register(new PlaceholderDefinition("pin_posIcon", PlaceholderKind.Inline, ScopeKind.Pin, SizeArgs, images.PinIcon));
        r.Register(new PlaceholderDefinition("pin_posImage", PlaceholderKind.Inline, ScopeKind.Plan | ScopeKind.Pin, SizeArgs, images.PinPositionImage));
        r.Register(new PlaceholderDefinition("pin_fotoList", PlaceholderKind.Inline, ScopeKind.Pin, SizeArgs, images.PinPhotoList));
        r.Register(new PlaceholderDefinition("photo_image", PlaceholderKind.Inline, ScopeKind.Photo, SizeArgs, images.Photo));
        r.Register(new PlaceholderDefinition("repeat", PlaceholderKind.Marker, ScopeKind.Project, Array.Empty<string>(), _ => PlaceholderResult.Empty));
        r.Register(new PlaceholderDefinition("pin_nr", PlaceholderKind.Inline, ScopeKind.Pin, Array.Empty<string>(), c => 
            PlaceholderResult.FromElement(PinLabelFactory.CreateBoundNumber(session.StoreItemId, c.Scope.Pin!.Number, c.RunProperties))));
        r.Register(new PlaceholderDefinition("pin_count", PlaceholderKind.Text, ScopeKind.Project, CountArgs, c => 
            {
                IEnumerable<ExportPin> pins;
                if (c.Scope.Plan != null)
                    pins = c.Scope.Plan.Pins;
                else
                    pins = c.Data.AllPins;
                string? priority = c.Args.Get("priority");
                if (!string.IsNullOrWhiteSpace(priority))
                    pins = pins.Where(pin => string.Equals(pin.PriorityKey, priority, StringComparison.OrdinalIgnoreCase));
                return PlaceholderResult.FromText(
                    pins.Count().ToString());
            }));
        r.Register(new PlaceholderDefinition("pin_priority", PlaceholderKind.Text, ScopeKind.Pin, TextArgs, c =>
            {
                var pin = c.Scope.Pin!;
                string? fill = string.IsNullOrEmpty(pin.PriorityKey) ? null : pin.PriorityColorHex;
                return PlaceholderResult.FromText(pin.PriorityKey, fill); // färbt zusätzlich die Zelle
            }));
        
        Date(r, session, "pin_captureDate", ScopeKind.Pin, c => PinTime(c), "d");
        Date(r, session, "pin_captureTime", ScopeKind.Pin, c => PinTime(c), "t");
        Date(r, session, "pin_createdDate", ScopeKind.Pin, c => c.Scope.Pin!.CapturedAt, "d");
        Date(r, session, "pin_modifiedDate", ScopeKind.Pin, c => c.Scope.Pin!.ModifiedAt.LocalDateTime, "d");
        Date(r, session, "pin_modifiedTime", ScopeKind.Pin, c => c.Scope.Pin!.ModifiedAt.LocalDateTime, "t");
        Date(r, session, "pin_geoTimestamp", ScopeKind.Pin, c => c.Scope.Pin!.GeoTimestamp?.LocalDateTime, "G");
        Date(r, session, "project_creationDate", ScopeKind.Project, c => c.Data.Project.CreationDate, "d");
        Date(r, session, "project_modifiedDate", ScopeKind.Project, c => c.Data.Project.ModifiedAt.LocalDateTime, "d");
        Date(r, session, "project_modifiedTime", ScopeKind.Project, c => c.Data.Project.ModifiedAt.LocalDateTime, "t");
        Date(r, session, "export_date", ScopeKind.Project, _ => session.ExportedAt, "d");
        Date(r, session, "export_time", ScopeKind.Project, _ => session.ExportedAt, "t");
        Date(r, session, "photo_captureDate", ScopeKind.Photo, c => c.Scope.Photo!.CapturedAt, "d");
        Date(r, session, "photo_captureTime", ScopeKind.Photo, c => c.Scope.Photo!.CapturedAt, "t");
        Date(r, session, "plan_modifiedDate", ScopeKind.Plan, c => c.Scope.Plan!.ModifiedAt.LocalDateTime, "d");
        Date(r, session, "plan_modifiedTime", ScopeKind.Plan, c => c.Scope.Plan!.ModifiedAt.LocalDateTime, "t");
        Date(r, session, "photo_modifiedDate", ScopeKind.Photo, c => c.Scope.Photo!.ModifiedAt.LocalDateTime, "d");
        Date(r, session, "photo_modifiedTime", ScopeKind.Photo, c => c.Scope.Photo!.ModifiedAt.LocalDateTime, "t");

        Number(r, "plan_pinCount", ScopeKind.Plan, c => c.Scope.Plan!.Pins.Count);
        Number(r, "pin_latitude", ScopeKind.Pin, c => c.Scope.Pin!.Latitude);
        Number(r, "pin_longitude", ScopeKind.Pin, c => c.Scope.Pin!.Longitude);
        Number(r, "pin_ch1903X", ScopeKind.Pin, c => c.Scope.Pin!.Ch1903X);
        Number(r, "pin_ch1903Y", ScopeKind.Pin, c => c.Scope.Pin!.Ch1903Y);
        Number(r, "pin_geoAccuracy", ScopeKind.Pin, c => c.Scope.Pin!.GeoAccuracy);
        Number(r, "photo_width", ScopeKind.Photo, c => c.Scope.Photo!.ImageSize.Width);
        Number(r, "photo_height", ScopeKind.Photo, c => c.Scope.Photo!.ImageSize.Height);
        Number(r, "priority_pinCount", ScopeKind.Priority, c => c.Scope.Priority!.PinCount);

        r.RegisterSource(new RepeatSourceDefinition("plans",
            Provides: ScopeKind.Plan, Requires: ScopeKind.Project, AllowedArguments: Array.Empty<string>(),
            Expand: (scope, _) => Indexed(scope.Data.Plans
                .Where(p => !p.IsWebMap)
                .Select(p => scope with { Plan = p, Pin = null, Photo = null }))));
        r.RegisterSource(new RepeatSourceDefinition("pins",
            Provides: ScopeKind.Plan | ScopeKind.Pin,
            Requires: ScopeKind.Project, PinRepeatArgs,
            Expand: (scope, args) =>
            {
                var pairs = scope.Plan != null
                    ? scope.Plan.Pins.Select(p => (scope.Plan, Pin: p))
                    : scope.Data.Plans.SelectMany(pl => pl.Pins.Select(p => (Plan: pl, Pin: p)));
                string? priority = args.Get("priority");
                if (!string.IsNullOrEmpty(priority))
                    pairs = pairs.Where(x => string.Equals(x.Pin.PriorityKey, priority, StringComparison.OrdinalIgnoreCase));
                return Indexed(pairs.Select(x => scope with { Plan = x.Plan, Pin = x.Pin, Photo = null }));
            }));

        r.RegisterSource(new RepeatSourceDefinition("photos",
            Provides: ScopeKind.Plan | ScopeKind.Pin | ScopeKind.Photo,
            Requires: ScopeKind.Project,
            AllowedArguments: Array.Empty<string>(),
            Expand: (scope, _) =>
            {
                IEnumerable<(ExportPlan? Plan, ExportPin Pin, ExportFoto Photo)> triples;

                if (scope.Pin is not null)
                    triples = scope.Pin.Photos.Select(photo => (scope.Plan, scope.Pin, Photo: photo));
                else
                {
                    triples = scope.Data.Plans.SelectMany(
                        plan => plan.Pins.SelectMany(
                            pin => pin.Photos.Select(
                                photo => (
                                    Plan: (ExportPlan?)plan,
                                    Pin: pin,
                                    Photo: photo))));
                }

                return Indexed(
                    triples.Select(x => scope with
                    {
                        Plan = x.Plan,
                        Pin = x.Pin,
                        Photo = x.Photo
                    }));
            }));
        r.RegisterSource(new RepeatSourceDefinition("priorities",
            Provides: ScopeKind.Priority,
            Requires: ScopeKind.Project,
            AllowedArguments: PriorityRepeatArgs,
            Expand: (scope, arguments) =>
            {
                IEnumerable<ExportPriority> priorities = scope.Data.Priorities;
                if (arguments.GetBool("usedOnly", false))
                    priorities = priorities.Where( priority => priority.PinCount > 0);
                return Indexed(priorities.Select( priority =>
                    scope with
                    {
                        Plan = null,
                        Pin = null,
                        Photo = null,
                        Priority = priority
                    }));
            }));
        return r;
    }

    // ---------------------------------------------------------------- Helfer
    private static void Text(PlaceholderRegistry r, string name, ScopeKind scope, Func<PlaceholderCall, string?> getter) =>
        r.Register(new PlaceholderDefinition(name, PlaceholderKind.Text, scope, TextArgs,
            c => PlaceholderResult.FromText(getter(c))));

    private static void Date(PlaceholderRegistry r, ExportSession session, string name, ScopeKind scope,
        Func<PlaceholderCall, DateTime?> getter, string defaultFormat) =>
        r.Register(new PlaceholderDefinition(name, PlaceholderKind.Text, scope, DateArgs,
            c =>
            {
                DateTime? value = getter(c);
                return PlaceholderResult.FromText(value is null ? null : FormatDate(session, c, value.Value, defaultFormat));
            },
            PlaceholderValueKind.DateTime));

    private static DateTime? PinTime(PlaceholderCall c)
    {
        var t = c.Scope.Pin!.CapturedAt;
        return t == DateTime.MinValue ? null : t;
    }

    private static string FormatDate(ExportSession session, PlaceholderCall call, DateTime value, string defaultFormat)
    {
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(call.Args.Get("culture") ?? "de-CH"); }
        catch (CultureNotFoundException)
        {
            session.Warn($"Unbekannte Kultur „{call.Args.Get("culture")}“, es wird de-CH verwendet.", call.Token.Raw);
            culture = CultureInfo.GetCultureInfo("de-CH");
        }

        string format = call.Args.Get("format") ?? defaultFormat;
        try
        {
            return value.ToString(format, culture);
        }
        catch (FormatException)
        {
            session.Warn($"Ungültiges Datumsformat „{format}“, es wird „{defaultFormat}“ verwendet.", call.Token.Raw);
            return value.ToString(defaultFormat, culture);
        }
    }

    private static void Number(PlaceholderRegistry registry, string name, ScopeKind scope, Func<PlaceholderCall, object?> getter)
    {
        registry.Register(
            new PlaceholderDefinition(
                name,
                PlaceholderKind.Text,
                scope,
                NumberArgs,
                call => PlaceholderResult.FromValue(getter(call)),
                PlaceholderValueKind.Number));
    }

    private static IEnumerable<RenderScope> Indexed(IEnumerable<RenderScope> scopes)
    {
        var list = scopes.ToList();
        return list.Select((s, i) => s with { RowNumber = i + 1, RowCount = list.Count });
    }
}
