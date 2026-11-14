using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Gruppen;

public class IndexModel(AvisDb db) : PageModel
{
    public const string NachGruppe = "gruppe";
    public const string NachPrioritaet = "prio";
    public const string NachLiegezeit = "liegezeit";

    /// <summary>
    /// Wie die offenen Aufträge gezeigt werden: je Gruppe eine Liste, oder alle
    /// Gruppen zusammen nach Priorität oder nach Liegezeit.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Ansicht { get; set; } = NachGruppe;

    public IReadOnlyList<Gruppe> Gruppen { get; private set; } = [];
    public Dictionary<int, IReadOnlyList<Auftrag>> Offen { get; } = [];

    /// <summary>Die Aufträge aller Gruppen in der Reihenfolge der Ansicht.</summary>
    public IReadOnlyList<Auftrag> Alle { get; private set; } = [];

    public async Task OnGetAsync()
    {
        if (Ansicht is not (NachPrioritaet or NachLiegezeit))
            Ansicht = NachGruppe;

        var ich = User.BenutzerId();

        Gruppen = await db.Gruppen.AsNoTracking()
            .Where(g => g.Aktiv && g.Mitglieder.Any(m => m.Id == ich))
            .OrderBy(g => g.Name)
            .ToListAsync();

        var ids = Gruppen.Select(g => g.Id).ToList();
        var offen = db.AuftraegeFuer(ich).FuerListe()
            .Offen()
            .Where(a => ids.Contains(a.GruppeId));

        // Die Liegezeit ist das Alter des Auftrags, wie es die Liste zeigt.
        Alle = await (Ansicht == NachLiegezeit
            ? offen.OrderBy(a => a.ErstelltAm).ThenBy(a => a.Id)
            : offen.NachPrioritaet()).ToListAsync();

        foreach (var gruppe in Gruppen)
            Offen[gruppe.Id] = Alle.Where(a => a.GruppeId == gruppe.Id).ToList();
    }
}
