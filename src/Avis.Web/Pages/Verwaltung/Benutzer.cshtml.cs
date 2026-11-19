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

/// <summary>
/// Die Liste der Benutzer. Jeder, der sich anmeldet, bekommt ein Konto, auch
/// jeder Kunde, der das Webinterface benutzt. Die Liste zeigt deshalb in der
/// Voreinstellung nur, wer eine Rolle hat, und lässt sich durchsuchen.
/// </summary>
public class BenutzerModel(AvisDb db) : PageModel
{
    public const int Hoechstzahl = 100;

    public const string MitRolle = "rolle";
    public const string NurAdministratoren = "administratoren";
    public const string OhneRolle = "ohne";
    public const string Alle = "alle";

    public record Zeile(int Id, string Name, string Anmeldename, Rolle Rolle, bool Aktiv, string Gruppen, bool AusQuelle);

    /// <summary>Wie viele Konten auf die Suche passen, je Auswahl.</summary>
    public record Zahlen(int MitRolle, int Administratoren, int OhneRolle, int Alle);

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>Welche Konten gezeigt werden. Ohne Angabe die mit Rolle.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Zeigen { get; set; }

    public string Auswahl { get; private set; } = MitRolle;
    public IReadOnlyList<Zeile> Benutzer { get; private set; } = [];
    public Zahlen Anzahl { get; private set; } = new(0, 0, 0, 0);
    public bool Gekuerzt { get; private set; }

    public async Task OnGetAsync()
    {
        Auswahl = Zeigen is NurAdministratoren or OhneRolle or Alle ? Zeigen : MitRolle;

        // Die Konten, die AVIS sich selbst anlegt, etwa für den E-Mail-Eingang, werden nicht gepflegt.
        var gesucht = db.Benutzer.AsNoTracking()
            .Where(b => !b.Anmeldename.StartsWith(Domain.Benutzer.Systemkonten))
            .Suchen(Q);

        var rollen = await gesucht
            .GroupBy(b => new { b.IstBearbeiter, b.IstAdministrator })
            .Select(g => new { g.Key.IstBearbeiter, g.Key.IstAdministrator, Zahl = g.Count() })
            .ToListAsync();
        Anzahl = new(
            rollen.Where(r => r.IstBearbeiter || r.IstAdministrator).Sum(r => r.Zahl),
            rollen.Where(r => r.IstAdministrator).Sum(r => r.Zahl),
            rollen.Where(r => !r.IstBearbeiter && !r.IstAdministrator).Sum(r => r.Zahl),
            rollen.Sum(r => r.Zahl));

        var gewaehlt = Auswahl switch
        {
            NurAdministratoren => gesucht.Where(b => b.IstAdministrator),
            OhneRolle => gesucht.Where(b => !b.IstBearbeiter && !b.IstAdministrator),
            Alle => gesucht,
            _ => gesucht.Where(b => b.IstBearbeiter || b.IstAdministrator),
        };

        var benutzer = await gewaehlt
            .Include(b => b.Gruppen)
            .OrderByDescending(b => b.Aktiv).ThenBy(b => b.Name).ThenBy(b => b.Id)
            .Take(Hoechstzahl + 1)
            .ToListAsync();

        Gekuerzt = benutzer.Count > Hoechstzahl;
        Benutzer = benutzer
            .Take(Hoechstzahl)
            .Select(b => new Zeile(
                b.Id,
                b.Name,
                b.Anmeldename,
                b.Rolle(),
                b.Aktiv,
                string.Join(", ", b.Gruppen.OrderBy(g => g.Name).Select(g => g.Name)),
                b.AusQuelle))
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
