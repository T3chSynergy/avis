using System.ComponentModel.DataAnnotations;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

public class NeuModel(AvisDb db, TimeProvider uhr) : PageModel
{
    public class Formular
    {
        [Required(ErrorMessage = "Bitte den Namen des Kunden angeben.")]
        [StringLength(200)]
        public string? KundeName { get; set; }

        [EmailAddress(ErrorMessage = "Das ist keine gültige E-Mail-Adresse.")]
        [StringLength(320)]
        public string? KundeEmail { get; set; }

        [StringLength(50)]
        public string? KundeTelefon { get; set; }

        [Required(ErrorMessage = "Bitte einen Betreff angeben.")]
        [StringLength(200, ErrorMessage = "Der Betreff darf höchstens 200 Zeichen lang sein.")]
        public string? Betreff { get; set; }

        [Required(ErrorMessage = "Bitte eine Beschreibung angeben.")]
        public string? Beschreibung { get; set; }

        public Eingang Eingang { get; set; } = Eingang.Telefon;
        public int? KategorieId { get; set; }
        public int? StandortId { get; set; }

        [StringLength(200)]
        public string? Geraet { get; set; }

        public Stufe Dringlichkeit { get; set; } = Stufe.Mittel;
        public Stufe Wichtigkeit { get; set; } = Stufe.Mittel;
        public DateOnly? Termin { get; set; }
        public int GruppeId { get; set; }
        public bool SofortUebernehmen { get; set; }
    }

    [BindProperty]
    public Formular Eingabe { get; set; } = new();

    public List<SelectListItem> Gruppen { get; private set; } = [];
    public List<SelectListItem> Kategorien { get; private set; } = [];
    public List<SelectListItem> Standorte { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await ListenLadenAsync();
        Eingabe.GruppeId = await db.Gruppen
            .Where(g => g.IstEingangsgruppe && g.Aktiv)
            .Select(g => g.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var gruppe = await db.Gruppen.FirstOrDefaultAsync(g => g.Id == Eingabe.GruppeId && g.Aktiv);
        if (gruppe is null)
            ModelState.AddModelError("Eingabe.GruppeId", "Bitte eine Gruppe wählen.");

        if (!ModelState.IsValid || gruppe is null)
        {
            await ListenLadenAsync();
            return Page();
        }

        var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
        var jetzt = uhr.GetUtcNow().UtcDateTime;
        var kunde = await KundeFindenOderAnlegenAsync();

        var auftrag = Auftrag.Erfassen(
            Eingabe.Betreff!.Trim(),
            Eingabe.Beschreibung!.Trim(),
            kunde,
            ich,
            gruppe,
            Eingabe.Eingang,
            jetzt);
        auftrag.KategorieId = Eingabe.KategorieId;
        auftrag.StandortId = Eingabe.StandortId;
        auftrag.Geraet = string.IsNullOrWhiteSpace(Eingabe.Geraet) ? null : Eingabe.Geraet.Trim();
        auftrag.Dringlichkeit = Eingabe.Dringlichkeit;
        auftrag.Wichtigkeit = Eingabe.Wichtigkeit;
        auftrag.Termin = Eingabe.Termin;

        if (Eingabe.SofortUebernehmen)
            auftrag.Uebernehmen(ich, jetzt);

        db.Auftraege.Add(auftrag);
        await db.SaveChangesAsync();

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
        Kategorien = await db.Kategorien.AsNoTracking()
            .Where(k => k.Aktiv)
            .OrderBy(k => k.Name)
            .Select(k => new SelectListItem(k.Name, k.Id.ToString()))
            .ToListAsync();
        Standorte = await db.Standorte.AsNoTracking()
            .Where(s => s.Aktiv)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
            .ToListAsync();
    }
}
