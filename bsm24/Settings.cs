
namespace bsm24;

public static class Settings
{
    public static string CacheDirectory { get => cacheDirectory; set => cacheDirectory = value; }
    public static int ThumbSize { get => thumbSize; set => thumbSize = value; }
    public static int PlanPreviewSize { get => planPreviewSize; set => planPreviewSize = value; }
    public static IconItem[] PinData { get => pinData; set => pinData = value; }

    private static int thumbSize = 150;

    private static int planPreviewSize = 250;

    private static string cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "cache");

    private static IconItem[] pinData =
    [
        new("a_pin_blue.png", "", new Point(0.0625, 0.9375), new Size(64,64), false),
        new("a_pin_green.png", "", new Point(0.0625, 0.9375), new Size(64,64), false),
        new("a_pin_grey.png", "", new Point(0.0625, 0.9375), new Size(64,64), false),
        new("a_pin_pink.png", "", new Point(0.0625, 0.9375), new Size(64,64), false),
        new("a_pin_red.png", "", new Point(0.0625, 0.9375), new Size(64,64), false),
        new("a_pin_yellow.png", "", new Point(0.0625, 0.9375), new Size(64,64), false),
        new("blitzleuchte.png", "Blitzleuchte", new Point(0.5, 0.5), new Size(64,64), true),
        new("blitzleuchte_evakuierungsanlage.png", "Blitzleuchte Evakuierungsanlage", new Point(0.5, 0.5), new Size(64,64), true),
        new("brandfallgesteuert.png", "Brandfallgesteuert", new Point(0.5, 0.5), new Size(64,64), true),
        new("brandmeldeanlage_bedienstelle.png", "Brandmeldeanlage Bedienstelle", new Point(0.5, 0.5), new Size(64,64), true),
        new("brandmeldezentrale.png", "Brandmeldezentrale", new Point(0.5, 0.5), new Size(64,64), true),
        new("druckentlastungsoeffnung.png", "Druckentlastungsöffnung", new Point(0.0, 0.5), new Size(64,64), true),
        new("einspeisestelle_trockensteigleitung.png", "Einspeisestelle mit Storz (Trockensteigleitung)", new Point(0.5, 0.5), new Size(64,64), true),
        new("einzelrauchmelder.png", "Einzelrauchmelder", new Point(0.5, 0.5), new Size(64,64), true),
        new("entnahmestelle_trockensteigleitung.png", "Entnahmestelle mit Storz (Trockensteigleitung)", new Point(0.5, 0.5), new Size(64,64), true),
        new("entrauchung_mit_luefter_der_feuerwehr.png", "Entrauchung mit Lüfter der Feuerwehr (LRWA) in m³/h", new Point(0.5, 0.5), new Size(64,64), true),
        new("evakuierungsanlage_bedienstelle.png", "Evakuierungsanlage Bedienstelle", new Point(0.5, 0.5), new Size(64,64), true),
        new("evakuierungsanlage_zentrale.png", "Zentrale Evakuierungsanlage", new Point(0.5, 0.5), new Size(64,64), true),
        new("ex_zone.png", "Raum / Schrank mit Explosionsgefährdung", new Point(0.5, 0.5), new Size(64,64), true),
        new("feuerwehraufzug.png", "Feuerwehraufzug", new Point(1.0, 0.5), new Size(64,64), true),
        new("grosser_ueberdruck.png", "Grosser Überdruck", new Point(0.5, 0.5), new Size(64,64), true),
        new("handfeuerloescher.png", "Handfeuerlöscher", new Point(0.5, 0.5), new Size(64,64), true),
        new("handfeuerloescher_a_feste_nicht_schmelzende_stoffe.png", "Handfeuerlöscher: Löschmittel für feste, nicht schmelzende Stoffe", new Point(0.5, 0.5), new Size(64,64), true),
        new("handfeuerloescher_b_fluessigkeiten_schmelzende_stoffe.png", "Handfeuerlöscher: Löschmittel für Flüssigkeiten und schmelzende, feste Stoffe", new Point(0.5, 0.5), new Size(64,64), true),
        new("handfeuerloescher_c_gase.png", "Handfeuerlöscher: Löschmittel für Gase", new Point(0.5, 0.5), new Size(64,64), true),
        new("handfeuerloescher_d_metalle.png", "Handfeuerlöscher: Löschmittel für Metalle", new Point(0.5, 0.5), new Size(64,64), true),
        new("handfeuerloescher_f_fettbrand.png", "Handfeuerlöscher: Löschmittel für Fettbrand", new Point(0.5, 0.5), new Size(64,64), true),
        new("handfeuermelder.png", "Handfeuermelder", new Point(0.5, 0.5), new Size(64,64), true),
        new("hauptzugang_feuerwehr.png", "Hauptzugang Feuerwehr", new Point(1.0, 0.5), new Size(64,64), true),
        new("innenhydrant.png", "Innenhydrant", new Point(0.5, 0.5), new Size(64,64), true),
        new("maschinelle_rauch_und_waermeabzugsanlage.png", "maschinelle Rauch- und Wärmeabzugsanlage (MRWA) in m³/h", new Point(0.5, 0.5), new Size(64,64), true),
        new("mobiler_luefter_der_feuerwehr.png", "Mobiler Lüfter der Feuerwehr", new Point(1.0, 0.5), new Size(64,64), true),
        new("natuerliche_rauch_und_waermeabzugsanlage_m2.png", "natürliche Rauch- und Wärmeabzugsanlage (NRWA) in m²", new Point(0.5, 0.5), new Size(64,64), true),
        new("natuerliche_rauch_und_waermeabzugsanlage_prozent.png", "natürliche Rauch- und Wärmeabzugsanlage (NRWA) in %", new Point(0.5, 0.5), new Size(64,64), true),
        new("notausgang.png", "Notausgang", new Point(1.0, 0.5), new Size(64,64), true),
        new("notausgangsverschluss_sn_en_179.png", "Notausgangsverschluss gemäss SN EN 179 oder nicht abschliessbar", new Point(0.5, 0.5), new Size(64,64), true),
        new("notoeffnungstaster.png", "Notöffnungstaster", new Point(0.5, 0.5), new Size(64,64), true),
        new("oeffnung_fuer_natuerliche_abstroemung.png", "Öffnung für natürliche Abströmung in m²", new Point(0.5, 0.5), new Size(64,64), true),
        new("paniktuerverschluss_sn_en_1125.png", "Paniktürverschluss gemäss SN EN 1125", new Point(0.5, 0.5), new Size(64,64), true),
        new("rauchschutz_druckanlage_bedienstelle.png", "Rauchschutz-Druckanlage Bedienstelle", new Point(0.5, 0.5), new Size(64,64), true),
        new("rauch_waermeabzugsanlage_bedienstelle.png", "Rauch- und Wärmeabzug Bedienstelle", new Point(0.5, 0.5), new Size(64,64), true),
        new("sammelplatz.png", "Sammelplatz", new Point(0.5, 0.5), new Size(64,64), true),
        new("schluesseldepot.png", "Schlüsseldepot", new Point(0.5, 0.5), new Size(64,64), true),
        new("selbstschliessend_mit_freilauftuerschliesser.png", "selbstschliessend mit Freilauftürschliesser", new Point(0.5, 0.5), new Size(64,64), true),
        new("selbstschliessend_ts.png", "Selbstschliessend TS", new Point(0.5, 0.5), new Size(64,64), true),
        new("sicherheitsbeleuchtung.png", "Raum / Bereich mit Sicherheitsbeleuchtung", new Point(0.5, 0.5), new Size(64,64), true),
        new("sicherheitsleuchte_tragbar.png", "Sicherheitsleuchte tragbar", new Point(0.5, 0.5), new Size(64,64), true),
        new("spezielle_loeschanlage.png", "Spezielle Löschanlage", new Point(0.5, 0.5), new Size(64,64), true),
        new("sprinklerzentrale.png", "Sprinklerzentrale", new Point(0.5, 0.5), new Size(64,64), true),
        new("spuellueftung_bedienstelle.png", "Spüllüftung Bedienstelle", new Point(0.5, 0.5), new Size(64,64), true),
        new("tuere_rauchdicht.png", "Türe rauchdicht", new Point(0.5, 0.5), new Size(64,64), true),
        new("ueberdruck.png", "Überdruck", new Point(0.5, 0.5), new Size(64,64), true),
        new("ueberflurhydrant.png", "Überflurhydrant", new Point(0.5, 0.5), new Size(64,64), true),
        new("unterdruck.png", "Unterdruck", new Point(0.5, 0.5), new Size(64,64), true),
        new("unterflurhydrant.png", "Unterflurhyfrand", new Point(0.5, 0.5), new Size(64,64), true),
        new("ventilator_rda_sla.png", "Ventilator Rauchschutz-Druckanlage", new Point(0.5, 0.5), new Size(64,64), true),
        new("ventilator_rwa.png", "Ventilator Rauch- und Wärmeabzug", new Point(0.5, 0.5), new Size(64,64), true),
        new("wasserloeschposten.png", "Wasserlöschposten", new Point(0.5, 0.5), new Size(64,64), true),
        new("wechselrichter_pv.png", "Wechselrichter PV-Anlage", new Point(0.5, 0.5), new Size(64,64), true),
        new("zugang_spa_z.png", "Zugang SPA-Z", new Point(1.0, 0.5), new Size(64,64), true),
        new("zuluft_absaugung_maschinell.png", "Zuluft / Absaugung maschinell", new Point(1.0, 0.5), new Size(64,64), true),
        new("zuluft_abstroemung_natuerlich.png", "Zuluft / Abströmung natürlich", new Point(1.0, 0.5), new Size(64,64), true),
        new("zusaetzlicher_zugang_feuerwehr.png", "Zusätzlicher Zugang Feuerwehr", new Point(1.0, 0.5), new Size(64,64), true)
    ];
}