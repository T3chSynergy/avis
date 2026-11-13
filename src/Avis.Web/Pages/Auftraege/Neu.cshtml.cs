using System.ComponentModel.DataAnnotations;
using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Pages.Kunden;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

public class NeuModel(AvisDb db, TimeProvider uhr, Anhangablage ablage) : PageModel
{
    public class Formular
    {
        [StringLength(200)]
        public string? KundeName { get; set; }

        [EmailAddress(ErrorMessage = "Das ist keine gültige E-Mail-Adresse.")]
        [StringLength(320)]
        public string? KundeEmail { get; set; }

        [StringLength(50)]
        public string? KundeTelefon { get; set; }

        public AuftragFelder Felder { get; set; } = new();

        public Eingang Eingang { get; set; } = Eingang.Telefon;
        public int GruppeId { get; set; }
        public bool SofortUebernehmen { get; set; }
    }

    [BindProperty]
    public Formular Eingabe { get; set; } = new();

    /// <summary>Der gewählte Kunde. Leer, wenn ein neuer Kunde angelegt wird.</summary>
    [BindProperty]
    public int? KundeId { get; set; }

    [BindProperty]
    public string? Suche { get; set; }

    [BindProperty]
    public List<IFormFile> Dateien { get; set; } = [];

    public List<SelectListItem> Gruppen { get; private set; } = [];
    public AuftragAuswahl Auswahl { get; private set; } = null!;
    public Kundenauswahl Kundenauswahl { get; private set; } = null!;

    public async Task OnGetAsync(int? kundeId)
    {
        KundeId = kundeId;
        await ListenLadenAsync();
        Eingabe.GruppeId = await db.Gruppen
            .Where(g => g.IstEingangsgruppe && g.Aktiv)
            .Select(g => g.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>Sucht Kunden und zeigt das Formular mit allen Eingaben wieder an.</summary>
    public async Task<IActionResult> OnPostSuchenAsync()
    {
        ModelState.Clear();
        await ListenLadenAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var neuerName = Eingabe.KundeName?.Trim() ?? "";

        // Eingabetaste im Suchfeld ohne Skript: Das ist eine Suche, kein Erfassen.
        if (KundeId is null && neuerName.Length == 0 && !string.IsNullOrWhiteSpace(Suche))
            return await OnPostSuchenAsync();

        Kunde? kunde = null;
        if (KundeId is not null)
        {
            // Die Felder für einen neuen Kunden spielen dann keine Rolle.
            foreach (var feld in new[] { nameof(Formular.KundeName), nameof(Formular.KundeEmail), nameof(Formular.KundeTelefon) })
                ModelState.Remove($"{nameof(Eingabe)}.{feld}");

            kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Id == KundeId);
            if (kunde is null)
            {
                ModelState.AddModelError(nameof(KundeId), "Dieser Kunde wurde nicht gefunden.");
                KundeId = null;
            }
        }
        else if (neuerName.Length == 0)
        {
            ModelState.AddModelError(nameof(KundeId), "Bitte einen Kunden auswählen oder den Namen eines neuen Kunden angeben.");
        }

        var gruppe = await db.Gruppen.FirstOrDefaultAsync(g => g.Id == Eingabe.GruppeId && g.Aktiv);
        if (gruppe is null)
            ModelState.AddModelError("Eingabe.GruppeId", "Bitte eine Gruppe wählen.");

        var angaben = await Eingabe.Felder.AlsAngabenAsync(db, ModelState, "Eingabe.Felder");
        if (angaben is null || gruppe is null)
        {
            await ListenLadenAsync();
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
            await ListenLadenAsync();
            return Page();
        }

        var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
        var jetzt = uhr.GetUtcNow().UtcDateTime;
        kunde ??= await KundeFindenOderAnlegenAsync();

        var auftrag = Auftrag.Erfassen(angaben, kunde, ich, gruppe, Eingabe.Eingang, jetzt, anhaenge);
        if (Eingabe.SofortUebernehmen)
            auftrag.Uebernehmen(ich, jetzt);

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

        TempData["Meldung"] = $"Auftrag {auftrag.Id} wurde erfasst.";
        return RedirectToPage("Details", new { id = auftrag.Id });
    }

    private async Task<Kunde> KundeFindenOderAnlegenAsync()
    {
        var email = string.IsNullOrWhiteSpace(Eingabe.KundeEmail) ? null : Eingabe.KundeEmail.Trim().ToLowerInvariant();
        var telefon = string.IsNullOrWhiteSpace(Eingabe.KundeTelefon) ? null : Eingabe.KundeTelefon.Trim();

        if (email is not null)
        {
            var vorhanden = await db.Kunden.FirstOrDefaultAsync(k => k.Email == email);
            if (vorhanden is not null)
                return vorhanden;
        }

        var kunde = new Kunde { Name = Eingabe.KundeName!.Trim(), Email = email, Telefon = telefon };
        db.Kunden.Add(kunde);
        return kunde;
    }

    private async Task ListenLadenAsync()
    {
        Gruppen = await db.Gruppen.AsNoTracking()
            .Where(g => g.Aktiv)
            .OrderByDescending(g => g.IstEingangsgruppe).ThenBy(g => g.Name)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString()))
            .ToListAsync();
        Auswahl = await AuftragAuswahl.LadenAsync(db);
        Kundenauswahl = await Kundenauswahl.LadenAsync(db, Suche, KundeId, leer: "neu");
    }
}
