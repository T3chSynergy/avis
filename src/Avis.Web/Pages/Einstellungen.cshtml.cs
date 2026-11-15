using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages;

/// <summary>
/// Die persönlichen Einstellungen eines Benutzers, für Bearbeiter wie für
/// Kunden. Sie hängen am Konto und gelten deshalb auf jedem Gerät. Bisher
/// gibt es eine: die Darstellung.
/// </summary>
public class EinstellungenModel(AvisDb db) : PageModel
{
    public Darstellung Darstellung { get; private set; }

    public static string Text(Darstellung darstellung) => darstellung switch
    {
        Darstellung.Hell => "Hell",
        Darstellung.Dunkel => "Dunkel",
        _ => "Wie das Gerät",
    };

    public async Task OnGetAsync() =>
        Darstellung = await db.Benutzer.Where(b => b.Id == User.BenutzerId()).Select(b => b.Darstellung).FirstAsync();

    public async Task<IActionResult> OnPostAsync(Darstellung darstellung)
    {
        // Ein Wert, den es nicht gibt, kommt hier als Voreinstellung an und stünde nur im ModelState als Fehler.
        if (!ModelState.IsValid || !Enum.IsDefined(darstellung))
            return BadRequest();

        var benutzer = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
        benutzer.Darstellung = darstellung;
        await db.SaveChangesAsync();

        TempData["Meldung"] = "Die Einstellungen wurden gespeichert.";
        return RedirectToPage();
    }
}
