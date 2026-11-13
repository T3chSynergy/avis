using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Verwaltung;

public class GruppeBearbeitenModel(AvisDb db) : PageModel
{
    public Gruppe Gruppe { get; private set; } = null!;
    public int Laufend { get; private set; }
    public List<SelectListItem> Kandidaten { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var gruppe = await db.Gruppen.AsNoTracking()
            .Include(g => g.Mitglieder.Where(m => m.Aktiv))
            .FirstOrDefaultAsync(g => g.Id == id);
        if (gruppe is null)
            return NotFound();

        Gruppe = gruppe;
        Laufend = await LaufendAsync(id);

        var mitglieder = gruppe.Mitglieder.Select(m => m.Id).ToList();
        Kandidaten = await db.Benutzer.AsNoTracking()
            .Where(b => b.Aktiv && b.IstBearbeiter && !mitglieder.Contains(b.Id))
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.Id.ToString()))
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSpeichernAsync(int id, string? name, string? beschreibung, bool aktiv, bool eingangsgruppe)
    {
        var gruppe = await db.Gruppen.FirstOrDefaultAsync(g => g.Id == id);
        if (gruppe is null)
            return NotFound();

        // Die Eingangsgruppe gibt ihre Rolle nur ab, indem eine andere sie übernimmt.
        var wirdEingangsgruppe = gruppe.IstEingangsgruppe || eingangsgruppe;

        var fehler = await GruppeRegeln.NamePruefenAsync(db, name, ausser: id);
        if (fehler is null && !aktiv)
        {
            if (wirdEingangsgruppe)
                fehler = "Die Eingangsgruppe kann nicht deaktiviert werden.";
            else if (gruppe.Aktiv && await LaufendAsync(id) is > 0 and var laufend)
                fehler = $"Die Gruppe kann nicht deaktiviert werden, solange sie laufende Aufträge hat (derzeit {laufend}).";
        }

        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage(new { id });
        }

        gruppe.Name = name!.Trim();
        gruppe.Beschreibung = GruppeRegeln.Bereinigen(beschreibung);
        gruppe.Aktiv = aktiv;

        if (wirdEingangsgruppe && !gruppe.IstEingangsgruppe)
        {
            foreach (var bisherige in await db.Gruppen.Where(g => g.IstEingangsgruppe).ToListAsync())
                bisherige.IstEingangsgruppe = false;
            gruppe.IstEingangsgruppe = true;
        }

        await db.SaveChangesAsync();
        TempData["Meldung"] = $"Gruppe {gruppe.Name} wurde gespeichert.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMitgliedHinzufuegenAsync(int id, int benutzerId)
    {
        var gruppe = await db.Gruppen.Include(g => g.Mitglieder).FirstOrDefaultAsync(g => g.Id == id);
        var benutzer = await db.Benutzer.FirstOrDefaultAsync(b => b.Id == benutzerId && b.Aktiv && b.IstBearbeiter);
        if (gruppe is null || benutzer is null)
            return NotFound();

        if (!gruppe.Mitglieder.Contains(benutzer))
        {
            gruppe.Mitglieder.Add(benutzer);
            await db.SaveChangesAsync();
        }
        TempData["Meldung"] = $"{benutzer.Name} ist jetzt Mitglied der Gruppe {gruppe.Name}.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMitgliedEntfernenAsync(int id, int benutzerId)
    {
        var gruppe = await db.Gruppen.Include(g => g.Mitglieder).FirstOrDefaultAsync(g => g.Id == id);
        if (gruppe is null)
            return NotFound();

        var mitglied = gruppe.Mitglieder.FirstOrDefault(m => m.Id == benutzerId);
        if (mitglied is not null)
        {
            gruppe.Mitglieder.Remove(mitglied);
            await db.SaveChangesAsync();
            TempData["Meldung"] = $"{mitglied.Name} wurde aus der Gruppe {gruppe.Name} entfernt.";
        }
        return RedirectToPage(new { id });
    }

    private Task<int> LaufendAsync(int id) =>
        db.Auftraege.CountAsync(a => a.GruppeId == id && a.AbgeschlossenAm == null);
}
