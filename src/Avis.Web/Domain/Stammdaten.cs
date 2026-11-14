namespace Avis.Web.Domain;

/// <summary>Anmeldekonto. Jeder, der sich anmeldet, bekommt eines.</summary>
public class Benutzer
{
    public int Id { get; set; }

    /// <summary>Anmeldename in Kleinbuchstaben, bei Windows-Anmeldung "domäne\name".</summary>
    public string Anmeldename { get; set; } = "";

    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public bool IstBearbeiter { get; set; }
    public bool IstAdministrator { get; set; }
    public bool Aktiv { get; set; } = true;

    public List<Gruppe> Gruppen { get; } = [];
}

/// <summary>Gruppe von Bearbeitern für einen Aufgabenbereich.</summary>
public class Gruppe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Beschreibung { get; set; }

    /// <summary>In dieser Gruppe landen neue Aufträge, wenn nichts anderes gewählt wird.</summary>
    public bool IstEingangsgruppe { get; set; }

    /// <summary>
    /// Nur der Administrator ordnet Mitglieder zu, und die Aufträge der
    /// Gruppe sehen nur ihre Mitglieder, der Erfasser, der Bearbeiter und der Kunde.
    /// </summary>
    public bool Gesperrt { get; set; }

    public bool Aktiv { get; set; } = true;

    public List<Benutzer> Mitglieder { get; } = [];
}

/// <summary>
/// Die Mitgliedschaft eines Bearbeiters in einer Gruppe, mit dem, was er für
/// diese Gruppe eingestellt hat. Ein- und ausgetragen wird weiter über
/// <see cref="Gruppe.Mitglieder"/>; mit dem Austragen verschwindet auch die
/// Einstellung.
/// </summary>
public class Gruppenmitglied
{
    public int GruppeId { get; set; }
    public int BenutzerId { get; set; }

    /// <summary>
    /// Ob das Mitglied eine Mail bekommt, wenn ein Auftrag neu in der Gruppe
    /// liegt. Aus, solange es das nicht selbst einschaltet.
    /// </summary>
    public bool MailBeiNeuemAuftrag { get; set; }
}

/// <summary>Der Anwender, für den ein Auftrag bearbeitet wird.</summary>
public class Kunde
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string? Organisationseinheit { get; set; }
    public string? Anmeldename { get; set; }

    public int? StandortId { get; set; }
    public Standort? Standort { get; set; }

    /// <summary>
    /// Wo der Kunde sitzt, zum Beispiel "A 105" für Raum 105 in Gebäude A.
    /// Freier Text; bei Kunden aus dem Verzeichnis steht er dort.
    /// </summary>
    public string? Raum { get; set; }

    /// <summary>Kennung in einem externen System, für einen späteren Abgleich.</summary>
    public string? ExterneKennung { get; set; }

    /// <summary>Name des externen Systems, aus dem der Eintrag stammt.</summary>
    public string? Quelle { get; set; }

    /// <summary>
    /// Ob Name, E-Mail, Telefon und Organisationseinheit in einem externen
    /// System gepflegt werden. Dann gewinnt dieses System: Der Abgleich
    /// überschreibt die Angaben, und in AVIS lassen sie sich nicht ändern.
    /// </summary>
    public bool AusQuelle => Quelle is not null;

    /// <summary>
    /// Ein inaktiver Kunde bleibt mit seinen Aufträgen erhalten, steht aber
    /// nicht mehr zur Auswahl, wenn ein Auftrag erfasst wird. Wer aus einer
    /// externen Quelle stammt, wird inaktiv, sobald er dort fehlt, und wieder
    /// aktiv, sobald er dort wieder steht. Alle anderen markiert ein
    /// Bearbeiter von Hand.
    /// </summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>
    /// Alles, wonach man einen Kunden sucht, in Kleinbuchstaben. Die Suche
    /// vergleicht nur mit diesem Feld und verhält sich dadurch in jeder
    /// Datenbank gleich, auch bei Umlauten. Wird beim Speichern gesetzt.
    /// </summary>
    public string Suchtext { get; private set; } = "";

    public void SuchtextAktualisieren()
    {
        var ziffern = new string((Telefon ?? "").Where(char.IsDigit).ToArray());
        var teile = new[] { Name, Email, Telefon, ziffern, Organisationseinheit, Anmeldename, Raum };
        Suchtext = string.Join(' ', teile.Where(t => !string.IsNullOrWhiteSpace(t))).ToLowerInvariant();
    }
}

/// <summary>Eintrag einer Auswahlliste, die der Administrator pflegt.</summary>
public interface IStammwert
{
    int Id { get; }
    string Name { get; set; }
    bool Aktiv { get; set; }
}

public class Kategorie : IStammwert
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Aktiv { get; set; } = true;
}

public class Standort : IStammwert
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Aktiv { get; set; } = true;
}
