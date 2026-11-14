namespace Avis.Web.Domain;

/// <summary>
/// Der letzte Lauf eines Abgleichs, je Art genau einer. Er steht in der
/// Datenbank, damit die Verwaltung ihn auch nach einem Neustart zeigt und
/// AVIS weiß, ob der tägliche Lauf schon war.
/// </summary>
public class Abgleichlauf
{
    public const string Kunden = "Kunden";
    public const string Geraete = "Geraete";

    public string Art { get; set; } = "";

    /// <summary>Wann der Abgleich zuletzt lief, ob mit oder ohne Erfolg.</summary>
    public DateTime Zeitpunkt { get; set; }

    /// <summary>Wann er zuletzt ohne Fehler lief.</summary>
    public DateTime? LetzterErfolg { get; set; }

    public int Gelesen { get; set; }
    public int Neu { get; set; }
    public int Geaendert { get; set; }
    public int Reaktiviert { get; set; }
    public int Inaktiv { get; set; }

    /// <summary>Warum der letzte Lauf nichts geändert hat. Leer, wenn er gelang.</summary>
    public string? Fehler { get; set; }

    /// <summary>Was der letzte Lauf ausgelassen hat, eine Zeile je Hinweis.</summary>
    public string? Hinweise { get; set; }
}
