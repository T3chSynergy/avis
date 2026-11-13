using System.Globalization;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

/// <summary>Suche und Filter über alle Aufträge, die der Bearbeiter sehen darf.</summary>
public class IndexModel(AvisDb db) : PageModel
{
    public const int Hoechstzahl = 200;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>aktiv, offen, bearbeitung, abgeschlossen oder alle.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Gruppe { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Bearbeiter { get; set; }

    public IReadOnlyList<Auftrag> Auftraege { get; private set; } = [];
    public bool Gekuerzt { get; private set; }
    public List<SelectListItem> Gruppen { get; private set; } = [];
    public List<SelectListItem> Bearbeiterliste { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var ich = User.BenutzerId();
        var sichtbar = db.AuftraegeFuer(ich);
        var gesucht = !string.IsNullOrWhiteSpace(Q);

        // Wer nur eine Auftragsnummer eingibt, will zu diesem Auftrag.
        if (Abfragen.Auftragsnummer(Q) is { } nummer && Status is null && Gruppe is null && Bearbeiter is null
            && await sichtbar.AnyAsync(a => a.Id == nummer))
            return RedirectToPage("Details", new { id = nummer });

        // Eine Suche ohne Angabe zum Status schließt abgeschlossene Aufträge ein.
        if (Status is not ("aktiv" or "offen" or "bearbeitung" or "abgeschlossen" or "alle"))
            Status = gesucht ? "alle" : "aktiv";

        var auftraege = sichtbar.FuerListe().Suchen(Q);
        if (Gruppe is not null)
            auftraege = auftraege.Where(a => a.GruppeId == Gruppe);
        if (Bearbeiter is not null)
            auftraege = auftraege.Where(a => a.BearbeiterId == Bearbeiter && a.AbgeschlossenAm == null);

        var sortiert = Status switch
        {
            "offen" => auftraege.Offen().NachPrioritaet(),
            "bearbeitung" => auftraege.InBearbeitung().NachPrioritaet(),
            "abgeschlossen" => auftraege.Where(a => a.AbgeschlossenAm != null).OrderByDescending(a => a.AbgeschlossenAm),
            "alle" => auftraege.OrderByDescending(a => a.Id),
            _ => auftraege.Where(a => a.AbgeschlossenAm == null).NachPrioritaet(),
        };

        var liste = await sortiert.Take(Hoechstzahl + 1).ToListAsync();
        Gekuerzt = liste.Count > Hoechstzahl;
        Auftraege = liste.Take(Hoechstzahl).ToList();

        Gruppen = await db.Gruppen.AsNoTracking()
            .Where(g => g.Aktiv)
            .OrderBy(g => g.Name)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(CultureInfo.InvariantCulture)))
            .ToListAsync();
        Bearbeiterliste = await db.Benutzer.AsNoTracking()
            .Where(b => b.Aktiv && b.IstBearbeiter)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.Id.ToString(CultureInfo.InvariantCulture)))
            .ToListAsync();
        return Page();
    }
}
