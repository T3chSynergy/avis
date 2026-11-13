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

    /// <summary>Nach einem Fehler: der geschriebene Text, damit er nicht verloren geht.</summary>
    public string? Entwurf { get; private set; }

    public static string Titel(VerlaufArt art) => art switch
    {
        VerlaufArt.Erfasst => "Auftrag eingegangen",
        VerlaufArt.NachrichtAnKunden => "Nachricht der IT",
        VerlaufArt.AntwortDesKunden => "Antwort",
        VerlaufArt.Abgeschlossen => "Abgeschlossen",
        VerlaufArt.WiederGeoeffnet or VerlaufArt.VomKundenWiederGeoeffnet => "Wieder geöffnet",
        _ => "",
    };

    /// <summary>Öffnet ein Bearbeiter wieder, ist sein Grund für die IT geschrieben und bleibt intern.</summary>
    public static bool ZeigtText(VerlaufArt art) =>
        art is VerlaufArt.NachrichtAnKunden or VerlaufArt.AntwortDesKunden or VerlaufArt.Abgeschlossen
            or VerlaufArt.VomKundenWiederGeoeffnet;

    /// <summary>Ob der Eintrag vom Kunden stammt.</summary>
    public static bool VomKunden(VerlaufArt art) =>
        art is VerlaufArt.AntwortDesKunden or VerlaufArt.VomKundenWiederGeoeffnet;

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
        Entwurf = TempData["Entwurf"] as string;
        Verlauf = auftrag.Verlauf
            .Where(v => v.FuerKundenSichtbar)
            .OrderBy(v => v.Zeitpunkt).ThenBy(v => v.Id)
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, string? text, List<IFormFile> dateien)
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());
        var auftrag = await db.Auftraege
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .FirstOrDefaultAsync(a => a.Id == id && a.KundeId == kunde.Id);
        if (auftrag is null)
            return NotFound();

        var anhaenge = new List<Anhang>();
        try
        {
            anhaenge = await ablage.SpeichernAsync(dateien);
            var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
            var jetzt = uhr.GetUtcNow().UtcDateTime;
            if (auftrag.Status == AuftragStatus.Abgeschlossen)
            {
                auftrag.KundeOeffnetWieder(ich, text, jetzt, anhaenge);
                TempData["Meldung"] = "Der Auftrag ist wieder geöffnet und liegt bei der IT.";
            }
            else
            {
                auftrag.KundeAntwortet(ich, text, jetzt, anhaenge);
                TempData["Meldung"] = "Die Nachricht wurde an die IT gesendet.";
            }
            await db.SaveChangesAsync();
        }
        catch (AuftragException ex)
        {
            ablage.Verwerfen(anhaenge);
            TempData.Remove("Meldung");
            TempData["Fehler"] = ex.Message;
            TempData["Entwurf"] = text;
        }

        return RedirectToPage(new { id });
    }
}
