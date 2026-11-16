using System.Globalization;
using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

/// <param name="Auftrag">Der andere Auftrag.</param>
/// <param name="Genannt">Ob der angezeigte Auftrag ihn nennt. Sonst nennt nur der andere den angezeigten.</param>
public record Bezug(Auftrag Auftrag, bool Genannt);

public class DetailsModel(AvisDb db, TimeProvider uhr, Anhangablage ablage) : PageModel
{
    /// <summary>In dieser Anfrage gespeicherte Dateien. Scheitert die Aktion, werden sie wieder gelöscht.</summary>
    private readonly List<Anhang> _neueAnhaenge = [];

    public Auftrag Auftrag { get; private set; } = null!;

    /// <summary>
    /// Die Aufträge, die dieser nennt oder die ihn nennen (siehe
    /// <see cref="Auftragsbezug"/>), soweit der Angemeldete sie sehen darf.
    /// </summary>
    public IReadOnlyList<Bezug> Bezuege { get; private set; } = [];

    public List<SelectListItem> Gruppen { get; private set; } = [];
    public List<SelectListItem> Bearbeiter { get; private set; } = [];

    /// <summary>
    /// Nach einem Fehler: welches Formular wieder aufgeht und was darin
    /// stand, damit ein geschriebener Text nicht verloren geht.
    /// </summary>
    public string? OffeneAktion { get; private set; }
    public string? Entwurf { get; private set; }

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
            .Include(a => a.Geraete)
            .Include(a => a.Verlauf).ThenInclude(v => v.Benutzer)
            .Include(a => a.Verlauf).ThenInclude(v => v.Anhaenge)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (auftrag is null)
            return NotFound();

        Auftrag = auftrag;

        // Der Bezug gilt in beide Richtungen: Am genannten Auftrag steht, wer ihn nennt.
        // Ein Auftrag einer gesperrten Gruppe erscheint nur bei dem, der ihn sehen darf.
        var genannt = await db.Auftraege.Where(a => a.Id == id).SelectMany(a => a.Bezuege.Select(b => b.Id)).ToListAsync();
        Bezuege = (await db.AuftraegeFuer(User.BenutzerId())
                .AsNoTracking()
                .Where(a => genannt.Contains(a.Id) || a.Bezuege.Any(b => b.Id == id))
                .OrderBy(a => a.Id)
                .ToListAsync())
            .Select(a => new Bezug(a, genannt.Contains(a.Id)))
            .ToList();

        OffeneAktion = TempData["Aktion"] as string;
        Entwurf = TempData["Entwurf"] as string;
        Gruppen = await db.Gruppen.AsNoTracking()
            .Where(g => g.Aktiv)
            .OrderBy(g => g.Name)
            .Select(g => new SelectListItem(g.Gesperrt ? g.Name + " (gesperrt)" : g.Name, g.Id.ToString(CultureInfo.InvariantCulture)))
            .ToListAsync();
        Bearbeiter = await db.Benutzer.AsNoTracking()
            .Where(b => b.Aktiv && b.IstBearbeiter)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.Id.ToString(CultureInfo.InvariantCulture)))
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
        }, formular: "weiterleiten", entwurf: text);

    public Task<IActionResult> OnPostInternerKommentarAsync(int id, int version, string? text, List<IFormFile> dateien) =>
        AusfuehrenAsync(id, version, "Der interne Kommentar wurde gespeichert.",
            async (auftrag, ich, jetzt) => auftrag.InternKommentieren(ich, text, jetzt, await AnhaengeSpeichernAsync(dateien)),
            versionPruefen: false, formular: "intern", entwurf: text);

    public Task<IActionResult> OnPostNachrichtAnKundenAsync(int id, int version, string? text, List<IFormFile> dateien) =>
        AusfuehrenAsync(id, version, "Die Nachricht an den Kunden wurde gespeichert.",
            async (auftrag, ich, jetzt) => auftrag.NachrichtAnKunden(ich, text, jetzt, await AnhaengeSpeichernAsync(dateien)),
            versionPruefen: false, formular: "nachricht", entwurf: text);

    /// <summary>Hängt an die Fehlermeldung an, welche Dateien noch einmal anzuhängen sind.</summary>
    private string MitDateihinweis(string fehler) =>
        Request.HasFormContentType && Anhangablage.NochmalAnhaengen(Request.Form.Files) is { } hinweis ? $"{fehler} {hinweis}" : fehler;

    private async Task<List<Anhang>> AnhaengeSpeichernAsync(List<IFormFile> dateien)
    {
        var anhaenge = await ablage.SpeichernAsync(dateien);
        _neueAnhaenge.AddRange(anhaenge);
        return anhaenge;
    }

    public Task<IActionResult> OnPostAbschliessenAsync(int id, int version, string? text) =>
        AusfuehrenAsync(id, version, "Der Auftrag wurde abgeschlossen.",
            (auftrag, ich, jetzt) => auftrag.Abschliessen(ich, Pflichttext(text), jetzt),
            formular: "abschliessen", entwurf: text);

    public Task<IActionResult> OnPostWiederOeffnenAsync(int id, int version, string? text) =>
        AusfuehrenAsync(id, version, "Der Auftrag wurde wieder geöffnet.",
            (auftrag, ich, jetzt) => auftrag.WiederOeffnen(ich, jetzt, text),
            formular: "oeffnen", entwurf: text);

    private Task<IActionResult> AusfuehrenAsync(
        int id,
        int version,
        string meldung,
        Action<Auftrag, Benutzer, DateTime> aktion,
        bool versionPruefen = true,
        string? formular = null,
        string? entwurf = null) =>
        AusfuehrenAsync(id, version, meldung, (auftrag, ich, jetzt) =>
        {
            aktion(auftrag, ich, jetzt);
            return Task.CompletedTask;
        }, versionPruefen, formular, entwurf);

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
        bool versionPruefen = true,
        string? formular = null,
        string? entwurf = null)
    {
        var auftrag = await db.AuftraegeFuer(User.BenutzerId())
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (auftrag is null)
            return NotFound();

        const string veraltet = "Der Auftrag wurde inzwischen geändert. Bitte prüfe den aktuellen Stand.";
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
            Gescheitert(ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            Gescheitert(veraltet);
        }

        void Gescheitert(string fehler)
        {
            ablage.Verwerfen(_neueAnhaenge);
            TempData["Fehler"] = MitDateihinweis(fehler);
            // Das Formular geht mit dem geschriebenen Text wieder auf.
            if (formular is not null)
            {
                TempData["Aktion"] = formular;
                TempData["Entwurf"] = entwurf;
            }
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
