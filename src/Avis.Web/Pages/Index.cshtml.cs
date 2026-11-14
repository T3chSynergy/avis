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

    /// <summary>
    /// Wie viele offene Aufträge mich als bevorzugten Bearbeiter nennen. Sie
    /// stehen bei den offenen Aufträgen, hier gibt es nur den Verweis dorthin.
    /// </summary>
    public int Vorgemerkt { get; private set; }

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

        Vorgemerkt = await db.AuftraegeFuer(ich).Offen().CountAsync(a => a.BevorzugterBearbeiterId == ich);
        return Page();
    }
}
