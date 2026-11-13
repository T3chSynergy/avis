using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Pages.Verwaltung;

public class MailsModel(AvisDb db, IOptions<MailOptionen> optionen, TimeProvider uhr) : PageModel
{
    public const int Hoechstzahl = 100;

    public record Zeile(int Id, DateTime ErstelltAm, int AuftragId, VerlaufArt Anlass, string? An, BenachrichtigungStatus Status, int Versuche, string? Hinweis);

    public string Art => optionen.Value.Art;
    public IReadOnlyList<Zeile> Mails { get; private set; } = [];

    public static string Text(BenachrichtigungStatus status) => status switch
    {
        BenachrichtigungStatus.Offen => "wartet",
        BenachrichtigungStatus.Gesendet => "gesendet",
        BenachrichtigungStatus.Fehlgeschlagen => "fehlgeschlagen",
        BenachrichtigungStatus.Entfallen => "entfallen",
        _ => status.ToString(),
    };

    public async Task OnGetAsync() =>
        Mails = await db.Benachrichtigungen.AsNoTracking()
            .OrderByDescending(b => b.Id)
            .Take(Hoechstzahl)
            .Select(b => new Zeile(b.Id, b.ErstelltAm, b.Verlaufseintrag.AuftragId, b.Verlaufseintrag.Art, b.An, b.Status, b.Versuche, b.Hinweis))
            .ToListAsync();

    public async Task<IActionResult> OnPostErneutAsync(int id)
    {
        var benachrichtigung = await db.Benachrichtigungen
            .FirstOrDefaultAsync(b => b.Id == id && b.Status == BenachrichtigungStatus.Fehlgeschlagen);
        if (benachrichtigung is null)
            return NotFound();

        benachrichtigung.Status = BenachrichtigungStatus.Offen;
        benachrichtigung.Versuche = 0;
        benachrichtigung.NaechsterVersuchAm = uhr.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        TempData["Meldung"] = "Die Mail wird erneut versucht.";
        return RedirectToPage();
    }
}
