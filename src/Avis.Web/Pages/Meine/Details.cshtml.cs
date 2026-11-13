using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Meine;

/// <summary>
/// Ein eigener Auftrag aus Sicht des Kunden. Zu sehen ist nur, was für
/// Kunden bestimmt ist: keine internen Kommentare, keine Weiterleitungen,
/// keine Gruppen.
/// </summary>
public class DetailsModel(AvisDb db, Kundenzuordnung zuordnung, TimeProvider uhr, Anhangablage ablage) : PageModel
{
    public Auftrag Auftrag { get; private set; } = null!;
    public IReadOnlyList<Verlaufseintrag> Verlauf { get; private set; } = [];

    public static string Titel(VerlaufArt art) => art switch
    {
        VerlaufArt.Erfasst => "Auftrag eingegangen",
        VerlaufArt.NachrichtAnKunden => "Nachricht der IT",
        VerlaufArt.AntwortDesKunden => "Antwort",
        VerlaufArt.Abgeschlossen => "Abgeschlossen",
        VerlaufArt.WiederGeoeffnet => "Wieder geöffnet",
        _ => "",
    };

    /// <summary>Der Grund einer Wiedereröffnung ist für die IT geschrieben und bleibt intern.</summary>
    public static bool ZeigtText(VerlaufArt art) =>
        art is VerlaufArt.NachrichtAnKunden or VerlaufArt.AntwortDesKunden or VerlaufArt.Abgeschlossen;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());

        // Fremde Aufträge gibt es aus Sicht des Kunden nicht.
        var auftrag = await db.Auftraege
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Bearbeiter)
            .Include(a => a.Standort)
            .Include(a => a.Verlauf).ThenInclude(v => v.Benutzer)
            .Include(a => a.Verlauf).ThenInclude(v => v.Anhaenge)
            .FirstOrDefaultAsync(a => a.Id == id && a.KundeId == kunde.Id);
        if (auftrag is null)
            return NotFound();

        Auftrag = auftrag;
        Verlauf = auftrag.Verlauf
            .Where(v => v.FuerKundenSichtbar)
            .OrderBy(v => v.Zeitpunkt).ThenBy(v => v.Id)
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string? text, List<IFormFile> dateien)
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());
        var auftrag = await db.Auftraege.FirstOrDefaultAsync(a => a.Id == id && a.KundeId == kunde.Id);
        if (auftrag is null)
            return NotFound();

        var anhaenge = new List<Anhang>();
        try
        {
            anhaenge = await ablage.SpeichernAsync(dateien);
            var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
            auftrag.KundeAntwortet(ich, text, uhr.GetUtcNow().UtcDateTime, anhaenge);
            await db.SaveChangesAsync();
            TempData["Meldung"] = "Die Nachricht wurde an die IT gesendet.";
        }
        catch (AuftragException ex)
        {
            ablage.Verwerfen(anhaenge);
            TempData["Fehler"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }
}
