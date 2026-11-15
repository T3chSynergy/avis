using Avis.Web.Anmeldung;
using Avis.Web.Hilfe;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Avis.Web.Pages.Hilfe;

/// <summary>
/// Die Hilfe. Bearbeiter sehen die Hilfe für Bearbeiter und können zu der
/// für Kunden wechseln; wer kein Bearbeiter ist, sieht nur die für Kunden.
/// </summary>
public class IndexModel(Hilfetexte texte) : PageModel
{
    public string Thema { get; private set; } = Hilfetexte.Kunden;
    public Hilfeseite Seite { get; private set; } = null!;
    public IHtmlContent? EigenerText { get; private set; }

    public IActionResult OnGet(string? thema)
    {
        var gewuenscht = thema?.ToLowerInvariant();
        if (gewuenscht is not (null or Hilfetexte.Bearbeiter or Hilfetexte.Kunden))
            return NotFound();

        // Die Hilfe für Bearbeiter beschreibt, was Kunden nicht sehen. Sie bekommen stattdessen ihre eigene.
        if (gewuenscht == Hilfetexte.Bearbeiter && !User.IstBearbeiter())
            return RedirectToPage(new { thema = (string?)null });

        Thema = gewuenscht ?? (User.IstBearbeiter() ? Hilfetexte.Bearbeiter : Hilfetexte.Kunden);
        Seite = texte.Lesen(Thema);
        EigenerText = texte.EigenerText();
        return Page();
    }
}
