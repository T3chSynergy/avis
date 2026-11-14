using System.ComponentModel.DataAnnotations;
using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
    }

    [BindProperty]
    public Formular Eingabe { get; set; } = new();

    [BindProperty]
    public List<IFormFile> Dateien { get; set; } = [];

    public string? Email { get; private set; }

    public async Task OnGetAsync()
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());
        Email = kunde.Email;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var kunde = await zuordnung.FuerBenutzerAsync(User.BenutzerId());

        var eingang = await db.Gruppen.FirstOrDefaultAsync(g => g.IstEingangsgruppe && g.Aktiv);
        if (eingang is null)
            ModelState.AddModelError("", "Aufträge können gerade nicht angenommen werden. Bitte die IT-Hotline anrufen.");

        if (!ModelState.IsValid || eingang is null)
        {
            DateienVerloren();
            Email = kunde.Email;
            return Page();
        }

        List<Anhang> anhaenge;
        try
        {
            anhaenge = await ablage.SpeichernAsync(Dateien);
        }
        catch (AuftragException ex)
        {
            DateienVerloren(ex.Message);
            Email = kunde.Email;
            return Page();
        }

        var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
        var auftrag = Auftrag.Erfassen(
            // Standort und Gerät fragt das Formular nicht ab: Der Standort kommt vom Kunden, das Gerät trägt die Hotline ein.
            new AuftragAngaben(Eingabe.Betreff!, Eingabe.Beschreibung!),
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

    /// <summary>
    /// Sagt am Dateifeld, dass die Dateien noch einmal anzuhängen sind, und
    /// davor, was an ihnen nicht stimmte. Angezeigt wird nur eine Meldung je Feld.
    /// </summary>
    private void DateienVerloren(string? fehler = null)
    {
        var meldung = string.Join(" ", new[] { fehler, Anhangablage.NochmalAnhaengen(Dateien) }.Where(t => t is not null));
        if (meldung.Length > 0)
            ModelState.AddModelError(nameof(Dateien), meldung);
    }
}
