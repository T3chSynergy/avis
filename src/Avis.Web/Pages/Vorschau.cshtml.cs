using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Avis.Web.Pages;

/// <summary>
/// Zeigt einen Text so, wie er später im Auftrag steht: für die Vorschau an
/// den Textfeldern. Die Umwandlung ist dieselbe wie bei der Anzeige
/// (<see cref="Markdowntext"/>), deshalb stimmt die Vorschau mit dem
/// Ergebnis überein. Gespeichert wird hier nichts.
/// </summary>
public class VorschauModel : PageModel
{
    public IHtmlContent Inhalt { get; private set; } = HtmlString.Empty;

    /// <summary>Die Seite antwortet nur dem Skript der Textfelder.</summary>
    public IActionResult OnGet() => NotFound();

    public void OnPost(string? text) => Inhalt = Markdowntext.AlsHtml(text);
}
