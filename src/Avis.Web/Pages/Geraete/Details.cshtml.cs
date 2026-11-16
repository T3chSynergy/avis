using Avis.Web.Abgleich;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Pages.Geraete;

public class DetailsModel(AvisDb db, IOptions<GeraeteabgleichOptionen> abgleich) : PageModel
{
    public const int Hoechstzahl = 50;

    public Inventargeraet Geraet { get; private set; } = null!;

    /// <summary>Die Seite des Geräts in Snipe-IT, wenn es von dort kommt und die Adresse eingestellt ist.</summary>
    public string? InSnipeIt { get; private set; }
    public IReadOnlyList<Auftrag> Laufend { get; private set; } = [];
    public IReadOnlyList<Auftrag> Abgeschlossen { get; private set; } = [];
    public bool Gekuerzt { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var geraet = await db.Geraete.AsNoTracking()
            .Include(g => g.Kunde)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (geraet is null)
            return NotFound();

        Geraet = geraet;
        InSnipeIt = AdresseInSnipeIt(geraet, abgleich.Value.Adresse);
        var auftraege = db.AuftraegeFuer(User.BenutzerId()).FuerListe().Where(a => a.Geraete.Any(g => g.Id == id));
        Laufend = await auftraege.Where(a => a.AbgeschlossenAm == null).NachPrioritaet().ToListAsync();

        var abgeschlossen = await auftraege
            .Where(a => a.AbgeschlossenAm != null)
            .OrderByDescending(a => a.AbgeschlossenAm)
            .Take(Hoechstzahl + 1)
            .ToListAsync();
        Gekuerzt = abgeschlossen.Count > Hoechstzahl;
        Abgeschlossen = abgeschlossen.Take(Hoechstzahl).ToList();
        return Page();
    }

    /// <summary>
    /// Snipe-IT zeigt ein Asset unter "/hardware/" und seiner Kennung. Die
    /// Kennung ist die, unter der der Abgleich das Gerät führt.
    /// </summary>
    public static string? AdresseInSnipeIt(Inventargeraet geraet, string? adresse)
    {
        if (geraet.Quelle != GeraeteabgleichOptionen.SnipeIt || string.IsNullOrWhiteSpace(geraet.ExterneKennung))
            return null;
        if (!Uri.TryCreate(adresse?.Trim(), UriKind.Absolute, out var basis) || basis.Scheme is not ("https" or "http"))
            return null;

        return $"{basis.AbsoluteUri.TrimEnd('/')}/hardware/{Uri.EscapeDataString(geraet.ExterneKennung)}";
    }
}
