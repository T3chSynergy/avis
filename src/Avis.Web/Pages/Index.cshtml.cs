using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages;

public class IndexModel(AvisDb db) : PageModel
{
    public IReadOnlyList<Auftrag> InBearbeitung { get; private set; } = [];
    public IReadOnlyList<Auftrag> FuerMich { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        // Wer kein Bearbeiter ist, landet bei den eigenen Aufträgen.
        if (!User.IstBearbeiter())
            return RedirectToPage("/Meine/Index");

        var ich = User.BenutzerId();

        InBearbeitung = await db.AuftraegeFuer(ich).FuerListe()
            .InBearbeitung()
            .Where(a => a.BearbeiterId == ich)
            .NachPrioritaet()
            .ToListAsync();

        FuerMich = await db.AuftraegeFuer(ich).FuerListe()
            .Offen()
            .Where(a => a.BevorzugterBearbeiterId == ich)
            .NachPrioritaet()
            .ToListAsync();
        return Page();
    }
}
