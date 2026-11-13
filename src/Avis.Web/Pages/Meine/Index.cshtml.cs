using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Meine;

/// <summary>Die Aufträge, die für die angemeldete Person als Kunde laufen.</summary>
public class IndexModel(AvisDb db, Kundenzuordnung zuordnung) : PageModel
{
    public const int Hoechstzahl = 50;

    public IReadOnlyList<Auftrag> Laufend { get; private set; } = [];
    public IReadOnlyList<Auftrag> Abgeschlossen { get; private set; } = [];
    public bool Gekuerzt { get; private set; }

    public async Task OnGetAsync()
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());

        Laufend = await db.Auftraege.AsNoTracking()
            .Where(a => a.KundeId == kunde.Id && a.AbgeschlossenAm == null)
            .OrderByDescending(a => a.Id)
            .ToListAsync();

        var abgeschlossen = await db.Auftraege.AsNoTracking()
            .Where(a => a.KundeId == kunde.Id && a.AbgeschlossenAm != null)
            .OrderByDescending(a => a.AbgeschlossenAm)
            .Take(Hoechstzahl + 1)
            .ToListAsync();
        Gekuerzt = abgeschlossen.Count > Hoechstzahl;
        Abgeschlossen = abgeschlossen.Take(Hoechstzahl).ToList();
    }
}
