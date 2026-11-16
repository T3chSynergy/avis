using Microsoft.Extensions.Options;

namespace Avis.Web;

public class TelefonbuchOptionen
{
    /// <summary>Der Platzhalter in <see cref="Adresse"/>, an dessen Stelle die Kennung der Person tritt.</summary>
    public const string Platzhalter = "{kennung}";

    /// <summary>
    /// Adresse der Seite einer Person in einem Telefonbuch oder Verzeichnis
    /// des Hauses, zum Beispiel https://telefonbuch.example.org/person/{kennung}.
    /// Fehlt der Platzhalter, wird die Kennung angehängt. Leer: kein Verweis.
    /// </summary>
    public string? Adresse { get; set; }

    /// <summary>Wie der Verweis in der Oberfläche heißt.</summary>
    public string Bezeichnung { get; set; } = "Telefonbuch";
}

/// <summary>
/// Der Verweis von einem Kunden oder Bearbeiter auf seine Seite in einem
/// Telefonbuch des Hauses, wo etwa Vertretung und Anwesenheit stehen. AVIS
/// baut nur die Adresse und liest dort nichts.
/// </summary>
public class Telefonbuch(IOptions<TelefonbuchOptionen> optionen)
{
    public string Bezeichnung => string.IsNullOrWhiteSpace(optionen.Value.Bezeichnung) ? "Telefonbuch" : optionen.Value.Bezeichnung.Trim();

    /// <summary>Die Adresse der Seite dieser Person, oder null, wenn nichts eingestellt ist oder die Kennung fehlt.</summary>
    public string? AdresseFuer(string? anmeldename)
    {
        var vorlage = optionen.Value.Adresse?.Trim();
        if (string.IsNullOrEmpty(vorlage) || Kennung(anmeldename) is not { } kennung)
            return null;

        var teil = Uri.EscapeDataString(kennung);
        var adresse = vorlage.Contains(TelefonbuchOptionen.Platzhalter, StringComparison.OrdinalIgnoreCase)
            ? vorlage.Replace(TelefonbuchOptionen.Platzhalter, teil, StringComparison.OrdinalIgnoreCase)
            : vorlage + teil;
        return Uri.TryCreate(adresse, UriKind.Absolute, out var ziel) && ziel.Scheme is "https" or "http" ? adresse : null;
    }

    /// <summary>
    /// Die Kennung ohne die Domäne: "musteruser" aus "domäne\musteruser" und
    /// aus "musteruser@example.org". Konten von AVIS selbst, etwa das des
    /// E-Mail-Eingangs, haben keine.
    /// </summary>
    public static string? Kennung(string? anmeldename)
    {
        var name = anmeldename?.Trim() ?? "";
        name = name[(name.LastIndexOf('\\') + 1)..];
        var at = name.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0)
            name = name[..at];
        return name.Length == 0 || name.Contains(':', StringComparison.Ordinal) ? null : name;
    }
}
