using Avis.Web.Anmeldung;
using Avis.Web.Hilfe;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Avis.Web.Pages.Hilfe;

/// <summary>
/// Die Hilfe. Bearbeiter sehen die Hilfe für Bearbeiter und können zu der
/// für Kunden wechseln, Administratoren außerdem zu ihrer eigenen. Wer kein
/// Bearbeiter ist, sieht nur die für Kunden.
/// </summary>
public class IndexModel(Hilfetexte texte) : PageModel
{
    public string Thema { get; private set; } = Hilfetexte.Kunden;
    public Hilfeseite Seite { get; private set; } = null!;
    public IHtmlContent? EigenerText { get; private set; }

    public IActionResult OnGet(string? thema)
    {
        var gewuenscht = thema?.ToLowerInvariant();
        if (gewuenscht is not null && !Hilfetexte.Themen.Contains(gewuenscht))
            return NotFound();

        // Die Hilfe für Bearbeiter und die für Administratoren beschreiben, was andere nicht sehen.
        // Wer sie nicht lesen darf, bekommt stattdessen seine eigene.
        if (gewuenscht is not null && !Hilfeverweise.Erlaubt(gewuenscht, User.IstBearbeiter(), User.IstAdministrator()))
            return RedirectToPage(new { thema = (string?)null });

        Thema = gewuenscht ?? Hilfeverweise.Standardthema(User.IstBearbeiter());
        Seite = texte.Lesen(Thema);
        EigenerText = texte.EigenerText();
        return Page();
    }
}
