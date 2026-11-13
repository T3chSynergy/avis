using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Data;

public static class Abfragen
{
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

    public static IQueryable<Auftrag> Offen(this IQueryable<Auftrag> auftraege) =>
        auftraege.Where(a => a.AbgeschlossenAm == null && a.BearbeiterId == null);

    public static IQueryable<Auftrag> InBearbeitung(this IQueryable<Auftrag> auftraege) =>
        auftraege.Where(a => a.AbgeschlossenAm == null && a.BearbeiterId != null);
}
