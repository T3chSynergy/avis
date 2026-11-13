using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Anhaenge;

/// <summary>
/// Liefert einen Anhang zum Herunterladen. Ein Bearbeiter bekommt ihn, wenn er
/// den Auftrag sehen darf. Ein Kunde bekommt ihn, wenn es sein Auftrag ist und
/// der Verlaufseintrag für Kunden sichtbar ist.
/// </summary>
public class IndexModel(AvisDb db, Anhangablage ablage, Kundenzuordnung zuordnung, ILogger<IndexModel> log) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id)
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

        // Immer als Download, nie zur Anzeige im Browser: Eine hochgeladene
        // Datei soll im Namen von AVIS nichts ausführen können.
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(pfad, "application/octet-stream", anhang.Dateiname);
    }
}
