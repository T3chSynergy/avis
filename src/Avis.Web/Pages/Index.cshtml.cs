using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages;

public class IndexModel(AvisDb db) : PageModel
{
    public IReadOnlyList<Auftrag> InBearbeitung { get; private set; } = [];
    public IReadOnlyList<Auftrag> FuerMich { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var ich = User.BenutzerId();

        InBearbeitung = await db.Auftraege.FuerListe()
            .InBearbeitung()
            .Where(a => a.BearbeiterId == ich)
            .NachPrioritaet()
            .ToListAsync();

        FuerMich = await db.Auftraege.FuerListe()
            .Offen()
            .Where(a => a.BevorzugterBearbeiterId == ich)
            .NachPrioritaet()
            .ToListAsync();
    }
}
