namespace Avis.Web.Domain;

/// <summary>
/// Ein Gerät aus dem Inventar, mit dem Aufträge verknüpft werden können.
/// Die Verknüpfung ist nie Pflicht, das Freitextfeld am Auftrag bleibt daneben
/// bestehen. Externe Kennung und Quelle bereiten einen späteren Abgleich mit
/// einem externen System vor.
/// </summary>
public class Inventargeraet
{
    public int Id { get; set; }

    /// <summary>So, wie sie auf dem Gerät steht.</summary>
    public string Inventarnummer { get; set; } = "";

    /// <summary>Die Inventarnummer in Kleinbuchstaben. Sie ist eindeutig.</summary>
    public string Nummernschluessel { get; private set; } = "";

    public string Bezeichnung { get; set; } = "";
    public string? Typ { get; set; }

    public int? StandortId { get; set; }
    public Standort? Standort { get; set; }

    /// <summary>
    /// Wo das Gerät steht, zum Beispiel "A 105" für Raum 105 in Gebäude A.
    /// Freier Text; bei Geräten aus einem externen System steht er dort.
    /// </summary>
    public string? Raum { get; set; }

    /// <summary>Der Kunde, der das Gerät nutzt.</summary>
    public int? KundeId { get; set; }
    public Kunde? Kunde { get; set; }

    public bool Aktiv { get; set; } = true;

    public string? ExterneKennung { get; set; }
    public string? Quelle { get; set; }

    /// <summary>
    /// Ob das Gerät in einem externen System gepflegt wird. Dann gewinnt
    /// dieses System: Der Abgleich überschreibt Inventarnummer, Bezeichnung,
    /// Typ, Raum, Nutzer und ob es in Betrieb ist, und in AVIS lässt sich
    /// davon nichts ändern. Nur der Standort gehört AVIS.
    /// </summary>
    public bool AusQuelle => Quelle is not null;

    /// <summary>Inventarnummer, Bezeichnung und Typ in Kleinbuchstaben, für die Suche. Wird beim Speichern gesetzt.</summary>
    public string Suchtext { get; private set; } = "";

    public static string Schluessel(string inventarnummer) => inventarnummer.Trim().ToLowerInvariant();

    public void SuchtextAktualisieren()
    {
        Nummernschluessel = Schluessel(Inventarnummer);
        Suchtext = string.Join(' ', new[] { Inventarnummer, Bezeichnung, Typ, Raum }.Where(t => !string.IsNullOrWhiteSpace(t))).ToLowerInvariant();
    }
}
