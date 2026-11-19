using Avis.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Avis.Web.Pages.Kunden;

/// <summary>Liefert nur die Trefferliste, für die Suche während des Tippens.</summary>
public class TrefferModel(AvisDb db, Abgleich.Kundenabgleich abgleich) : PageModel
{
    public Kundenauswahl Auswahl { get; private set; } = null!;

    public async Task OnGetAsync(string? q, int? gewaehlt, string? leer, bool neu)
    {
        // "Neuen Kunden anlegen" gibt es nur, wo es erlaubt ist, gleich was die Anfrage verlangt.
        var neuErlaubt = abgleich.VonHandAnlegen;
        Auswahl = await Kundenauswahl.LadenAsync(db, q, gewaehlt, leer == "neu" && !neuErlaubt ? null : leer, neu && neuErlaubt);
    }
}
