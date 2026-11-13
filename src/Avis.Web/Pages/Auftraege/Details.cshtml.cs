using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

public class DetailsModel(AvisDb db, TimeProvider uhr) : PageModel
{
    public Auftrag Auftrag { get; private set; } = null!;
    public List<SelectListItem> Gruppen { get; private set; } = [];
    public List<SelectListItem> Bearbeiter { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var auftrag = await db.AuftraegeFuer(User.BenutzerId())
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Kunde)
            .Include(a => a.ErfasstVon)
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .Include(a => a.BevorzugterBearbeiter)
            .Include(a => a.Kategorie)
            .Include(a => a.Standort)
            .Include(a => a.Inventargeraet)
            .Include(a => a.Verlauf).ThenInclude(v => v.Benutzer)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (auftrag is null)
            return NotFound();

        Auftrag = auftrag;
        Gruppen = await db.Gruppen.AsNoTracking()
            .Where(g => g.Aktiv)
            .OrderBy(g => g.Name)
            .Select(g => new SelectListItem(g.Gesperrt ? g.Name + " (gesperrt)" : g.Name, g.Id.ToString()))
            .ToListAsync();
        Bearbeiter = await db.Benutzer.AsNoTracking()
            .Where(b => b.Aktiv && b.IstBearbeiter)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.Id.ToString()))
            .ToListAsync();
        return Page();
    }

    public Task<IActionResult> OnPostUebernehmenAsync(int id, int version) =>
        AusfuehrenAsync(id, version, "Du hast den Auftrag übernommen.",
            (auftrag, ich, jetzt) => auftrag.Uebernehmen(ich, jetzt));

    public Task<IActionResult> OnPostWeiterleitenAsync(int id, int version, int gruppeId, int? bevorzugtId, string? text) =>
        AusfuehrenAsync(id, version, "Der Auftrag wurde weitergeleitet.", async (auftrag, ich, jetzt) =>
        {
            var ziel = await db.Gruppen.FirstOrDefaultAsync(g => g.Id == gruppeId && g.Aktiv)
                ?? throw new AuftragException("Bitte eine Gruppe wählen.");
            var bevorzugt = bevorzugtId is null
                ? null
                : await db.Benutzer.FirstOrDefaultAsync(b => b.Id == bevorzugtId && b.Aktiv && b.IstBearbeiter)
                    ?? throw new AuftragException("Der bevorzugte Bearbeiter wurde nicht gefunden.");
            if (bevorzugt is not null && ziel.Gesperrt
                && !await db.Gruppen.AnyAsync(g => g.Id == ziel.Id && g.Mitglieder.Any(m => m.Id == bevorzugt.Id)))
                throw new AuftragException($"{bevorzugt.Name} ist kein Mitglied der gesperrten Gruppe {ziel.Name}.");
            auftrag.Weiterleiten(ziel, ich, jetzt, bevorzugt, text);
        });

    public Task<IActionResult> OnPostInternerKommentarAsync(int id, int version, string? text) =>
        AusfuehrenAsync(id, version, "Der interne Kommentar wurde gespeichert.",
            (auftrag, ich, jetzt) => auftrag.InternKommentieren(ich, Pflichttext(text), jetzt),
            versionPruefen: false);

    public Task<IActionResult> OnPostNachrichtAnKundenAsync(int id, int version, string? text) =>
        AusfuehrenAsync(id, version, "Die Nachricht an den Kunden wurde gespeichert.",
            (auftrag, ich, jetzt) => auftrag.NachrichtAnKunden(ich, Pflichttext(text), jetzt),
            versionPruefen: false);

    public Task<IActionResult> OnPostAbschliessenAsync(int id, int version, string? text) =>
        AusfuehrenAsync(id, version, "Der Auftrag wurde abgeschlossen.",
            (auftrag, ich, jetzt) => auftrag.Abschliessen(ich, Pflichttext(text), jetzt));

    public Task<IActionResult> OnPostWiederOeffnenAsync(int id, int version, string? text) =>
        AusfuehrenAsync(id, version, "Der Auftrag wurde wieder geöffnet.",
            (auftrag, ich, jetzt) => auftrag.WiederOeffnen(ich, jetzt, text));

    private Task<IActionResult> AusfuehrenAsync(
        int id,
        int version,
        string meldung,
        Action<Auftrag, Benutzer, DateTime> aktion,
        bool versionPruefen = true) =>
        AusfuehrenAsync(id, version, meldung, (auftrag, ich, jetzt) =>
        {
            aktion(auftrag, ich, jetzt);
            return Task.CompletedTask;
        }, versionPruefen);

    /// <summary>
    /// Lädt den Auftrag, führt die Aktion aus und speichert. Aktionen, die
    /// Gruppe, Bearbeiter oder Abschluss ändern, werden abgelehnt, wenn der
    /// Auftrag seit dem Laden der Seite geändert wurde.
    /// </summary>
    private async Task<IActionResult> AusfuehrenAsync(
        int id,
        int version,
        string meldung,
        Func<Auftrag, Benutzer, DateTime, Task> aktion,
        bool versionPruefen = true)
    {
        var auftrag = await db.AuftraegeFuer(User.BenutzerId())
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (auftrag is null)
            return NotFound();

        const string veraltet = "Der Auftrag wurde inzwischen von jemand anderem geändert. Bitte prüfe den aktuellen Stand.";
        try
        {
            if (versionPruefen && auftrag.Version != version)
                throw new AuftragException(veraltet);

            var ich = await db.Benutzer.FirstAsync(b => b.Id == User.BenutzerId());
            await aktion(auftrag, ich, uhr.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            TempData["Meldung"] = meldung;
        }
        catch (AuftragException ex)
        {
            TempData["Fehler"] = ex.Message;
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Fehler"] = veraltet;
        }

        // Nach dem Weiterleiten an eine gesperrte Gruppe kann der Auftrag außer Sicht sein.
        if (!await db.AuftraegeFuer(User.BenutzerId()).AnyAsync(a => a.Id == id))
        {
            TempData["Meldung"] = $"{TempData["Meldung"]} Der Auftrag {id} liegt jetzt in einer gesperrten Gruppe und ist für dich nicht mehr sichtbar.";
            return RedirectToPage("/Index");
        }

        return RedirectToPage(new { id });
    }

    private static string Pflichttext(string? text) =>
        string.IsNullOrWhiteSpace(text) ? throw new AuftragException("Bitte einen Text eingeben.") : text;
}
