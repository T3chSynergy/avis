using System.ComponentModel.DataAnnotations;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Kunden;

public class IndexModel(AvisDb db) : PageModel
{
    public const int Hoechstzahl = 50;

    public record Zeile(int Id, string Name, string? Email, string? Telefon, string? Organisationseinheit, string? Standort, int Laufend);

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public IReadOnlyList<Zeile> Kunden { get; private set; } = [];
    public bool Gekuerzt { get; private set; }

    public async Task OnGetAsync()
    {
        var kunden = await db.Kunden.AsNoTracking()
            .Suchen(Q)
            .OrderBy(k => k.Name)
            .Take(Hoechstzahl + 1)
            .Select(k => new Zeile(
                k.Id,
                k.Name,
                k.Email,
                k.Telefon,
                k.Organisationseinheit,
                k.Standort!.Name,
                db.Auftraege.Count(a => a.KundeId == k.Id && a.AbgeschlossenAm == null)))
            .ToListAsync();

        Gekuerzt = kunden.Count > Hoechstzahl;
        Kunden = kunden.Take(Hoechstzahl).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? name, string? email, string? telefon)
    {
        var kunde = new Kunde();
        var fehler = await KundeRegeln.UebernehmenAsync(db, kunde, name, email, telefon, organisationseinheit: null, standortId: null);
        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage();
        }

        db.Kunden.Add(kunde);
        await db.SaveChangesAsync();
        TempData["Meldung"] = $"{kunde.Name} wurde angelegt.";
        return RedirectToPage("Details", new { id = kunde.Id });
    }
}

internal static class KundeRegeln
{
    /// <summary>
    /// Prüft die Angaben und überträgt sie auf den Kunden. Gibt bei einem
    /// Fehler die Meldung zurück und ändert dann nichts.
    /// </summary>
    public static async Task<string?> UebernehmenAsync(
        AvisDb db, Kunde kunde, string? name, string? email, string? telefon, string? organisationseinheit, int? standortId)
    {
        var bereinigterName = name?.Trim() ?? "";
        var adresse = Bereinigen(email)?.ToLowerInvariant();
        var nummer = Bereinigen(telefon);
        var einheit = Bereinigen(organisationseinheit);

        if (bereinigterName.Length is 0 or > 200)
            return "Bitte einen Namen mit höchstens 200 Zeichen angeben.";
        if (adresse is not null && (adresse.Length > 320 || !new EmailAddressAttribute().IsValid(adresse)))
            return "Das ist keine gültige E-Mail-Adresse.";
        if (nummer?.Length > 50)
            return "Die Telefonnummer darf höchstens 50 Zeichen lang sein.";
        if (einheit?.Length > 200)
            return "Die Organisationseinheit darf höchstens 200 Zeichen lang sein.";

        // Über die E-Mail-Adresse werden Kunden wiedererkannt, sie muss eindeutig sein.
        if (adresse is not null)
        {
            var anderer = await db.Kunden.FirstOrDefaultAsync(k => k.Email == adresse && k.Id != kunde.Id);
            if (anderer is not null)
                return $"Die E-Mail-Adresse gehört schon zum Kunden {anderer.Name}.";
        }

        if (standortId is not null && standortId != kunde.StandortId
            && !await db.Standorte.AnyAsync(s => s.Id == standortId && s.Aktiv))
            return "Dieser Standort steht nicht zur Auswahl.";

        kunde.Name = bereinigterName;
        kunde.Email = adresse;
        kunde.Telefon = nummer;
        kunde.Organisationseinheit = einheit;
        kunde.StandortId = standortId;
        return null;
    }

    private static string? Bereinigen(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
