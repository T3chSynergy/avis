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

    /// <summary>Lädt alles, was die Auftragsliste anzeigt.</summary>
    public static IQueryable<Auftrag> FuerListe(this IQueryable<Auftrag> auftraege) =>
        auftraege
            .AsNoTracking()
            .Include(a => a.Kunde)
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .Include(a => a.BevorzugterBearbeiter);

    /// <summary>Höchste Priorität zuerst, dann nach Termin, dann die ältesten.</summary>
    public static IOrderedQueryable<Auftrag> NachPrioritaet(this IQueryable<Auftrag> auftraege) =>
        auftraege
            .OrderByDescending(a => (int)a.Dringlichkeit + (int)a.Wichtigkeit)
            .ThenBy(a => a.Termin == null)
            .ThenBy(a => a.Termin)
            .ThenBy(a => a.Id);

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

    public static string[] Suchworte(string? suche) =>
        (suche ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    public static IQueryable<Auftrag> Offen(this IQueryable<Auftrag> auftraege) =>
        auftraege.Where(a => a.AbgeschlossenAm == null && a.BearbeiterId == null);

    public static IQueryable<Auftrag> InBearbeitung(this IQueryable<Auftrag> auftraege) =>
        auftraege.Where(a => a.AbgeschlossenAm == null && a.BearbeiterId != null);
}
