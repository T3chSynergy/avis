using Avis.Web.Abgleich;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Avis.Web.Pages.Verwaltung;

/// <summary>Zeigt, wie der Geräteabgleich eingestellt ist und was der letzte Lauf getan hat, und stößt einen Lauf an.</summary>
public class GeraeteabgleichModel(
    Geraeteabgleich abgleich,
    Abgleichprotokoll protokoll,
    IOptions<GeraeteabgleichOptionen> optionen) : PageModel
{
    public GeraeteabgleichOptionen Optionen => optionen.Value;
    public bool Eingerichtet => abgleich.Eingerichtet;
    public Abgleichergebnis? Letztes { get; private set; }

    public async Task OnGetAsync() =>
        Letztes = await protokoll.LetztesAsync(Abgleichlauf.Geraete, HttpContext.RequestAborted);

    public async Task<IActionResult> OnPostAsync()
    {
        if (!abgleich.Eingerichtet)
            return NotFound();

        var ergebnis = await abgleich.AusfuehrenAsync(HttpContext.RequestAborted);
        if (ergebnis.Fehler is null)
            TempData["Meldung"] = "Die Geräte wurden abgeglichen.";
        else
            TempData["Fehler"] = ergebnis.Fehler;
        return RedirectToPage();
    }
}
