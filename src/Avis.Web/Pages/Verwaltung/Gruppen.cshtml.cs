using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Verwaltung;

public class GruppenModel(AvisDb db) : PageModel
{
    public record Zeile(int Id, string Name, string? Beschreibung, bool Aktiv, bool IstEingangsgruppe, bool Gesperrt, int Mitglieder, int Laufend);

    public IReadOnlyList<Zeile> Gruppen { get; private set; } = [];

    public async Task OnGetAsync() =>
        Gruppen = await db.Gruppen.AsNoTracking()
            .OrderByDescending(g => g.Aktiv).ThenBy(g => g.Name)
            .Select(g => new Zeile(
                g.Id,
                g.Name,
                g.Beschreibung,
                g.Aktiv,
                g.IstEingangsgruppe,
                g.Gesperrt,
                g.Mitglieder.Count(m => m.Aktiv),
                db.Auftraege.Count(a => a.GruppeId == g.Id && a.AbgeschlossenAm == null)))
            .ToListAsync();

    public async Task<IActionResult> OnPostAsync(string? name, string? beschreibung)
    {
        var fehler = await GruppeRegeln.NamePruefenAsync(db, name, ausser: null);
        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage();
        }

        var gruppe = new Gruppe { Name = name!.Trim(), Beschreibung = GruppeRegeln.Bereinigen(beschreibung) };
        db.Gruppen.Add(gruppe);
        await db.SaveChangesAsync();

        TempData["Meldung"] = $"Gruppe {gruppe.Name} wurde angelegt.";
        return RedirectToPage("GruppeBearbeiten", new { id = gruppe.Id });
    }
}

internal static class GruppeRegeln
{
    public static string? Bereinigen(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static async Task<string?> NamePruefenAsync(AvisDb db, string? name, int? ausser)
    {
        var bereinigt = name?.Trim() ?? "";
        if (bereinigt.Length == 0)
            return "Bitte einen Namen angeben.";
        if (bereinigt.Length > 100)
            return "Der Name darf höchstens 100 Zeichen lang sein.";
        // Der Vergleich läuft in der Datenbank; ToLower() wird dort zu lower().
#pragma warning disable CA1304, CA1311, CA1862
        if (await db.Gruppen.AnyAsync(g => g.Id != ausser && g.Name.ToLower() == bereinigt.ToLower()))
#pragma warning restore CA1304, CA1311, CA1862
            return $"Die Gruppe {bereinigt} gibt es schon.";
        return null;
    }
}
