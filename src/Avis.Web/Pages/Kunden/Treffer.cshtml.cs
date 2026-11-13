using Avis.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Avis.Web.Pages.Kunden;

/// <summary>Liefert nur die Trefferliste, für die Suche während des Tippens.</summary>
public class TrefferModel(AvisDb db) : PageModel
{
    public Kundenauswahl Auswahl { get; private set; } = null!;

    public async Task OnGetAsync(string? q, int? gewaehlt, bool neu) =>
        Auswahl = await Kundenauswahl.LadenAsync(db, q, gewaehlt, neu);
}
