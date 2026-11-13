using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Verwaltung;

/// <summary>Die Rolle eines Benutzers. Administratoren sind immer auch Bearbeiter.</summary>
public enum Rolle
{
    Keine = 0,
    Bearbeiter = 1,
    Administrator = 2,
}

public static class RolleExtensions
{
    public static Rolle Rolle(this Benutzer benutzer) =>
        benutzer.IstAdministrator ? Verwaltung.Rolle.Administrator
        : benutzer.IstBearbeiter ? Verwaltung.Rolle.Bearbeiter
        : Verwaltung.Rolle.Keine;

    public static void RolleSetzen(this Benutzer benutzer, Rolle rolle)
    {
        benutzer.IstAdministrator = rolle == Verwaltung.Rolle.Administrator;
        benutzer.IstBearbeiter = rolle is Verwaltung.Rolle.Bearbeiter or Verwaltung.Rolle.Administrator;
    }

    public static string Text(this Rolle rolle) => rolle switch
    {
        Verwaltung.Rolle.Administrator => "Administrator",
        Verwaltung.Rolle.Bearbeiter => "Bearbeiter",
        _ => "keine",
    };
}

public class BenutzerModel(AvisDb db) : PageModel
{
    public record Zeile(int Id, string Name, string Anmeldename, Rolle Rolle, bool Aktiv, string Gruppen);

    public IReadOnlyList<Zeile> Benutzer { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var benutzer = await db.Benutzer.AsNoTracking()
            .Include(b => b.Gruppen)
            .OrderByDescending(b => b.Aktiv).ThenBy(b => b.Name)
            .ToListAsync();

        Benutzer = benutzer
            .Select(b => new Zeile(
                b.Id,
                b.Name,
                b.Anmeldename,
                b.Rolle(),
                b.Aktiv,
                string.Join(", ", b.Gruppen.OrderBy(g => g.Name).Select(g => g.Name))))
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? anmeldename, string? name, Rolle rolle)
    {
        var bereinigt = anmeldename?.Trim().ToLowerInvariant() ?? "";
        var anzeigename = name?.Trim() ?? "";

        string? fehler = null;
        if (bereinigt.Length == 0 || anzeigename.Length == 0)
            fehler = "Bitte Anmeldename und Name angeben.";
        else if (bereinigt.Length > 200 || anzeigename.Length > 200)
            fehler = "Anmeldename und Name dürfen höchstens 200 Zeichen lang sein.";
        else if (await db.Benutzer.AnyAsync(b => b.Anmeldename == bereinigt))
            fehler = $"Den Anmeldenamen {bereinigt} gibt es schon.";

        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage();
        }

        var benutzer = new Benutzer { Anmeldename = bereinigt, Name = anzeigename };
        benutzer.RolleSetzen(rolle);
        db.Benutzer.Add(benutzer);
        await db.SaveChangesAsync();

        TempData["Meldung"] = $"{benutzer.Name} wurde angelegt.";
        return RedirectToPage();
    }
}
