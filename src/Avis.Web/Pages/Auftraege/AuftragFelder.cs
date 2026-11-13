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
    public int? StandortId { get; set; }

    [StringLength(200, ErrorMessage = "Das Gerät darf höchstens 200 Zeichen lang sein.")]
    public string? Geraet { get; set; }

    public Stufe Dringlichkeit { get; set; } = Stufe.Mittel;
    public Stufe Wichtigkeit { get; set; } = Stufe.Mittel;
    public DateOnly? Termin { get; set; }

    public static AuftragFelder Aus(Auftrag auftrag) => new()
    {
        Betreff = auftrag.Betreff,
        Beschreibung = auftrag.Beschreibung,
        KategorieId = auftrag.KategorieId,
        StandortId = auftrag.StandortId,
        Geraet = auftrag.Geraet,
        Dringlichkeit = auftrag.Dringlichkeit,
        Wichtigkeit = auftrag.Wichtigkeit,
        Termin = auftrag.Termin,
    };

    /// <summary>
    /// Wandelt das Formular in Angaben um. Zur Auswahl stehen aktive
    /// Kategorien und Standorte sowie die, die der Auftrag schon hat.
    /// Bei einer ungültigen Auswahl steht der Fehler im ModelState und das
    /// Ergebnis ist null.
    /// </summary>
    public async Task<AuftragAngaben?> AlsAngabenAsync(AvisDb db, ModelStateDictionary modelState, string praefix, Auftrag? bisher = null)
    {
        var bisherigeKategorie = bisher?.KategorieId;
        var bisherigerStandort = bisher?.StandortId;

        Kategorie? kategorie = null;
        if (KategorieId is not null)
        {
            kategorie = await db.Kategorien.FirstOrDefaultAsync(k => k.Id == KategorieId && (k.Aktiv || k.Id == bisherigeKategorie));
            if (kategorie is null)
                modelState.AddModelError($"{praefix}.{nameof(KategorieId)}", "Diese Kategorie steht nicht zur Auswahl.");
        }

        Standort? standort = null;
        if (StandortId is not null)
        {
            standort = await db.Standorte.FirstOrDefaultAsync(s => s.Id == StandortId && (s.Aktiv || s.Id == bisherigerStandort));
            if (standort is null)
                modelState.AddModelError($"{praefix}.{nameof(StandortId)}", "Dieser Standort steht nicht zur Auswahl.");
        }

        if (!modelState.IsValid)
            return null;

        return new AuftragAngaben(Betreff!, Beschreibung!)
        {
            Kategorie = kategorie,
            Standort = standort,
            Geraet = Geraet,
            Dringlichkeit = Dringlichkeit,
            Wichtigkeit = Wichtigkeit,
            Termin = Termin,
        };
    }
}

/// <summary>Die Auswahllisten für die Ansicht _Auftragsfelder.</summary>
public record AuftragAuswahl(List<SelectListItem> Kategorien, List<SelectListItem> Standorte)
{
    public static async Task<AuftragAuswahl> LadenAsync(AvisDb db, Auftrag? bisher = null)
    {
        var kategorieId = bisher?.KategorieId;
        var standortId = bisher?.StandortId;

        var kategorien = await db.Kategorien.AsNoTracking()
            .Where(k => k.Aktiv || k.Id == kategorieId)
            .OrderBy(k => k.Name)
            .Select(k => new SelectListItem(k.Name, k.Id.ToString()))
            .ToListAsync();
        var standorte = await db.Standorte.AsNoTracking()
            .Where(s => s.Aktiv || s.Id == standortId)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString()))
            .ToListAsync();
        return new(kategorien, standorte);
    }
}
