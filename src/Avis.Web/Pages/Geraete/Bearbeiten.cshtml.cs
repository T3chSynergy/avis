using System.Globalization;
using System.ComponentModel.DataAnnotations;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Pages.Kunden;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Geraete;

/// <summary>Anlegen und Bearbeiten eines Geräts. Nur für Administratoren.</summary>
public class BearbeitenModel(AvisDb db) : PageModel
{
    public class Formular
    {
        [Required(ErrorMessage = "Bitte eine Inventarnummer angeben.")]
        [StringLength(50, ErrorMessage = "Höchstens 50 Zeichen.")]
        public string? Inventarnummer { get; set; }

        [Required(ErrorMessage = "Bitte eine Bezeichnung angeben.")]
        [StringLength(200, ErrorMessage = "Höchstens 200 Zeichen.")]
        public string? Bezeichnung { get; set; }

        [StringLength(100, ErrorMessage = "Höchstens 100 Zeichen.")]
        public string? Typ { get; set; }

        public int? StandortId { get; set; }
        public bool Aktiv { get; set; } = true;
    }

    public int? Id { get; private set; }

    [BindProperty]
    public Formular Eingabe { get; set; } = new();

    /// <summary>Der Nutzer des Geräts. Leer, wenn es keinem Kunden zugeordnet ist.</summary>
    [BindProperty]
    public int? KundeId { get; set; }

    [BindProperty]
    public string? Suche { get; set; }

    public List<SelectListItem> Standorte { get; private set; } = [];
    public Kundenauswahl Kundenauswahl { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Inventargeraet? geraet = null;
        if (id is not null)
        {
            geraet = await db.Geraete.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            if (geraet is null)
                return NotFound();

            Eingabe = new Formular
            {
                Inventarnummer = geraet.Inventarnummer,
                Bezeichnung = geraet.Bezeichnung,
                Typ = geraet.Typ,
                StandortId = geraet.StandortId,
                Aktiv = geraet.Aktiv,
            };
            KundeId = geraet.KundeId;
        }

        return await SeiteAsync(id, geraet);
    }

    /// <summary>Sucht Kunden und zeigt das Formular mit allen Eingaben wieder an.</summary>
    public async Task<IActionResult> OnPostSuchenAsync(int? id)
    {
        ModelState.Clear();
        return await SeiteAsync(id, await LadenAsync(id));
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        var geraet = await LadenAsync(id);
        if (id is not null && geraet is null)
            return NotFound();

        if (ModelState.IsValid)
        {
            var schluessel = Inventargeraet.Schluessel(Eingabe.Inventarnummer!);
            if (await db.Geraete.AnyAsync(g => g.Nummernschluessel == schluessel && g.Id != id))
                ModelState.AddModelError("Eingabe.Inventarnummer", "Diese Inventarnummer gibt es schon.");

            if (Eingabe.StandortId is not null && Eingabe.StandortId != geraet?.StandortId
                && !await db.Standorte.AnyAsync(s => s.Id == Eingabe.StandortId && s.Aktiv))
                ModelState.AddModelError("Eingabe.StandortId", "Dieser Standort steht nicht zur Auswahl.");

            if (KundeId is not null && !await db.Kunden.AnyAsync(k => k.Id == KundeId))
                ModelState.AddModelError("", "Dieser Kunde wurde nicht gefunden.");
        }

        if (!ModelState.IsValid)
            return await SeiteAsync(id, geraet);

        if (geraet is null)
        {
            geraet = new Inventargeraet();
            db.Geraete.Add(geraet);
        }

        geraet.Inventarnummer = Eingabe.Inventarnummer!.Trim();
        geraet.Bezeichnung = Eingabe.Bezeichnung!.Trim();
        geraet.Typ = string.IsNullOrWhiteSpace(Eingabe.Typ) ? null : Eingabe.Typ.Trim();
        geraet.StandortId = Eingabe.StandortId;
        geraet.KundeId = KundeId;
        geraet.Aktiv = Eingabe.Aktiv;
        await db.SaveChangesAsync();

        TempData["Meldung"] = $"Gerät {geraet.Inventarnummer} wurde gespeichert.";
        return RedirectToPage("Details", new { id = geraet.Id });
    }

    private async Task<Inventargeraet?> LadenAsync(int? id) =>
        id is null ? null : await db.Geraete.FirstOrDefaultAsync(g => g.Id == id);

    private async Task<IActionResult> SeiteAsync(int? id, Inventargeraet? geraet)
    {
        Id = id;
        var bisherigerStandort = geraet?.StandortId;
        Standorte = await db.Standorte.AsNoTracking()
            .Where(s => s.Aktiv || s.Id == bisherigerStandort)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(CultureInfo.InvariantCulture)))
            .ToListAsync();
        Kundenauswahl = await Kundenauswahl.LadenAsync(db, Suche, KundeId, leer: "kein");
        return Page();
    }
}
