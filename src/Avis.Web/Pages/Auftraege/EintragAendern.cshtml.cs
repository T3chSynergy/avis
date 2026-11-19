using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

/// <summary>
/// Der Verfasser ändert den Text seines internen Kommentars oder seines
/// Hinweises beim Weiterleiten. Die Regeln stehen in <see cref="Auftrag.EintragAendern"/>.
/// </summary>
public class EintragAendernModel(AvisDb db, TimeProvider uhr) : PageModel
{
    public int Id { get; private set; }
    public Verlaufseintrag Eintrag { get; private set; } = null!;

    [BindProperty]
    public string? Text { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int eintrag)
    {
        var (auftrag, gefunden) = await LadenAsync(id, eintrag);
        if (auftrag is null || gefunden is null)
            return NotFound();
        if (!auftrag.DarfTextAendern(gefunden, User.BenutzerId()))
        {
            TempData["Fehler"] = Grund(auftrag, gefunden);
            return RedirectToPage("Details", new { id });
        }

        Id = id;
        Eintrag = gefunden;
        Text = gefunden.Text;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, int eintrag)
    {
        var (auftrag, gefunden) = await LadenAsync(id, eintrag);
        if (auftrag is null || gefunden is null)
            return NotFound();

        try
        {
            var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
            var geaendert = auftrag.EintragAendern(gefunden, ich, Text, uhr.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            TempData["Meldung"] = geaendert ? "Der Eintrag wurde geändert." : "Am Eintrag hat sich nichts geändert.";
            return RedirectToPage("Details", new { id });
        }
        catch (Exception ex) when (ex is AuftragException or DbUpdateConcurrencyException)
        {
            // Der geschriebene Text bleibt im Formular stehen.
            ModelState.AddModelError(nameof(Text), ex is AuftragException
                ? ex.Message
                : "Der Auftrag wurde in diesem Augenblick geändert. Bitte noch einmal speichern.");
            Id = id;
            Eintrag = gefunden;
            return Page();
        }
    }

    /// <summary>Warum sich der Eintrag gerade nicht ändern lässt, in der Reihenfolge, in der man es beheben würde.</summary>
    private string Grund(Auftrag auftrag, Verlaufseintrag eintrag) =>
        !Verlaufseintrag.TextAenderbar(eintrag.Art) ? "Dieser Eintrag lässt sich nicht ändern. Das geht nur bei internen Kommentaren und beim Hinweis zum Weiterleiten."
        : eintrag.BenutzerId != User.BenutzerId() ? "Einen Eintrag kann nur ändern, wer ihn geschrieben hat."
        : Auftrag.NichtUebernommen;

    private async Task<(Auftrag?, Verlaufseintrag?)> LadenAsync(int id, int eintrag)
    {
        var auftrag = await db.AuftraegeFuer(User.BenutzerId())
            .Include(a => a.Verlauf.Where(v => v.Id == eintrag)).ThenInclude(v => v.Anhaenge)
            .FirstOrDefaultAsync(a => a.Id == id);
        return (auftrag, auftrag?.Verlauf.FirstOrDefault(v => v.Id == eintrag));
    }
}
