using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

public class BearbeitenModel(AvisDb db, TimeProvider uhr) : PageModel
{
    public int Id { get; private set; }

    [BindProperty]
    public AuftragFelder Felder { get; set; } = new();

    /// <summary>Der Stand des Auftrags, auf dem das Formular beruht.</summary>
    [BindProperty]
    public int Version { get; set; }

    public AuftragAuswahl Auswahl { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var auftrag = await LadenAsync(id);
        if (auftrag is null)
            return NotFound();
        if (auftrag.Status == AuftragStatus.Abgeschlossen)
        {
            TempData["Fehler"] = "Ein abgeschlossener Auftrag lässt sich nicht bearbeiten. Öffne ihn zuerst wieder.";
            return RedirectToPage("Details", new { id });
        }
        if (auftrag.BearbeiterId != User.BenutzerId())
        {
            TempData["Fehler"] = Auftrag.NichtUebernommen;
            return RedirectToPage("Details", new { id });
        }

        Id = id;
        Felder = AuftragFelder.Aus(auftrag);
        Version = auftrag.Version;
        Auswahl = await AuftragAuswahl.LadenAsync(db, auftrag);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var auftrag = await LadenAsync(id);
        if (auftrag is null)
            return NotFound();

        var angaben = await Felder.AlsAngabenAsync(db, ModelState, nameof(Felder), auftrag);

        // Hat inzwischen jemand anderes den Auftrag übernommen, bleiben die
        // Eingaben im Formular stehen, gespeichert wird nichts.
        if (angaben is not null && auftrag.BearbeiterId != User.BenutzerId())
        {
            ModelState.AddModelError("", Auftrag.NichtUebernommen);
            ModelState.Remove(nameof(Version));
            Version = auftrag.Version;
        }

        // Hat jemand den Auftrag inzwischen geändert, wird nichts
        // überschrieben. Die Eingaben bleiben im Formular stehen.
        else if (angaben is not null && auftrag.Version != Version)
        {
            ModelState.AddModelError("",
                "Der Auftrag wurde inzwischen geändert. Deine Eingaben stehen noch im Formular. "
                + "Vergleiche sie mit dem aktuellen Stand, bevor du erneut speicherst.");
            ModelState.Remove(nameof(Version));
            Version = auftrag.Version;
        }

        if (angaben is not null && ModelState.IsValid)
        {
            try
            {
                var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
                var geaendert = auftrag.Aendern(angaben, ich, uhr.GetUtcNow().UtcDateTime);
                await db.SaveChangesAsync();

                TempData["Meldung"] = geaendert ? "Die Änderungen wurden gespeichert." : "Es wurde nichts geändert.";
                return RedirectToPage("Details", new { id });
            }
            catch (AuftragException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Fehler"] = "Der Auftrag wurde inzwischen geändert. Bitte prüfe den aktuellen Stand.";
                return RedirectToPage("Details", new { id });
            }
        }

        Id = id;
        Auswahl = await AuftragAuswahl.LadenAsync(db, auftrag);
        return Page();
    }

    private Task<Auftrag?> LadenAsync(int id) =>
        db.AuftraegeFuer(User.BenutzerId())
            .Include(a => a.Kategorie)
            .Include(a => a.Standort)
            .Include(a => a.Inventargeraet)
            .FirstOrDefaultAsync(a => a.Id == id);
}
