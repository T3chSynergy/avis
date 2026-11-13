using System.ComponentModel.DataAnnotations;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Verwaltung;

public class BenutzerBearbeitenModel(AvisDb db, TimeProvider uhr) : PageModel
{
    public Benutzer Benutzer { get; private set; } = null!;
    public int InBearbeitung { get; private set; }
    public bool IstEigenesKonto { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var benutzer = await db.Benutzer.AsNoTracking().Include(b => b.Gruppen)
            .FirstOrDefaultAsync(b => b.Id == id && !b.Anmeldename.StartsWith("avis:"));
        if (benutzer is null)
            return NotFound();

        Benutzer = benutzer;
        InBearbeitung = await db.Auftraege.InBearbeitung().CountAsync(a => a.BearbeiterId == id);
        IstEigenesKonto = id == User.BenutzerId();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string? name, string? email, Rolle rolle, bool aktiv)
    {
        var benutzer = await db.Benutzer.Include(b => b.Gruppen)
            .FirstOrDefaultAsync(b => b.Id == id && !b.Anmeldename.StartsWith("avis:"));
        if (benutzer is null)
            return NotFound();

        var anzeigename = name?.Trim() ?? "";
        var adresse = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

        string? fehler = null;
        if (anzeigename.Length is 0 or > 200)
            fehler = "Bitte einen Namen mit höchstens 200 Zeichen angeben.";
        else if (adresse is not null && (adresse.Length > 320 || !new EmailAddressAttribute().IsValid(adresse)))
            fehler = "Das ist keine gültige E-Mail-Adresse.";

        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage(new { id });
        }

        benutzer.Name = anzeigename;
        benutzer.Email = adresse;

        // Am eigenen Konto ändert niemand Rolle oder Status. So bleibt immer
        // mindestens ein Administrator übrig.
        var zurueck = 0;
        if (id != User.BenutzerId())
        {
            benutzer.RolleSetzen(rolle);
            benutzer.Aktiv = aktiv;

            if (!benutzer.Aktiv || !benutzer.IstBearbeiter)
                zurueck = await AusDerBearbeitungNehmenAsync(benutzer);
        }

        await db.SaveChangesAsync();

        TempData["Meldung"] = $"{benutzer.Name} wurde gespeichert." + zurueck switch
        {
            0 => "",
            1 => " 1 Auftrag liegt wieder offen in seiner Gruppe.",
            _ => $" {zurueck} Aufträge liegen wieder offen in ihren Gruppen.",
        };
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Gibt die Aufträge des Benutzers an ihre Gruppen zurück und trägt ihn
    /// aus allen Gruppen aus.
    /// </summary>
    private async Task<int> AusDerBearbeitungNehmenAsync(Benutzer benutzer)
    {
        var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
        var jetzt = uhr.GetUtcNow().UtcDateTime;

        var auftraege = await db.Auftraege
            .InBearbeitung()
            .Where(a => a.BearbeiterId == benutzer.Id)
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .ToListAsync();

        foreach (var auftrag in auftraege)
            auftrag.ZurueckAnGruppe(ich, jetzt, $"{benutzer.Name} ist kein Bearbeiter mehr.");

        benutzer.Gruppen.Clear();
        return auftraege.Count;
    }
}
