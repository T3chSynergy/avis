namespace Avis.Web.Domain;

/// <summary>
/// Eine Datei an einem Verlaufseintrag. Wer den Eintrag sieht, sieht auch
/// seine Anhänge: Anhänge an internen Kommentaren bleiben intern, Anhänge an
/// Nachrichten und am Auftrag selbst sieht auch der Kunde. Die Datei liegt im
/// Dateisystem, hier stehen nur ihre Angaben.
/// </summary>
public class Anhang
{
    private Anhang() { }

    public Anhang(string dateiname, string? inhaltstyp, long groesse, string ablagename, string? bildtyp = null)
    {
        Dateiname = dateiname;
        Inhaltstyp = inhaltstyp;
        Groesse = groesse;
        Ablagename = ablagename;
        Bildtyp = bildtyp;
    }

    public int Id { get; private set; }

    public int VerlaufseintragId { get; private set; }
    public Verlaufseintrag Verlaufseintrag { get; private set; } = null!;

    /// <summary>Der Name, unter dem die Datei hochgeladen wurde.</summary>
    public string Dateiname { get; private set; } = "";

    /// <summary>Der Typ, den der Absender genannt hat. Darauf verlässt sich AVIS nicht.</summary>
    public string? Inhaltstyp { get; private set; }

    /// <summary>
    /// Gesetzt, wenn der Inhalt der Datei ein Bild ist, das sich gefahrlos
    /// anzeigen lässt (PNG, JPEG, GIF oder WebP). Am Inhalt erkannt, nicht am
    /// Namen. Nur solche Anhänge zeigt AVIS als Bild, alle anderen gibt es
    /// nur zum Herunterladen.
    /// </summary>
    public string? Bildtyp { get; private set; }
    public long Groesse { get; private set; }

    /// <summary>Ort der Datei unterhalb des Anhangsordners. Von AVIS vergeben, nie vom Benutzer.</summary>
    public string Ablagename { get; private set; } = "";
}
