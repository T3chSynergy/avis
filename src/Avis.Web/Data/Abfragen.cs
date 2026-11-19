using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Data;

public static class Abfragen
{
    /// <summary>
    /// Die Aufträge, die ein Bearbeiter sehen darf: alle außer denen in
    /// gesperrten Gruppen, in denen er nicht Mitglied ist. Was er selbst
    /// erfasst hat oder gerade bearbeitet, sieht er immer.
    /// Jede Seite für Bearbeiter fragt Aufträge hierüber ab.
    /// </summary>
    public static IQueryable<Auftrag> AuftraegeFuer(this AvisDb db, int benutzerId) =>
        db.Auftraege.Where(a =>
            !a.Gruppe.Gesperrt
            || a.Gruppe.Mitglieder.Any(m => m.Id == benutzerId)
            || a.ErfasstVonId == benutzerId
            || a.BearbeiterId == benutzerId);

    /// <summary>
    /// Lädt mit, was die Methoden des Auftrags lesen, wenn sie ihn ändern: Gruppe, Bearbeiter, Kunde und
    /// Kategorie. Fehlte eines davon, stünde im Verlauf ein halber Satz ("Übernommen von "), oder der
    /// bisherige Bearbeiter würde nicht erkannt. Jede Stelle, die einen Auftrag zum Ändern lädt, nimmt deshalb
    /// diese Abfrage.
    /// </summary>
    public static IQueryable<Auftrag> ZumAendern(this IQueryable<Auftrag> auftraege) =>
        auftraege
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .Include(a => a.Kunde)
            .Include(a => a.Kategorie);

    /// <summary>
    /// Der Bearbeiter, der an einem Auftrag in dieser Gruppe als bevorzugt genannt werden soll. In einer
    /// gesperrten Gruppe muss er Mitglied sein: Sonst bekäme jemand Hinweise und Mails zu einem Auftrag, den er
    /// nicht sehen darf.
    /// </summary>
    /// <exception cref="AuftragException">Es gibt ihn nicht, oder er gehört nicht zur gesperrten Gruppe.</exception>
    public static async Task<Benutzer> BevorzugterBearbeiterAsync(this AvisDb db, int benutzerId, Gruppe? gruppe)
    {
        var bevorzugt = await db.Benutzer.FirstOrDefaultAsync(b => b.Id == benutzerId && b.Aktiv && b.IstBearbeiter)
            ?? throw new AuftragException("Der bevorzugte Bearbeiter wurde nicht gefunden.");
        if (gruppe is { Gesperrt: true }
            && !await db.Gruppen.AnyAsync(g => g.Id == gruppe.Id && g.Mitglieder.Any(m => m.Id == bevorzugt.Id)))
            throw new AuftragException($"{bevorzugt.Name} ist kein Mitglied der gesperrten Gruppe {gruppe.Name}.");
        return bevorzugt;
    }

    /// <summary>Lädt alles, was die Auftragsliste anzeigt.</summary>
    public static IQueryable<Auftrag> FuerListe(this IQueryable<Auftrag> auftraege) =>
        auftraege
            .AsNoTracking()
            .Include(a => a.Kunde)
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .Include(a => a.BevorzugterBearbeiter);

    /// <summary>
    /// Höchste Priorität zuerst. Bei gleicher Priorität stehen Aufträge vorn,
    /// bei denen der Kunde auf eine Reaktion wartet, dann zählen Termin und Alter.
    /// </summary>
    public static IOrderedQueryable<Auftrag> NachPrioritaet(this IQueryable<Auftrag> auftraege) =>
        auftraege
            .OrderByDescending(a => (int)a.Dringlichkeit + (int)a.Wichtigkeit)
            .ThenBy(a => a.KundeWartetSeit == null)
            .ThenBy(a => a.Termin == null)
            .ThenBy(a => a.Termin)
            .ThenBy(a => a.Id);

    /// <summary>Benutzer, bei denen jedes Wort der Suche in Name, Anmeldename oder E-Mail vorkommt.</summary>
    public static IQueryable<Benutzer> Suchen(this IQueryable<Benutzer> benutzer, string? suche)
    {
        foreach (var wort in Suchworte(suche))
            benutzer = benutzer.Where(b => b.Suchtext.Contains(wort));
        return benutzer;
    }

    /// <summary>
    /// Kunden, bei denen jedes Wort der Suche in Name, E-Mail, Telefon,
    /// Organisationseinheit oder Anmeldename vorkommt.
    /// </summary>
    public static IQueryable<Kunde> Suchen(this IQueryable<Kunde> kunden, string? suche)
    {
        foreach (var wort in Suchworte(suche))
            kunden = kunden.Where(k => k.Suchtext.Contains(wort));
        return kunden;
    }

    /// <summary>
    /// Aufträge, bei denen jedes Wort der Suche in Betreff, Beschreibung,
    /// Gerät, beim Kunden oder in einem Text des Verlaufs vorkommt. Eine Zahl
    /// trifft auch die Auftragsnummer.
    /// </summary>
    public static IQueryable<Auftrag> Suchen(this IQueryable<Auftrag> auftraege, string? suche)
    {
        foreach (var wort in Suchworte(suche))
        {
            var nummer = Auftragsnummer(wort) ?? -1;
            auftraege = auftraege.Where(a =>
                a.Id == nummer
                || a.Suchtext.Contains(wort)
                || a.Kunde.Suchtext.Contains(wort)
                || a.Geraete.Any(g => g.Suchtext.Contains(wort))
                || a.Verlauf.Any(v => v.Suchtext != null && v.Suchtext.Contains(wort)));
        }
        return auftraege;
    }

    /// <summary>Geräte, bei denen jedes Wort der Suche in Inventarnummer, Bezeichnung, Typ oder beim Nutzer vorkommt.</summary>
    public static IQueryable<Inventargeraet> Suchen(this IQueryable<Inventargeraet> geraete, string? suche)
    {
        foreach (var wort in Suchworte(suche))
            geraete = geraete.Where(g => g.Suchtext.Contains(wort) || g.Kunde!.Suchtext.Contains(wort));
        return geraete;
    }

    /// <summary>Liest "123" oder "#123" als Auftragsnummer.</summary>
    public static int? Auftragsnummer(string? text) =>
        int.TryParse(text?.Trim().TrimStart('#'), out var nummer) && nummer > 0 ? nummer : null;

    public static string[] Suchworte(string? suche) =>
        (suche ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    public static IQueryable<Auftrag> Offen(this IQueryable<Auftrag> auftraege) =>
        auftraege.Where(a => a.AbgeschlossenAm == null && a.BearbeiterId == null);

    public static IQueryable<Auftrag> InBearbeitung(this IQueryable<Auftrag> auftraege) =>
        auftraege.Where(a => a.AbgeschlossenAm == null && a.BearbeiterId != null);
}
