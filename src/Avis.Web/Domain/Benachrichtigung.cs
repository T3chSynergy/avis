namespace Avis.Web.Domain;

public enum BenachrichtigungStatus
{
    /// <summary>Wartet auf den Versand oder den nächsten Versuch.</summary>
    Offen = 0,
    Gesendet = 1,

    /// <summary>Nach mehreren Versuchen aufgegeben.</summary>
    Fehlgeschlagen = 2,

    /// <summary>Es gab nichts zu senden, zum Beispiel weil keine Adresse hinterlegt ist.</summary>
    Entfallen = 3,
}

/// <summary>
/// Eine Mail, die zu einem Verlaufseintrag zu senden ist. Sie entsteht
/// zusammen mit dem Eintrag in derselben Speicherung und wird danach im
/// Hintergrund versendet. Ist der Mailserver nicht erreichbar, geht so nichts
/// verloren und die Aktion des Benutzers gelingt trotzdem.
/// </summary>
public class Benachrichtigung
{
    public int Id { get; set; }

    public int VerlaufseintragId { get; set; }
    public Verlaufseintrag Verlaufseintrag { get; set; } = null!;

    /// <summary>
    /// Der Hinweis an den bevorzugten Bearbeiter eines neu erfassten Auftrags.
    /// Zum selben Eintrag gibt es dann noch die Mail an den Kunden.
    /// </summary>
    public bool AnBevorzugtenBearbeiter { get; set; }

    /// <summary>
    /// Die Id des Benutzers bei der Mail an ein Mitglied der Gruppe, das sich
    /// über neue Aufträge benachrichtigen lässt. Sonst ergibt sich der
    /// Empfänger aus dem Eintrag.
    ///
    /// Bewusst nur die Nummer, ohne Verknüpfung in der Datenbank: Eine
    /// Verknüpfung ließe sich in SQLite nur durch den Umbau der ganzen
    /// Tabelle nachrüsten, und der läuft nicht in einer Transaktion. Konten
    /// werden nie gelöscht, nur deaktiviert.
    /// </summary>
    public int? EmpfaengerId { get; set; }

    public BenachrichtigungStatus Status { get; set; }
    public DateTime ErstelltAm { get; set; }
    public DateTime NaechsterVersuchAm { get; set; }
    public DateTime? GesendetAm { get; set; }
    public int Versuche { get; set; }

    /// <summary>Empfänger und Betreff stehen fest, sobald die Mail erstellt wurde.</summary>
    public string? An { get; set; }
    public string? Betreff { get; set; }

    /// <summary>Der letzte Fehler oder der Grund, warum nichts gesendet wurde.</summary>
    public string? Hinweis { get; set; }
}
