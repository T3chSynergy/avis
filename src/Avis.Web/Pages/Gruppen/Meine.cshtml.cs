using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Gruppen;

public class MeineModel(AvisDb db) : PageModel
{
    public record Zeile(int Id, string Name, bool IstMitglied, bool Gesperrt, string Mitglieder);

    public IReadOnlyList<Zeile> Gruppen { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var ich = User.BenutzerId();
        var gruppen = await db.Gruppen.AsNoTracking()
            .Where(g => g.Aktiv)
            .Include(g => g.Mitglieder)
            .OrderBy(g => g.Name)
            .ToListAsync();

        Gruppen = gruppen
            .Select(g => new Zeile(
                g.Id,
                g.Name,
                g.Mitglieder.Any(m => m.Id == ich),
                g.Gesperrt,
                string.Join(", ", g.Mitglieder.Where(m => m.Aktiv).OrderBy(m => m.Name).Select(m => m.Name))))
            .ToList();
    }

    public Task<IActionResult> OnPostBeitretenAsync(int id) => AendernAsync(id, beitreten: true);

    public Task<IActionResult> OnPostAustretenAsync(int id) => AendernAsync(id, beitreten: false);

    private async Task<IActionResult> AendernAsync(int id, bool beitreten)
    {
        var ich = User.BenutzerId();
        var gruppe = await db.Gruppen.Include(g => g.Mitglieder).FirstOrDefaultAsync(g => g.Id == id && g.Aktiv);
        if (gruppe is null)
            return NotFound();
        if (gruppe.Gesperrt)
        {
            TempData["Fehler"] = $"Die Gruppe {gruppe.Name} ist gesperrt. Mitglieder ordnet der Administrator zu.";
            return RedirectToPage();
        }

        var mitglied = gruppe.Mitglieder.FirstOrDefault(m => m.Id == ich);
        if (beitreten && mitglied is null)
            gruppe.Mitglieder.Add(await db.Benutzer.FirstAsync(b => b.Id == ich));
        else if (!beitreten && mitglied is not null)
            gruppe.Mitglieder.Remove(mitglied);

        await db.SaveChangesAsync();
        TempData["Meldung"] = beitreten
            ? $"Du bist jetzt Mitglied der Gruppe {gruppe.Name}."
            : $"Du bist aus der Gruppe {gruppe.Name} ausgetreten.";
        return RedirectToPage();
    }
}
