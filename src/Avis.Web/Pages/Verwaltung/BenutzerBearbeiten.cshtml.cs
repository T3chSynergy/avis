using System.ComponentModel.DataAnnotations;
using Avis.Web.Abgleich;
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
            .FirstOrDefaultAsync(b => b.Id == id && !b.Anmeldename.StartsWith(Benutzer.Systemkonten));
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
            .FirstOrDefaultAsync(b => b.Id == id && !b.Anmeldename.StartsWith(Benutzer.Systemkonten));
        if (benutzer is null)
            return NotFound();

        var anzeigename = name?.Trim() ?? "";
        var adresse = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

        // Bei einem Konto aus einer externen Quelle sind Name und E-Mail gesperrt und kommen nicht mit.
        string? fehler = null;
        if (benutzer.AusQuelle)
            fehler = null;
        else if (anzeigename.Length is 0 or > 200)
            fehler = "Bitte einen Namen mit höchstens 200 Zeichen angeben.";
        else if (adresse is not null && (adresse.Length > 320 || !new EmailAddressAttribute().IsValid(adresse)))
            fehler = "Das ist keine gültige E-Mail-Adresse.";

        if (fehler is not null)
        {
            TempData["Fehler"] = fehler;
            return RedirectToPage(new { id });
        }

        // Was aus einer externen Quelle stammt, wird dort gepflegt: Name, E-Mail und ob jemand Bearbeiter ist.
        if (!benutzer.AusQuelle)
        {
            benutzer.Name = anzeigename;
            benutzer.Email = adresse;
        }

        // Am eigenen Konto ändert niemand Rolle oder Status. So bleibt immer
        // mindestens ein Administrator übrig.
        var zurueck = 0;
        if (id != User.BenutzerId())
        {
            if (!benutzer.AusQuelle)
                benutzer.RolleSetzen(rolle);
            else if (rolle == Rolle.Administrator)
                benutzer.RolleSetzen(Rolle.Administrator);
            else
                benutzer.IstAdministrator = false;
            benutzer.Aktiv = aktiv;

            if (!benutzer.Aktiv || !benutzer.IstBearbeiter)
            {
                var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
                zurueck = await db.AusDerBearbeitungNehmenAsync(benutzer, ich, uhr.GetUtcNow().UtcDateTime, $"{benutzer.Name} ist kein Bearbeiter mehr.");
            }
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
}
