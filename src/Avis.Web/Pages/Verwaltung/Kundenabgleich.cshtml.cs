using Avis.Web.Kundenabgleich;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Avis.Web.Pages.Verwaltung;

/// <summary>Zeigt, wie der Kundenabgleich eingestellt ist und was der letzte Lauf getan hat, und stößt einen Lauf an.</summary>
public class KundenabgleichModel(
    Avis.Web.Kundenabgleich.Kundenabgleich abgleich,
    Abgleichstand stand,
    IOptions<KundenabgleichOptionen> optionen) : PageModel
{
    public KundenabgleichOptionen Optionen => optionen.Value;
    public bool Eingerichtet => abgleich.Eingerichtet;
    public Abgleichergebnis? Letztes => stand.Letztes;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!abgleich.Eingerichtet)
            return NotFound();

        var ergebnis = await abgleich.AusfuehrenAsync(HttpContext.RequestAborted);
        if (ergebnis.Fehler is null)
            TempData["Meldung"] = "Die Kunden wurden abgeglichen.";
        else
            TempData["Fehler"] = ergebnis.Fehler;
        return RedirectToPage();
    }
}
