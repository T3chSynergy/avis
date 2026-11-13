using System.Security.Claims;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Pages;

/// <summary>Testanmeldung für die Entwicklung. Im Betrieb gibt es diese Seite nicht.</summary>
public class AnmeldungModel(AvisDb db, IOptions<AnmeldungOptionen> optionen) : PageModel
{
    public IReadOnlyList<Benutzer> Benutzer { get; private set; } = [];
    public string? ReturnUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        if (optionen.Value.Art != AnmeldungOptionen.Entwicklung)
            return NotFound();

        ReturnUrl = returnUrl;
        Benutzer = await db.Benutzer.AsNoTracking().Where(b => b.Aktiv).OrderBy(b => b.Name).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string anmeldename, string? returnUrl)
    {
        if (optionen.Value.Art != AnmeldungOptionen.Entwicklung)
            return NotFound();

        if (!await db.Benutzer.AnyAsync(b => b.Anmeldename == anmeldename && b.Aktiv))
            return NotFound();

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, anmeldename)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToPage("/Index");
    }
}
