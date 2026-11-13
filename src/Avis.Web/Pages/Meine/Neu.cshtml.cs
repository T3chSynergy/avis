using System.ComponentModel.DataAnnotations;
using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Meine;

/// <summary>
/// Das Formular für Kunden. Es fragt nur das Nötigste, alles andere ergänzt
/// die Hotline. Der Auftrag geht immer an die Eingangsgruppe.
/// </summary>
public class NeuModel(AvisDb db, Kundenzuordnung zuordnung, TimeProvider uhr, Anhangablage ablage) : PageModel
{
    public class Formular
    {
        [Required(ErrorMessage = "Bitte angeben, worum es geht.")]
        [StringLength(200, ErrorMessage = "Höchstens 200 Zeichen.")]
        public string? Betreff { get; set; }

        [Required(ErrorMessage = "Bitte eine Beschreibung angeben.")]
        public string? Beschreibung { get; set; }

        public int? StandortId { get; set; }

        [StringLength(200, ErrorMessage = "Höchstens 200 Zeichen.")]
        public string? Geraet { get; set; }
    }

    [BindProperty]
    public Formular Eingabe { get; set; } = new();

    [BindProperty]
    public List<IFormFile> Dateien { get; set; } = [];

    public List<SelectListItem> Standorte { get; private set; } = [];
    public string? Email { get; private set; }

    public async Task OnGetAsync()
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());
        Eingabe.StandortId = await db.Standorte
            .Where(s => s.Id == kunde.StandortId && s.Aktiv)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();
        await SeiteVorbereitenAsync(kunde);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());

        Standort? standort = null;
        if (Eingabe.StandortId is not null)
        {
            standort = await db.Standorte.FirstOrDefaultAsync(s => s.Id == Eingabe.StandortId && s.Aktiv);
            if (standort is null)
                ModelState.AddModelError("Eingabe.StandortId", "Dieser Standort steht nicht zur Auswahl.");
        }

        var eingang = await db.Gruppen.FirstOrDefaultAsync(g => g.IstEingangsgruppe && g.Aktiv);
        if (eingang is null)
            ModelState.AddModelError("", "Aufträge können gerade nicht angenommen werden. Bitte die IT-Hotline anrufen.");

        if (!ModelState.IsValid || eingang is null)
        {
            await SeiteVorbereitenAsync(kunde);
            return Page();
        }

        List<Anhang> anhaenge;
        try
        {
            anhaenge = await ablage.SpeichernAsync(Dateien);
        }
        catch (AuftragException ex)
        {
            ModelState.AddModelError(nameof(Dateien), ex.Message);
            await SeiteVorbereitenAsync(kunde);
            return Page();
        }

        var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
        var auftrag = Auftrag.Erfassen(
            new AuftragAngaben(Eingabe.Betreff!, Eingabe.Beschreibung!) { Standort = standort, Geraet = Eingabe.Geraet },
            kunde,
            ich,
            eingang,
            Eingang.Web,
            uhr.GetUtcNow().UtcDateTime,
            anhaenge);

        db.Auftraege.Add(auftrag);
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            ablage.Verwerfen(anhaenge);
            throw;
        }

        TempData["Meldung"] = $"Der Auftrag {auftrag.Id} ist bei der IT-Hotline eingegangen.";
        return RedirectToPage("Details", new { id = auftrag.Id });
    }

    private async Task SeiteVorbereitenAsync(Kunde kunde)
    {
        Email = kunde.Email;
        Standorte = await db.Standorte.AsNoTracking()
            .Where(s => s.Aktiv)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
            .ToListAsync();
    }
}
