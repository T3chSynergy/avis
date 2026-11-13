namespace Avis.Web.Domain;

public enum MaileingangErgebnis
{
    NeuerAuftrag = 0,
    Antwort = 1,
    WiederGeoeffnet = 2,
    InternerKommentar = 3,

    /// <summary>Bewusst nicht verarbeitet, zum Beispiel eine Abwesenheitsnotiz.</summary>
    Uebergangen = 4,
}

/// <summary>
/// Eine Mail aus dem Hotline-Postfach und was AVIS damit getan hat. Über die
/// Kennung der Mail ist sichergestellt, dass keine Mail zweimal verarbeitet wird.
/// </summary>
public class Maileingang
{
    public int Id { get; set; }

    /// <summary>Die Message-ID der Mail.</summary>
    public string Kennung { get; set; } = "";

    public DateTime EmpfangenAm { get; set; }
    public string? Von { get; set; }
    public string? Betreff { get; set; }
    public MaileingangErgebnis Ergebnis { get; set; }

    public int? AuftragId { get; set; }
    public Auftrag? Auftrag { get; set; }

    public string? Hinweis { get; set; }
}
