using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Geraete;

/// <summary>Liefert Vorschläge für das Feld Inventarnummer, während jemand tippt.</summary>
public class VorschlaegeModel(AvisDb db) : PageModel
{
    public IReadOnlyList<Inventargeraet> Geraete { get; private set; } = [];

    public async Task OnGetAsync(string? q)
    {
        if ((q?.Trim().Length ?? 0) < 2)
            return;

        Geraete = await db.Geraete.AsNoTracking()
            .Where(g => g.Aktiv)
            .Suchen(q)
            .Include(g => g.Standort)
            .Include(g => g.Kunde)
            .OrderBy(g => g.Inventarnummer)
            .Take(10)
            .ToListAsync();
    }
}
