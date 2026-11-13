using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Pages.Kunden;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

public class KundeAendernModel(AvisDb db, TimeProvider uhr) : PageModel
{
    public int Id { get; private set; }
    public int Version { get; private set; }
    public string Bisher { get; private set; } = "";
    public Kundenauswahl Kundenauswahl { get; private set; } = null!;

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

        return await SeiteAsync(auftrag, auftrag.Version, suche: null, kundeId: null);
    }

    public async Task<IActionResult> OnPostSuchenAsync(int id, int version, string? suche, int? kundeId)
    {
        var auftrag = await LadenAsync(id);
        return auftrag is null ? NotFound() : await SeiteAsync(auftrag, version, suche, kundeId);
    }

    public async Task<IActionResult> OnPostAsync(int id, int version, string? suche, int? kundeId)
    {
        var auftrag = await LadenAsync(id);
        if (auftrag is null)
            return NotFound();

        // Eingabetaste im Suchfeld ohne Skript: Das ist eine Suche.
        if (kundeId is null && !string.IsNullOrWhiteSpace(suche))
            return await SeiteAsync(auftrag, version, suche, kundeId);

        try
        {
            if (auftrag.Version != version)
                throw new AuftragException("Der Auftrag wurde inzwischen von jemand anderem geändert. Bitte prüfe den aktuellen Stand.");

            var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Id == kundeId)
                ?? throw new AuftragException("Bitte einen Kunden auswählen.");
            var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());

            auftrag.KundeAendern(kunde, ich, uhr.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            TempData["Meldung"] = $"Der Auftrag gehört jetzt zu {kunde.Name}.";
        }
        catch (AuftragException ex)
        {
            TempData["Fehler"] = ex.Message;
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Fehler"] = "Der Auftrag wurde inzwischen von jemand anderem geändert. Bitte prüfe den aktuellen Stand.";
        }

        return RedirectToPage("Details", new { id });
    }

    private async Task<IActionResult> SeiteAsync(Auftrag auftrag, int version, string? suche, int? kundeId)
    {
        Id = auftrag.Id;
        Version = version;
        Bisher = auftrag.Kunde.Name;
        Kundenauswahl = await Kundenauswahl.LadenAsync(db, suche, kundeId, neuErlaubt: false);
        return Page();
    }

    private Task<Auftrag?> LadenAsync(int id) =>
        db.Auftraege.Include(a => a.Kunde).FirstOrDefaultAsync(a => a.Id == id);
}
