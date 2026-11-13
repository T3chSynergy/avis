using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Geraete;

public class IndexModel(AvisDb db) : PageModel
{
    public const int Hoechstzahl = 100;

    public record Zeile(int Id, string Inventarnummer, string Bezeichnung, string? Typ, string? Standort, string? Nutzer, bool Aktiv, int Laufend);

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public IReadOnlyList<Zeile> Geraete { get; private set; } = [];
    public bool Gekuerzt { get; private set; }

    public async Task OnGetAsync()
    {
        var sichtbar = db.AuftraegeFuer(User.BenutzerId());
        var geraete = await db.Geraete.AsNoTracking()
            .Suchen(Q)
            .OrderByDescending(g => g.Aktiv).ThenBy(g => g.Inventarnummer)
            .Take(Hoechstzahl + 1)
            .Select(g => new Zeile(
                g.Id,
                g.Inventarnummer,
                g.Bezeichnung,
                g.Typ,
                g.Standort!.Name,
                g.Kunde!.Name,
                g.Aktiv,
                sichtbar.Count(a => a.InventargeraetId == g.Id && a.AbgeschlossenAm == null)))
            .ToListAsync();

        Gekuerzt = geraete.Count > Hoechstzahl;
        Geraete = geraete.Take(Hoechstzahl).ToList();
    }
}
