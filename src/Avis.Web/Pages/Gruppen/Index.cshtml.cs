using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Gruppen;

public class IndexModel(AvisDb db) : PageModel
{
    public IReadOnlyList<Gruppe> Gruppen { get; private set; } = [];
    public Dictionary<int, IReadOnlyList<Auftrag>> Offen { get; } = [];

    public async Task OnGetAsync()
    {
        var ich = User.BenutzerId();

        Gruppen = await db.Gruppen.AsNoTracking()
            .Where(g => g.Aktiv && g.Mitglieder.Any(m => m.Id == ich))
            .OrderBy(g => g.Name)
            .ToListAsync();

        var ids = Gruppen.Select(g => g.Id).ToList();
        var auftraege = await db.Auftraege.FuerListe()
            .Offen()
            .Where(a => ids.Contains(a.GruppeId))
            .NachPrioritaet()
            .ToListAsync();

        foreach (var gruppe in Gruppen)
            Offen[gruppe.Id] = auftraege.Where(a => a.GruppeId == gruppe.Id).ToList();
    }
}
