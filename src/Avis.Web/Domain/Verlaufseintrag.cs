namespace Avis.Web.Domain;

public enum VerlaufArt
{
    Erfasst = 0,
    Uebernommen = 1,
    Weitergeleitet = 2,
    InternerKommentar = 3,
    NachrichtAnKunden = 4,
    AntwortDesKunden = 5,
    Geaendert = 6,
    Abgeschlossen = 7,
    WiederGeoeffnet = 8,
    VomKundenWiederGeoeffnet = 9,
}

/// <summary>
/// Ein Eintrag im Verlauf eines Auftrags. Einträge werden nur angelegt und
/// nie gelöscht. Ändern lässt sich allein der Text eines internen Eintrags,
/// und nur durch den, der ihn geschrieben hat (siehe
/// <see cref="Auftrag.EintragAendern"/>). Was ein Kunde gesehen oder per
/// Mail bekommen hat, bleibt, wie es war.
/// </summary>
public class Verlaufseintrag
{
    private readonly List<Anhang> _anhaenge = [];

    private Verlaufseintrag() { }

    internal Verlaufseintrag(
        Auftrag auftrag,
        VerlaufArt art,
        Benutzer benutzer,
        DateTime zeitpunkt,
        string? beschreibung,
        string? text,
        IEnumerable<Anhang>? anhaenge = null)
    {
        if (anhaenge is not null)
            _anhaenge.AddRange(anhaenge);
        Auftrag = auftrag;
        Art = art;
        Benutzer = benutzer;
        BenutzerId = benutzer.Id;
        Zeitpunkt = zeitpunkt;
        Beschreibung = beschreibung;
        Text = text;
        SuchtextAktualisieren();
    }

    public int Id { get; private set; }

    public int AuftragId { get; private set; }
    public Auftrag Auftrag { get; private set; } = null!;

    public VerlaufArt Art { get; private set; }
    public DateTime Zeitpunkt { get; private set; }

    public int BenutzerId { get; private set; }
    public Benutzer Benutzer { get; private set; } = null!;

    /// <summary>
    /// Vom System erzeugter Text, z. B. "Weitergeleitet an Gruppe Netzwerk".
    /// Namen stehen hier ausgeschrieben, damit der Verlauf auch nach einer
    /// Umbenennung stimmt.
    /// </summary>
    public string? Beschreibung { get; private set; }

    /// <summary>Vom Benutzer geschriebener Text.</summary>
    public string? Text { get; private set; }

    /// <summary>
    /// Wann der Verfasser den Text zuletzt geändert hat. Leer, solange der
    /// Text der ursprüngliche ist. Der bisherige Text wird nicht aufbewahrt.
    /// </summary>
    public DateTime? GeaendertAm { get; private set; }

    public IReadOnlyList<Anhang> Anhaenge => _anhaenge;

    /// <summary>Der Text in Kleinbuchstaben, für die Suche.</summary>
    public string? Suchtext { get; private set; }

    internal void SuchtextAktualisieren() => Suchtext = Text?.ToLowerInvariant();

    /// <summary>Ob der Verfasser den Text eines Eintrags dieser Art ändern kann: nur Internes.</summary>
    public static bool TextAenderbar(VerlaufArt art) => art is VerlaufArt.InternerKommentar or VerlaufArt.Weitergeleitet;

    internal void TextAendern(string? text, DateTime jetzt)
    {
        Text = text;
        GeaendertAm = jetzt;
        SuchtextAktualisieren();
    }

    /// <summary>
    /// Ob der Kunde den Eintrag sieht. Interne Kommentare und
    /// Weiterleitungen samt ihrem Text bleiben intern.
    /// </summary>
    public bool FuerKundenSichtbar => Art is
        VerlaufArt.Erfasst or
        VerlaufArt.NachrichtAnKunden or
        VerlaufArt.AntwortDesKunden or
        VerlaufArt.Abgeschlossen or
        VerlaufArt.WiederGeoeffnet or
        VerlaufArt.VomKundenWiederGeoeffnet;
}
