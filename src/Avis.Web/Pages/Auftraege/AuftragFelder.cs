using System.Globalization;
using System.ComponentModel.DataAnnotations;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

/// <summary>
/// Die Angaben eines Auftrags als Formular. Erfassen und Bearbeiten nutzen
/// dieselben Felder und dieselbe Ansicht (_Auftragsfelder).
/// </summary>
public class AuftragFelder
{
    [Required(ErrorMessage = "Bitte einen Betreff angeben.")]
    [StringLength(200, ErrorMessage = "Der Betreff darf höchstens 200 Zeichen lang sein.")]
    public string? Betreff { get; set; }

    [Required(ErrorMessage = "Bitte eine Beschreibung angeben.")]
    public string? Beschreibung { get; set; }

    public int? KategorieId { get; set; }
    public Stufe Dringlichkeit { get; set; } = Stufe.Mittel;
    public Stufe Wichtigkeit { get; set; } = Stufe.Mittel;
    public DateOnly? Termin { get; set; }

    public static AuftragFelder Aus(Auftrag auftrag) => new()
    {
        Betreff = auftrag.Betreff,
        Beschreibung = auftrag.Beschreibung,
        KategorieId = auftrag.KategorieId,
        Dringlichkeit = auftrag.Dringlichkeit,
        Wichtigkeit = auftrag.Wichtigkeit,
        Termin = auftrag.Termin,
    };

    /// <summary>
    /// Wandelt das Formular in Angaben um. Zur Auswahl stehen aktive
    /// Kategorien sowie die, die der Auftrag schon hat.
    /// Bei einer ungültigen Auswahl steht der Fehler im ModelState und das
    /// Ergebnis ist null.
    /// </summary>
    public async Task<AuftragAngaben?> AlsAngabenAsync(AvisDb db, ModelStateDictionary modelState, string praefix, Auftrag? bisher = null)
    {
        var bisherigeKategorie = bisher?.KategorieId;

        Kategorie? kategorie = null;
        if (KategorieId is not null)
        {
            kategorie = await db.Kategorien.FirstOrDefaultAsync(k => k.Id == KategorieId && (k.Aktiv || k.Id == bisherigeKategorie));
            if (kategorie is null)
                modelState.AddModelError($"{praefix}.{nameof(KategorieId)}", "Diese Kategorie steht nicht zur Auswahl.");
        }

        if (!modelState.IsValid)
            return null;

        return new AuftragAngaben(Betreff!, Beschreibung!)
        {
            Kategorie = kategorie,
            Dringlichkeit = Dringlichkeit,
            Wichtigkeit = Wichtigkeit,
            Termin = Termin,
        };
    }
}

/// <summary>Die Auswahlliste für die Ansicht _Auftragsfelder.</summary>
public record AuftragAuswahl(List<SelectListItem> Kategorien)
{
    public static async Task<AuftragAuswahl> LadenAsync(AvisDb db, Auftrag? bisher = null)
    {
        var kategorieId = bisher?.KategorieId;

        var kategorien = await db.Kategorien.AsNoTracking()
            .Where(k => k.Aktiv || k.Id == kategorieId)
            .OrderBy(k => k.Name)
            .Select(k => new SelectListItem(k.Name, k.Id.ToString(CultureInfo.InvariantCulture)))
            .ToListAsync();
        return new(kategorien);
    }
}
