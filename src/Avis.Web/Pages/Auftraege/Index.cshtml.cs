using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Auftraege;

public class IndexModel(AvisDb db) : PageModel
{
    public const int Hoechstzahl = 200;

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "aktiv";

    public IReadOnlyList<Auftrag> Auftraege { get; private set; } = [];
    public bool Gekuerzt { get; private set; }

    public async Task OnGetAsync()
    {
        var auftraege = db.Auftraege.FuerListe();
        IQueryable<Auftrag> sortiert;

        switch (Status)
        {
            case "offen":
                sortiert = auftraege.Offen().NachPrioritaet();
                break;
            case "bearbeitung":
                sortiert = auftraege.InBearbeitung().NachPrioritaet();
                break;
            case "abgeschlossen":
                sortiert = auftraege.Where(a => a.AbgeschlossenAm != null).OrderByDescending(a => a.AbgeschlossenAm);
                break;
            default:
                Status = "aktiv";
                sortiert = auftraege.Where(a => a.AbgeschlossenAm == null).NachPrioritaet();
                break;
        }

        var liste = await sortiert.Take(Hoechstzahl + 1).ToListAsync();
        Gekuerzt = liste.Count > Hoechstzahl;
        Auftraege = liste.Take(Hoechstzahl).ToList();
    }
}
