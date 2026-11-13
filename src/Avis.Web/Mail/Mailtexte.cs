using Avis.Web.Domain;

namespace Avis.Web.Mail;

public record Mailinhalt(string An, string AnName, string Betreff, string Text);

/// <summary>
/// Die Texte aller Mails. Mails an Kunden enthalten nie interne Kommentare,
/// Weiterleitungen oder Gruppen.
/// </summary>
public static class Mailtexte
{
    private const string Fusszeile = "Diese Mail wurde automatisch von AVIS versendet.";

    /// <summary>
    /// Erstellt die Mail zu einem Verlaufseintrag. Auftrag, Kunde, Gruppe,
    /// Bearbeiter und bevorzugter Bearbeiter müssen geladen sein. Gibt es
    /// nichts zu senden, ist die Mail null und der Grund gesetzt.
    /// </summary>
    public static (Mailinhalt? Mail, string? Grund) Erstellen(Verlaufseintrag eintrag, string? basisUrl)
    {
        var auftrag = eintrag.Auftrag;
        var basis = string.IsNullOrWhiteSpace(basisUrl) ? null : basisUrl.TrimEnd('/');
        var fuerKunden = basis is null ? null : $"{basis}/Meine/{auftrag.Id}";
        var fuerBearbeiter = basis is null ? null : $"{basis}/Auftraege/{auftrag.Id}";
        var kunde = auftrag.Kunde;
        var anhaenge = eintrag.Anhaenge.Count == 0
            ? null
            : "Anhänge, abrufbar im Auftrag: " + string.Join(", ", eintrag.Anhaenge.OrderBy(a => a.Id).Select(a => a.Dateiname));

        switch (eintrag.Art)
        {
            case VerlaufArt.Erfasst:
                return AnKunden(kunde, auftrag, "Erfasst",
                    $"der Auftrag {auftrag.Id} wurde erfasst und liegt bei der IT.",
                    $"Betreff: {auftrag.Betreff}",
                    Link("Stand und Nachrichten zum Auftrag:", fuerKunden));

            case VerlaufArt.NachrichtAnKunden:
                return AnKunden(kunde, auftrag, "Nachricht",
                    $"zum Auftrag {auftrag.Id} „{auftrag.Betreff}“ gibt es eine Nachricht der IT:",
                    eintrag.Text,
                    anhaenge,
                    Link("Antworten ist hier möglich:", fuerKunden));

            case VerlaufArt.Abgeschlossen:
                return AnKunden(kunde, auftrag, "Abgeschlossen",
                    $"der Auftrag {auftrag.Id} „{auftrag.Betreff}“ wurde abgeschlossen.",
                    eintrag.Text,
                    Link("Der Auftrag zum Nachlesen:", fuerKunden));

            case VerlaufArt.AntwortDesKunden:
                if (auftrag.Bearbeiter is null)
                    return (null, "Der Auftrag hat keinen zuständigen Bearbeiter. Die Antwort steht im Verlauf.");
                return AnBearbeiter(auftrag.Bearbeiter, auftrag, "Antwort",
                    $"{kunde.Name} hat zum Auftrag {auftrag.Id} „{auftrag.Betreff}“ geantwortet:",
                    eintrag.Text,
                    anhaenge,
                    fuerBearbeiter);

            case VerlaufArt.Weitergeleitet:
                if (auftrag.Status != AuftragStatus.Offen || auftrag.BevorzugterBearbeiter is null)
                    return (null, "Der Auftrag wurde inzwischen übernommen oder erneut weitergeleitet.");
                return AnBearbeiter(auftrag.BevorzugterBearbeiter, auftrag, "Für dich",
                    $"der Auftrag {auftrag.Id} „{auftrag.Betreff}“ liegt in der Gruppe {auftrag.Gruppe.Name} und nennt dich als bevorzugten Bearbeiter.",
                    eintrag.Text,
                    fuerBearbeiter);

            case VerlaufArt.VomKundenWiederGeoeffnet:
                if (auftrag.Status != AuftragStatus.Offen || auftrag.BevorzugterBearbeiter is null)
                    return (null, "Der Auftrag hat keinen bisherigen Bearbeiter oder wurde inzwischen übernommen. Er liegt offen in seiner Gruppe.");
                return AnBearbeiter(auftrag.BevorzugterBearbeiter, auftrag, "Wieder geöffnet",
                    $"{kunde.Name} hat den abgeschlossenen Auftrag {auftrag.Id} „{auftrag.Betreff}“ wieder geöffnet:",
                    eintrag.Text,
                    anhaenge,
                    $"Er liegt in der Gruppe {auftrag.Gruppe.Name} und nennt dich als bevorzugten Bearbeiter.",
                    fuerBearbeiter);

            default:
                return (null, "Zu dieser Art von Eintrag wird keine Mail gesendet.");
        }
    }

    private static (Mailinhalt?, string?) AnKunden(Kunde kunde, Auftrag auftrag, string anlass, params string?[] absaetze)
    {
        if (kunde.Email is null)
            return (null, $"Für {kunde.Name} ist keine E-Mail-Adresse hinterlegt.");

        return (new Mailinhalt(kunde.Email, kunde.Name, Betreff(auftrag, anlass), Text($"Guten Tag {kunde.Name},", absaetze)), null);
    }

    private static (Mailinhalt?, string?) AnBearbeiter(Benutzer bearbeiter, Auftrag auftrag, string anlass, params string?[] absaetze)
    {
        if (bearbeiter.Email is null)
            return (null, $"Für {bearbeiter.Name} ist keine E-Mail-Adresse hinterlegt.");

        return (new Mailinhalt(bearbeiter.Email, bearbeiter.Name, Betreff(auftrag, anlass), Text($"Hallo {bearbeiter.Name},", absaetze)), null);
    }

    /// <summary>Die Auftragsnummer steht in eckigen Klammern vorn, damit Antworten später zugeordnet werden können.</summary>
    private static string Betreff(Auftrag auftrag, string anlass) =>
        $"[AVIS-{auftrag.Id}] {anlass}: {auftrag.Betreff}";

    private static string? Link(string einleitung, string? adresse) =>
        adresse is null ? null : $"{einleitung}\n{adresse}";

    private static string Text(string anrede, string?[] absaetze)
    {
        var teile = new List<string> { anrede };
        teile.AddRange(absaetze.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!.Trim()));
        teile.Add(Fusszeile);
        return string.Join("\n\n", teile) + "\n";
    }
}
