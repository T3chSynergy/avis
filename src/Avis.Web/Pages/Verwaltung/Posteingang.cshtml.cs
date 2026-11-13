using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Posteingang;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Pages.Verwaltung;

public class PosteingangModel(AvisDb db, IOptions<PosteingangOptionen> optionen) : PageModel
{
    public const int Hoechstzahl = 100;

    public string Art => optionen.Value.Art;
    public IReadOnlyList<Maileingang> Mails { get; private set; } = [];

    public static string Text(MaileingangErgebnis ergebnis) => ergebnis switch
    {
        MaileingangErgebnis.NeuerAuftrag => "neuer Auftrag",
        MaileingangErgebnis.Antwort => "Antwort des Kunden",
        MaileingangErgebnis.WiederGeoeffnet => "Auftrag wieder geöffnet",
        MaileingangErgebnis.InternerKommentar => "interner Kommentar",
        MaileingangErgebnis.Uebergangen => "übergangen",
        _ => ergebnis.ToString(),
    };

    public async Task OnGetAsync() =>
        Mails = await db.Maileingaenge.AsNoTracking().OrderByDescending(m => m.Id).Take(Hoechstzahl).ToListAsync();
}
