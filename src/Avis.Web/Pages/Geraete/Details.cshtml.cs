using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Geraete;

public class DetailsModel(AvisDb db) : PageModel
{
    public const int Hoechstzahl = 50;

    public Inventargeraet Geraet { get; private set; } = null!;
    public IReadOnlyList<Auftrag> Laufend { get; private set; } = [];
    public IReadOnlyList<Auftrag> Abgeschlossen { get; private set; } = [];
    public bool Gekuerzt { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var geraet = await db.Geraete.AsNoTracking()
            .Include(g => g.Standort)
            .Include(g => g.Kunde)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (geraet is null)
            return NotFound();

        Geraet = geraet;
        var auftraege = db.AuftraegeFuer(User.BenutzerId()).FuerListe().Where(a => a.InventargeraetId == id);
        Laufend = await auftraege.Where(a => a.AbgeschlossenAm == null).NachPrioritaet().ToListAsync();

        var abgeschlossen = await auftraege
            .Where(a => a.AbgeschlossenAm != null)
            .OrderByDescending(a => a.AbgeschlossenAm)
            .Take(Hoechstzahl + 1)
            .ToListAsync();
        Gekuerzt = abgeschlossen.Count > Hoechstzahl;
        Abgeschlossen = abgeschlossen.Take(Hoechstzahl).ToList();
        return Page();
    }
}
