using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Anhaenge;

/// <summary>
/// Liefert einen Anhang zum Herunterladen, ein Bild auf Wunsch auch zur Anzeige. Ein Bearbeiter bekommt ihn, wenn er
/// den Auftrag sehen darf. Ein Kunde bekommt ihn, wenn es sein Auftrag ist und
/// der Verlaufseintrag für Kunden sichtbar ist.
/// </summary>
public class IndexModel(AvisDb db, Anhangablage ablage, Kundenzuordnung zuordnung, ILogger<IndexModel> log) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, bool bild)
    {
        var anhang = await db.Anhaenge.AsNoTracking()
            .Include(a => a.Verlaufseintrag)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (anhang is null)
            return NotFound();

        var auftragId = anhang.Verlaufseintrag.AuftragId;
        var ich = User.BenutzerId();

        var erlaubt = User.IstBearbeiter() && await db.AuftraegeFuer(ich).AnyAsync(a => a.Id == auftragId);
        if (!erlaubt && anhang.Verlaufseintrag.FuerKundenSichtbar)
        {
            var kunde = await zuordnung.FuerBenutzerAsync(ich);
            erlaubt = await db.Auftraege.AnyAsync(a => a.Id == auftragId && a.KundeId == kunde.Id);
        }
        if (!erlaubt)
            return NotFound();

        var pfad = ablage.Pfad(anhang);
        if (!System.IO.File.Exists(pfad))
        {
            log.LogError("Die Datei zum Anhang {Anhang} fehlt in der Ablage: {Pfad}", id, pfad);
            return NotFound();
        }

        // Eine hochgeladene Datei soll im Namen von AVIS nichts ausführen
        // können. Der Browser darf den Typ nicht selbst erraten, und die
        // Antwort darf weder Skripte noch anderes nachladen.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";

        // Zur Anzeige nur, was am Inhalt als Bild erkannt wurde, mit genau diesem Typ.
        if (bild && anhang.Bildtyp is not null)
        {
            Response.Headers.CacheControl = "private, max-age=3600";
            return PhysicalFile(pfad, anhang.Bildtyp);
        }

        // Alles andere immer als Download, nie zur Anzeige im Browser.
        return PhysicalFile(pfad, "application/octet-stream", anhang.Dateiname);
    }
}
