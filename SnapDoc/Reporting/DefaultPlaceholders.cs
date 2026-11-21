#nullable enable
using System.Globalization;

namespace SnapDoc.Reporting;

/// <summary>
/// Alle Platzhalter und Wiederholungsquellen von SnapDoc.
/// Einen neuen Platzhalter hinzufügen = eine Zeile in dieser Datei.
/// </summary>
public static class DefaultPlaceholders
{
    private static readonly string[] TextArgs = ["prefix", "suffix", "fallback"];
    private static readonly string[] SizeArgs = ["width", "height"];
    private static readonly string[] DateArgs = ["prefix", "suffix", "fallback", "format", "culture"];
    private static readonly string[] CountArgs = ["priority", "prefix", "suffix", "fallback"];

    public static PlaceholderRegistry Create(ExportSession session, WordImageRenderer images, WordPlanRenderer plans)
    {
        var r = new PlaceholderRegistry();

        // ------------------------------------------------------------ Projekt (Text)
        Text(r, "client_name", ScopeKind.Project, c => c.Data.Project.ClientName);
        Text(r, "object_address", ScopeKind.Project, c => c.Data.Project.ObjectAddress);
        Text(r, "working_title", ScopeKind.Project, c => c.Data.Project.WorkingTitle);
        Text(r, "project_nr", ScopeKind.Project, c => c.Data.Project.ProjectNr);
        Text(r, "object_name", ScopeKind.Project, c => c.Data.Project.ObjectName);
        Text(r, "project_manager", ScopeKind.Project, c => c.Data.Project.ProjectManager);
        Text(r, "linebreak", ScopeKind.Project, _ => "\n");
        Text(r, "row_count", ScopeKind.Project, c => c.Scope.RowCount > 0 ? c.Scope.RowCount.ToString() : null);

        // ------------------------------------------------------------ Projekt (Bilder & Blöcke)
        r.Register(new PlaceholderDefinition("title_image", PlaceholderKind.Inline, ScopeKind.Project, SizeArgs, images.TitleImage));
        r.Register(new PlaceholderDefinition("plan_indexes", PlaceholderKind.Text, ScopeKind.Project, Array.Empty<string>(), plans.PlanIndexes));
        r.Register(new PlaceholderDefinition("plan_images", PlaceholderKind.Block, ScopeKind.Project, SizeArgs, plans.PlanImages));

        // ------------------------------------------------------------ Plan
        Text(r, "plan_name", ScopeKind.Plan, c => c.Scope.Plan!.Name);
        Text(r, "plan_description", ScopeKind.Plan, c => c.Scope.Plan!.Description);

        // ------------------------------------------------------------ Pin
        r.Register(new PlaceholderDefinition(
            "pin_nr",
            PlaceholderKind.Inline,
            ScopeKind.Pin,
            Array.Empty<string>(),
            c => PlaceholderResult.FromElement(PinLabelFactory.CreateBoundNumber(session.StoreItemId, c.Scope.Pin!.Number, c.RunProperties))));

        r.Register(new PlaceholderDefinition(
            "pin_count",
            PlaceholderKind.Text,
            ScopeKind.Project,
            CountArgs,
            c => 
            {
                IEnumerable<ExportPin> pins;

                if (c.Scope.Plan != null)
                    pins = c.Scope.Plan.Pins;
                else
                    pins = c.Data.AllPins;

                string? priority =
                    c.Args.Get("priority");

                if (!string.IsNullOrWhiteSpace(priority))
                {
                    pins = pins.Where(pin =>
                        string.Equals(
                            pin.PriorityKey,
                            priority,
                            StringComparison.OrdinalIgnoreCase));
                }

                return PlaceholderResult.FromText(
                    pins.Count().ToString());
        }));

        Text(r, "pin_planName", ScopeKind.Plan | ScopeKind.Pin, c => c.Scope.Plan!.Name);
        Text(r, "pin_name", ScopeKind.Pin, c => c.Scope.Pin!.Name);
        Text(r, "pin_desc", ScopeKind.Pin, c => c.Scope.Pin!.Description);
        Text(r, "pin_location", ScopeKind.Pin, c => c.Scope.Pin!.Location);
        Text(r, "pin_geolocWGS84", ScopeKind.Pin, c => c.Scope.Pin!.Wgs84);
        Text(r, "pin_geolocCH1903", ScopeKind.Pin, c => c.Scope.Pin!.Ch1903);
        Text(r, "photo_count", ScopeKind.Project, c =>
            {
                int count =
                    c.Scope.Pin != null
                        ? c.Scope.Pin.Photos.Count
                        : c.Scope.Plan != null
                            ? c.Scope.Plan.Pins.Sum(
                                pin => pin.Photos.Count)
                            : c.Data.AllPins.Sum(
                                pin => pin.Photos.Count);

                return count.ToString();
            });
        Text(r, "plan_count", ScopeKind.Project, c => c.Data.Plans.Count(plan => !plan.IsWebMap).ToString());
        Text(r, "report_filename", ScopeKind.Project, _ => session.ReportFileName);
        Text(r, "template_name", ScopeKind.Project, _ => session.TemplateName);
        Text(r, "photo_filename", ScopeKind.Photo, c => Path.GetFileName( c.Scope.Photo!.File));
        Text(r, "plan_filename", ScopeKind.Plan, c => Path.GetFileName( c.Scope.Plan!.File));
        Text(r, "row_nr", ScopeKind.Project, c => c.Scope.RowNumber > 0 ? c.Scope.RowNumber.ToString() : null);
        Text(r, "row_count", ScopeKind.Project, c => c.Scope.RowCount > 0 ? c.Scope.RowCount.ToString() : null);

        r.Register(new PlaceholderDefinition("pin_priority", PlaceholderKind.Text, ScopeKind.Pin, TextArgs,
            c =>
            {
                var pin = c.Scope.Pin!;
                string? fill = string.IsNullOrEmpty(pin.PriorityKey) ? null : pin.PriorityColorHex;
                return PlaceholderResult.FromText(pin.PriorityKey, fill); // färbt zusätzlich die Zelle
            }));

        Date(r, session, "pin_captureDate", ScopeKind.Pin, c => PinTime(c), "d");
        Date(r, session, "pin_captureTime", ScopeKind.Pin, c => PinTime(c), "t");
        Date(r, session, "project_creationDate", ScopeKind.Project, c => c.Data.Project.CreationDate, "d");
        Date(r, session, "export_date", ScopeKind.Project, _ => session.ExportedAt, "d");
        Date(r, session, "export_time", ScopeKind.Project, _ => session.ExportedAt, "t");
        Date(r, session, "pin_createdDate", ScopeKind.Pin, c => c.Scope.Pin!.CapturedAt, "d");
        Date(r, session, "pin_modifiedDate", ScopeKind.Pin, c => c.Scope.Pin!.ModifiedAt.LocalDateTime, "d");
        Date(r, session, "pin_modifiedTime", ScopeKind.Pin, c => c.Scope.Pin!.ModifiedAt.LocalDateTime, "t");
        Date(r, session, "photo_captureDate", ScopeKind.Photo, c => c.Scope.Photo!.CapturedAt, "d");
        Date(r, session, "photo_captureTime", ScopeKind.Photo, c => c.Scope.Photo!.CapturedAt, "t");

        r.Register(new PlaceholderDefinition("pin_posIcon", PlaceholderKind.Inline, ScopeKind.Pin,
            SizeArgs, images.PinIcon));
        r.Register(new PlaceholderDefinition("pin_posImage", PlaceholderKind.Inline, ScopeKind.Plan | ScopeKind.Pin,
            SizeArgs, images.PinPositionImage));
        r.Register(new PlaceholderDefinition("pin_fotoList", PlaceholderKind.Inline, ScopeKind.Pin,
            SizeArgs, images.PinPhotoList));

        // ------------------------------------------------------------ Foto
        r.Register(new PlaceholderDefinition("photo_image", PlaceholderKind.Inline, ScopeKind.Photo,
            SizeArgs, images.Photo));

        // ------------------------------------------------------------ Wiederholung: ${repeat:<quelle>}
        r.Register(new PlaceholderDefinition("repeat", PlaceholderKind.Marker, ScopeKind.Project,
            Array.Empty<string>(), _ => PlaceholderResult.Empty));

        r.RegisterSource(new RepeatSourceDefinition("plans",
            Provides: ScopeKind.Plan, Requires: ScopeKind.Project, AllowedArguments: Array.Empty<string>(),
            Expand: (scope, _) => Indexed(scope.Data.Plans
                .Where(p => !p.IsWebMap)
                .Select(p => scope with { Plan = p, Pin = null, Photo = null }))));

        r.RegisterSource(new RepeatSourceDefinition("pins",
            Provides: ScopeKind.Plan | ScopeKind.Pin, Requires: ScopeKind.Project, AllowedArguments: new[] { "priority" },
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

        r.RegisterSource(new RepeatSourceDefinition(
            "photos",
            Provides: ScopeKind.Plan | ScopeKind.Pin | ScopeKind.Photo,
            Requires: ScopeKind.Project,
            AllowedArguments: Array.Empty<string>(),
            Expand: (scope, _) =>
            {
                IEnumerable<(ExportPlan? Plan, ExportPin Pin, ExportFoto Photo)> triples;

                if (scope.Pin is not null)
                {
                    triples = scope.Pin.Photos.Select(
                        photo => (scope.Plan,
                             scope.Pin,
                            Photo: photo));
                }
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
            }));

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

    private static IEnumerable<RenderScope> Indexed(IEnumerable<RenderScope> scopes)
    {
        var list = scopes.ToList();
        return list.Select((s, i) => s with { RowNumber = i + 1, RowCount = list.Count });
    }
}
