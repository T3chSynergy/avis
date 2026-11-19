using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Verwaltung;

/// <summary>
/// Pflege der Kategorien: anlegen, umbenennen, aktivieren und deaktivieren. Gelöscht wird nichts, weil
/// bestehende Aufträge auf die Einträge verweisen.
/// </summary>
public class KategorienModel(AvisDb db) : PageModel
{
    public const int Hoechstlaenge = 100;

    public IReadOnlyList<Kategorie> Eintraege { get; private set; } = [];

    public async Task OnGetAsync() =>
        Eintraege = await db.Kategorien.AsNoTracking().OrderBy(e => e.Name).ToListAsync();

    public async Task<IActionResult> OnPostAnlegenAsync(string? name)
    {
        if (await NamePruefenAsync(name, ausser: null) is { } bereinigt)
        {
            db.Kategorien.Add(new Kategorie { Name = bereinigt });
            await db.SaveChangesAsync();
            TempData["Meldung"] = $"Kategorie {bereinigt} wurde angelegt.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSpeichernAsync(int id, string? name, bool aktiv)
    {
        var eintrag = await db.Kategorien.FirstOrDefaultAsync(e => e.Id == id);
        if (eintrag is null)
            return NotFound();

        if (await NamePruefenAsync(name, ausser: id) is { } bereinigt)
        {
            eintrag.Name = bereinigt;
            eintrag.Aktiv = aktiv;
            await db.SaveChangesAsync();
            TempData["Meldung"] = $"Kategorie {bereinigt} wurde gespeichert.";
        }
        return RedirectToPage();
    }

    private async Task<string?> NamePruefenAsync(string? name, int? ausser)
    {
        var bereinigt = name?.Trim() ?? "";
        if (bereinigt.Length == 0)
            TempData["Fehler"] = "Bitte einen Namen angeben.";
        else if (bereinigt.Length > Hoechstlaenge)
            TempData["Fehler"] = $"Der Name darf höchstens {Hoechstlaenge} Zeichen lang sein.";
        // Der Vergleich läuft in der Datenbank; ToLower() wird dort zu lower().
#pragma warning disable CA1304, CA1311, CA1862
        else if (await db.Kategorien.AnyAsync(e => e.Id != ausser && e.Name.ToLower() == bereinigt.ToLower()))
#pragma warning restore CA1304, CA1311, CA1862
            TempData["Fehler"] = $"Kategorie {bereinigt} gibt es schon.";
        else
            return bereinigt;

        return null;
    }
}
