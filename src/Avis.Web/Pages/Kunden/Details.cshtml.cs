using System.Globalization;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Kunden;

public class DetailsModel(AvisDb db) : PageModel
{
    public const int Hoechstzahl = 50;

    public Kunde Kunde { get; private set; } = null!;
    public IReadOnlyList<Auftrag> Laufend { get; private set; } = [];
    public IReadOnlyList<Auftrag> Abgeschlossen { get; private set; } = [];
    public bool Gekuerzt { get; private set; }
    public List<SelectListItem> Standorte { get; private set; } = [];
    public IReadOnlyList<Inventargeraet> Geraete { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var kunde = await db.Kunden.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id);
        if (kunde is null)
            return NotFound();

        Kunde = kunde;
        Laufend = await db.AuftraegeFuer(User.BenutzerId()).FuerListe()
            .Where(a => a.KundeId == id && a.AbgeschlossenAm == null)
            .NachPrioritaet()
            .ToListAsync();

        var abgeschlossen = await db.AuftraegeFuer(User.BenutzerId()).FuerListe()
            .Where(a => a.KundeId == id && a.AbgeschlossenAm != null)
            .OrderByDescending(a => a.AbgeschlossenAm)
            .Take(Hoechstzahl + 1)
            .ToListAsync();
        Gekuerzt = abgeschlossen.Count > Hoechstzahl;
        Abgeschlossen = abgeschlossen.Take(Hoechstzahl).ToList();

        Geraete = await db.Geraete.AsNoTracking()
            .Where(g => g.KundeId == id && g.Aktiv)
            .OrderBy(g => g.Inventarnummer)
            .ToListAsync();

        Standorte = await db.Standorte.AsNoTracking()
            .Where(s => s.Aktiv || s.Id == kunde.StandortId)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(CultureInfo.InvariantCulture), s.Id == kunde.StandortId))
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string? name, string? email, string? telefon, string? organisationseinheit, int? standortId)
    {
        var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Id == id);
        if (kunde is null)
            return NotFound();

        // Was aus einer externen Quelle stammt, wird dort gepflegt. Hier ändert sich dann nur der Standort.
        var fehler = kunde.AusQuelle
            ? await KundeRegeln.UebernehmenAsync(db, kunde, kunde.Name, kunde.Email, kunde.Telefon, kunde.Organisationseinheit, standortId)
            : await KundeRegeln.UebernehmenAsync(db, kunde, name, email, telefon, organisationseinheit, standortId);
        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
        }
        else
        {
            await db.SaveChangesAsync();
            TempData["Meldung"] = $"{kunde.Name} wurde gespeichert.";
        }
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Markiert einen Kunden als inaktiv oder wieder als aktiv. Bei einem
    /// Kunden aus einer externen Quelle entscheidet das die Quelle.
    /// </summary>
    public async Task<IActionResult> OnPostAktivAsync(int id, bool aktiv)
    {
        var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Id == id);
        if (kunde is null)
            return NotFound();

        if (kunde.AusQuelle)
        {
            TempData["Fehler"] = $"{kunde.Name} stammt aus {kunde.Quelle}. Ob der Kunde aktiv ist, entscheidet sich dort.";
        }
        else
        {
            kunde.Aktiv = aktiv;
            await db.SaveChangesAsync();
            TempData["Meldung"] = aktiv ? $"{kunde.Name} ist wieder aktiv." : $"{kunde.Name} ist jetzt als inaktiv markiert.";
        }
        return RedirectToPage(new { id });
    }
}
