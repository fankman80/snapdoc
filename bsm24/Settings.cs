using SkiaSharp;

namespace bsm24;

public static class Settings
{
    public static string CacheDirectory { get => cacheDirectory; set => cacheDirectory = value; }
    public static int ThumbSize { get => thumbSize; set => thumbSize = value; }
    public static int PlanPreviewSize { get => planPreviewSize; set => planPreviewSize = value; }
    public static double DefaultPinZoom { get => defaultPinZoom; set => defaultPinZoom = value; }
    public static int PinTextPadding { get => pinTextPadding; set => pinTextPadding = value; }
    public static int PinTextDistance { get => pinTextDistance; set => pinTextDistance = value; }
    public static IconItem[] PinData { get => pinData; set => pinData = value; }


    private static string cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "cache");
    private static int thumbSize = 150;
    private static int planPreviewSize = 250;
    private static double defaultPinZoom = 3;
    private static int pinTextPadding = 6;
    private static int pinTextDistance = 6;
    private static IconItem[] pinData =
    [
        new("a_pin_blue.png", "", new Point(0.297, 0.97), new Size(64,64), false, new SKColor(0,50,204), 1.0),  // PinSet1 Anchor Point(0.0625, 0.9375)
        new("a_pin_green.png", "", new Point(0.297, 0.97), new Size(64,64), false, new SKColor(0,153,0), 1.0),
        new("a_pin_grey.png", "", new Point(0.297, 0.97), new Size(64,64), false, new SKColor(112,112,112), 1.0),
        new("a_pin_pink.png", "", new Point(0.297, 0.97), new Size(64,64), false, new SKColor(252,2,202), 1.0),
        new("a_pin_red.png", "", new Point(0.297, 0.97), new Size(64,64), false, new SKColor(255,0,0), 1.0),
        new("a_pin_yellow.png", "", new Point(0.297, 0.97), new Size(64,64), false, new SKColor(220,220,50), 1.0),
        new("blitzleuchte.png", "Blitzleuchte", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("blitzleuchte_evakuierungsanlage.png", "Blitzleuchte Evakuierungsanlage", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(0,153,0), 1.0),
        new("brandfallgesteuert.png", "Brandfallgesteuert", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("brandmeldeanlage_bedienstelle.png", "Brandmeldeanlage Bedienstelle", new Point(0.5, 0.5), new Size(192,64), true, new SKColor(255,0,0), 0.4),
        new("brandmeldezentrale.png", "Brandmeldezentrale", new Point(0.5, 0.5), new Size(192,64), true, new SKColor(255,0,0), 0.4),
        new("druckentlastungsoeffnung.png", "Druckentlastungsöffnung", new Point(0.0, 0.5), new Size(197,64), true, new SKColor(220,220,50), 1.0),
        new("druckschacht.png", "Druckschacht", new Point(0.5, 0.5), new Size(121,64), true, new SKColor(220,220,50), 1.0),
        new("einspeisestelle_trockensteigleitung.png", "Einspeisestelle mit Storz (Trockensteigleitung)", new Point(0.5, 0.5), new Size(109,64), true, new SKColor(0,50,204), 1.0),
        new("einzelrauchmelder.png", "Einzelrauchmelder", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("entnahmestelle_trockensteigleitung.png", "Entnahmestelle mit Storz (Trockensteigleitung)", new Point(0.5, 0.5), new Size(109,64), true, new SKColor(0,50,204), 1.0),
        new("entrauchung_mit_luefter_der_feuerwehr.png", "Entrauchung mit Lüfter der Feuerwehr (LRWA) in m³/h", new Point(0.5, 0.5), new Size(160,64), true, new SKColor(220,220,50), 0.8),
        new("evakuierungsanlage_bedienstelle.png", "Evakuierungsanlage Bedienstelle", new Point(0.5, 0.5), new Size(192,64), true, new SKColor(0,153,0), 0.4),
        new("evakuierungsanlage_zentrale.png", "Zentrale Evakuierungsanlage", new Point(0.5, 0.5), new Size(192,64), true, new SKColor(0,153,0), 0.4),
        new("ex_zone.png", "Raum / Schrank mit Explosionsgefährdung", new Point(0.5, 0.5), new Size(73,64), true, new SKColor(220,220,50), 1.0),
        new("feuerwehraufzug.png", "Feuerwehraufzug", new Point(1.0, 0.5), new Size(102,64), true, new SKColor(255,0,0), 1.0),
        new("grosser_ueberdruck.png", "Grosser Überdruck", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(220,220,50), 1.0),
        new("handfeuerloescher.png", "Handfeuerlöscher", new Point(0.5, 0.5), new Size(71,64), true, new SKColor(0,50,204), 1.0),
        new("handfeuerloescher_a_feste_nicht_schmelzende_stoffe.png", "Handfeuerlöscher: Löschmittel für feste, nicht schmelzende Stoffe", new Point(0.5, 0.5), new Size(71,64), true, new SKColor(0,50,204), 1.0),
        new("handfeuerloescher_b_fluessigkeiten_schmelzende_stoffe.png", "Handfeuerlöscher: Löschmittel für Flüssigkeiten und schmelzende, feste Stoffe", new Point(0.5, 0.5), new Size(71,64), true, new SKColor(0,50,204), 1.0),
        new("handfeuerloescher_c_gase.png", "Handfeuerlöscher: Löschmittel für Gase", new Point(0.5, 0.5), new Size(71,64), true, new SKColor(0,50,204), 1.0),
        new("handfeuerloescher_d_metalle.png", "Handfeuerlöscher: Löschmittel für Metalle", new Point(0.5, 0.5), new Size(71,64), true, new SKColor(0,50,204), 1.0),
        new("handfeuerloescher_f_fettbrand.png", "Handfeuerlöscher: Löschmittel für Fettbrand", new Point(0.5, 0.5), new Size(71,64), true, new SKColor(0,50,204), 1.0),
        new("handfeuermelder.png", "Handfeuermelder", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("hauptzugang_feuerwehr.png", "Hauptzugang Feuerwehr", new Point(1.0, 0.5), new Size(90,64), true, new SKColor(255,0,0), 1.0),
        new("innenhydrant.png", "Innenhydrant", new Point(0.5, 0.5), new Size(144,64), true, new SKColor(0,50,204), 1.0),
        new("maschinelle_rauch_und_waermeabzugsanlage.png", "maschinelle Rauch- und Wärmeabzugsanlage (MRWA) in m³/h", new Point(0.5, 0.5), new Size(160,64), true, new SKColor(255,0,0), 0.8),
        new("mobiler_luefter_der_feuerwehr.png", "Mobiler Lüfter der Feuerwehr", new Point(1.0, 0.5), new Size(111,64), true, new SKColor(220,220,50), 1.0),
        new("natuerliche_rauch_und_waermeabzugsanlage_m2.png", "natürliche Rauch- und Wärmeabzugsanlage (NRWA) in m²", new Point(0.5, 0.5), new Size(160,64), true, new SKColor(220,220,50), 0.8),
        new("natuerliche_rauch_und_waermeabzugsanlage_prozent.png", "natürliche Rauch- und Wärmeabzugsanlage (NRWA) in %", new Point(0.5, 0.5), new Size(160,64), true, new SKColor(220,220,50), 0.8),
        new("notausgang.png", "Notausgang", new Point(1.0, 0.5), new Size(89,64), true, new SKColor(0,153,0), 1.0),
        new("notausgangsverschluss_sn_en_179.png", "Notausgangsverschluss gemäss SN EN 179 oder nicht abschliessbar", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(0,153,0), 1.0),
        new("notoeffnungstaster.png", "Notöffnungstaster", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(0,153,0), 1.0),
        new("oeffnung_fuer_natuerliche_abstroemung.png", "Öffnung für natürliche Abströmung in m²", new Point(0.5, 0.5), new Size(160,64), true, new SKColor(220,220,50), 0.8),
        new("paniktuerverschluss_sn_en_1125.png", "Paniktürverschluss gemäss SN EN 1125", new Point(0.5, 0.5), new Size(129,64), true, new SKColor(0,153,0), 1.0),
        new("rauchschutz_druckanlage_bedienstelle.png", "Rauchschutz-Druckanlage Bedienstelle", new Point(0.5, 0.5), new Size(185,64), true, new SKColor(220,220,50), 0.4),
        new("rauch_waermeabzugsanlage_bedienstelle.png", "Rauch- und Wärmeabzug Bedienstelle", new Point(0.5, 0.5), new Size(185,64), true, new SKColor(220,220,50), 0.4),
        new("rwa_schacht.png", "RWA Schacht", new Point(0.5, 0.5), new Size(121,64), true, new SKColor(220,220,50), 1.0),
        new("sammelplatz.png", "Sammelplatz", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(0,153,0), 1.0),
        new("schluesseldepot.png", "Schlüsseldepot", new Point(0.5, 0.5), new Size(128,64), true, new SKColor(255,0,0), 1.0),
        new("selbstschliessend_mit_freilauftuerschliesser.png", "selbstschliessend mit Freilauftürschliesser", new Point(0.5, 0.5), new Size(131,64), true, new SKColor(255,0,0), 1.0),
        new("selbstschliessend_ts.png", "Selbstschliessend TS", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("sicherheitsbeleuchtung.png", "Raum / Bereich mit Sicherheitsbeleuchtung", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("sicherheitsleuchte_tragbar.png", "Sicherheitsleuchte tragbar", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("spezielle_loeschanlage.png", "Spezielle Löschanlage", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(0,50,204), 1.0),
        new("sprinklerzentrale.png", "Sprinklerzentrale", new Point(0.5, 0.5), new Size(192,64), true, new SKColor(0,50,204), 0.4),
        new("spuellueftung_bedienstelle.png", "Spüllüftung Bedienstelle", new Point(0.5, 0.5), new Size(185,64), true, new SKColor(220,220,50), 0.4),
        new("tuere_rauchdicht.png", "Türe rauchdicht", new Point(0.5, 0.5), new Size(64,84), true, new SKColor(255,0,0), 1.0),
        new("ueberdruck.png", "Überdruck", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(220,220,50), 1.0),
        new("ueberflurhydrant.png", "Überflurhydrant", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(0,50,204), 1.0),
        new("unterdruck.png", "Unterdruck", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(220,220,50), 1.0),
        new("unterflurhydrant.png", "Unterflurhyfrand", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(0,50,204), 1.0),
        new("ventilator_rda_sla.png", "Ventilator Rauchschutz-Druckanlage", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(220,220,50), 1.0),
        new("ventilator_rwa.png", "Ventilator Rauch- und Wärmeabzug", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(220,220,50), 1.0),
        new("wasserloeschposten.png", "Wasserlöschposten", new Point(0.5, 0.5), new Size(118,64), true, new SKColor(0,50,204), 1.0),
        new("wechselrichter_pv.png", "Wechselrichter PV-Anlage", new Point(0.5, 0.5), new Size(64,64), true, new SKColor(255,0,0), 1.0),
        new("zugang_spa_z.png", "Zugang SPA-Z", new Point(1.0, 0.5), new Size(89,64), true, new SKColor(0,50,204), 1.0),
        new("zuluft_absaugung_maschinell.png", "Zuluft / Absaugung maschinell", new Point(1.0, 0.5), new Size(141,64), true, new SKColor(220,220,50), 1.0),
        new("zuluft_abstroemung_natuerlich.png", "Zuluft / Abströmung natürlich", new Point(1.0, 0.5), new Size(141,64), true, new SKColor(220,220,50), 1.0),
        new("zusaetzlicher_zugang_feuerwehr.png", "Zusätzlicher Zugang Feuerwehr", new Point(1.0, 0.5), new Size(89,64), true, new SKColor(255,0,0), 1.0)
    ];
}
