using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Verwaltung;

/// <summary>Was die gemeinsame Ansicht der Auswahllisten braucht.</summary>
public interface IStammliste
{
    string Titel { get; }
    string Einzahl { get; }
    string Erklaerung { get; }
    IReadOnlyList<IStammwert> Eintraege { get; }
}

/// <summary>
/// Pflege einer Auswahlliste: anlegen, umbenennen, aktivieren und
/// deaktivieren. Gelöscht wird nichts, weil bestehende Aufträge auf die
/// Einträge verweisen.
/// </summary>
public abstract class StammlisteModel<T>(AvisDb db) : PageModel, IStammliste
    where T : class, IStammwert, new()
{
    public const int Hoechstlaenge = 100;

    public abstract string Titel { get; }
    public abstract string Einzahl { get; }
    public abstract string Erklaerung { get; }
    public IReadOnlyList<IStammwert> Eintraege { get; private set; } = [];

    public async Task OnGetAsync() =>
        Eintraege = await db.Set<T>().AsNoTracking().OrderBy(e => e.Name).ToListAsync();

    public async Task<IActionResult> OnPostAnlegenAsync(string? name)
    {
        if (await NamePruefenAsync(name, ausser: null) is { } bereinigt)
        {
            db.Set<T>().Add(new T { Name = bereinigt });
            await db.SaveChangesAsync();
            TempData["Meldung"] = $"{Einzahl} {bereinigt} wurde angelegt.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSpeichernAsync(int id, string? name, bool aktiv)
    {
        var eintrag = await db.Set<T>().FirstOrDefaultAsync(e => e.Id == id);
        if (eintrag is null)
            return NotFound();

        if (await NamePruefenAsync(name, ausser: id) is { } bereinigt)
        {
            eintrag.Name = bereinigt;
            eintrag.Aktiv = aktiv;
            await db.SaveChangesAsync();
            TempData["Meldung"] = $"{Einzahl} {bereinigt} wurde gespeichert.";
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
        else if (await db.Set<T>().AnyAsync(e => e.Id != ausser && e.Name.ToLower() == bereinigt.ToLower()))
            TempData["Fehler"] = $"{Einzahl} {bereinigt} gibt es schon.";
        else
            return bereinigt;

        return null;
    }
}

public class KategorienModel(AvisDb db) : StammlisteModel<Kategorie>(db)
{
    public override string Titel => "Kategorien";
    public override string Einzahl => "Kategorie";
    public override string Erklaerung => "Kategorien ordnen Aufträge nach ihrer Art.";
}

public class StandorteModel(AvisDb db) : StammlisteModel<Standort>(db)
{
    public override string Titel => "Standorte";
    public override string Einzahl => "Standort";
    public override string Erklaerung => "Standorte stehen an Aufträgen und Kunden zur Auswahl.";
}
