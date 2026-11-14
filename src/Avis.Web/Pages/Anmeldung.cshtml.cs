using System.Security.Claims;
using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Mail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Pages;

/// <summary>Testanmeldung für die Entwicklung. Im Betrieb gibt es diese Seite nicht.</summary>
public class AnmeldungModel(
    AvisDb db,
    IOptions<AnmeldungOptionen> optionen,
    IOptions<MailOptionen> mail,
    Anhangablage ablage,
    IConfiguration konfiguration,
    IWebHostEnvironment umgebung,
    ILogger<AnmeldungModel> log) : PageModel
{
    public IReadOnlyList<Benutzer> Benutzer { get; private set; } = [];
    public string? ReturnUrl { get; private set; }

    /// <summary>Ob sich die Datenbank von hier aus mit Testdaten neu anlegen lässt.</summary>
    public bool MitTestdaten => umgebung.IsDevelopment() && konfiguration.GetValue<bool>("Testdaten:Anlegen");

    public string Mailart => mail.Value.Art;

    /// <summary>Der Ordner, in dem Mails als Dateien landen, wenn Mail:Art = Ordner gilt.</summary>
    public string Mailordner => Path.GetFullPath(mail.Value.Ablage, umgebung.ContentRootPath);

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        if (optionen.Value.Art != AnmeldungOptionen.Entwicklung)
            return NotFound();

        ReturnUrl = returnUrl;
        Benutzer = await db.Benutzer.AsNoTracking().Where(b => b.Aktiv).OrderBy(b => b.Name).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? anmeldename, string? neu, string? returnUrl)
    {
        if (optionen.Value.Art != AnmeldungOptionen.Entwicklung)
            return NotFound();

        if (string.IsNullOrEmpty(anmeldename))
        {
            // Ein neues Konto: Es entsteht wie im Betrieb beim ersten Aufruf.
            anmeldename = neu?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(anmeldename))
                return RedirectToPage();
        }
        else if (!await db.Benutzer.AnyAsync(b => b.Anmeldename == anmeldename && b.Aktiv))
        {
            return NotFound();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, anmeldename)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToPage("/Index");
    }

    /// <summary>Leert die Datenbank und legt die Testdaten neu an.</summary>
    public async Task<IActionResult> OnPostNeuAnlegenAsync(bool bestaetigt)
    {
        if (optionen.Value.Art != AnmeldungOptionen.Entwicklung || !MitTestdaten)
            return NotFound();

        if (!bestaetigt)
        {
            TempData["Fehler"] = "Bitte bestätige, dass alle Daten gelöscht werden sollen.";
            return RedirectToPage();
        }

        // Die Dateien der Anhänge gehören zu den Daten, die verschwinden.
        var anhaenge = await db.Anhaenge.AsNoTracking().ToListAsync();
        await Startdaten.NeuAnlegenAsync(db, konfiguration, umgebung, log);
        ablage.Verwerfen(anhaenge);

        // Das angemeldete Konto gibt es womöglich nicht mehr.
        await HttpContext.SignOutAsync();
        TempData["Meldung"] = "Die Datenbank wurde geleert und mit Testdaten neu angelegt.";
        return RedirectToPage();
    }
}
