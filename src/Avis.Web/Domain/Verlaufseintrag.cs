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
}

/// <summary>
/// Ein Eintrag im Verlauf eines Auftrags. Einträge werden nur angelegt, nie
/// geändert oder gelöscht.
/// </summary>
public class Verlaufseintrag
{
    private Verlaufseintrag() { }

    internal Verlaufseintrag(Auftrag auftrag, VerlaufArt art, Benutzer benutzer, DateTime zeitpunkt, string? beschreibung, string? text)
    {
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

    /// <summary>Der Text in Kleinbuchstaben, für die Suche.</summary>
    public string? Suchtext { get; private set; }

    internal void SuchtextAktualisieren() => Suchtext = Text?.ToLowerInvariant();

    /// <summary>
    /// Ob der Kunde den Eintrag sieht. Interne Kommentare und
    /// Weiterleitungen samt ihrem Text bleiben intern.
    /// </summary>
    public bool FuerKundenSichtbar => Art is
        VerlaufArt.Erfasst or
        VerlaufArt.NachrichtAnKunden or
        VerlaufArt.AntwortDesKunden or
        VerlaufArt.Abgeschlossen or
        VerlaufArt.WiederGeoeffnet;
}
