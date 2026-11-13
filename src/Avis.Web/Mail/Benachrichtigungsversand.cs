using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Avis.Web.Mail;

/// <summary>Versendet vorgemerkte Benachrichtigungen und wiederholt fehlgeschlagene Versuche.</summary>
public class Benachrichtigungsversand(
    AvisDb db,
    IOptions<MailOptionen> optionen,
    TimeProvider uhr,
    ILogger<Benachrichtigungsversand> log,
    IMailversand? versand = null)
{
    public const int Hoechstversuche = 6;

    /// <summary>Wartezeit in Minuten nach dem ersten, zweiten, dritten ... Fehlversuch.</summary>
    private static readonly int[] Wartezeiten = [1, 5, 15, 60, 240];

    /// <summary>Verarbeitet, was fällig ist. Gibt die Zahl der gesendeten Mails zurück.</summary>
    public async Task<int> VerarbeitenAsync(CancellationToken abbruch = default)
    {
        var o = optionen.Value;
        var jetzt = uhr.GetUtcNow().UtcDateTime;

        var faellig = await db.Benachrichtigungen
            .Where(b => b.Status == BenachrichtigungStatus.Offen && b.NaechsterVersuchAm <= jetzt)
            .OrderBy(b => b.Id)
            .Take(20)
            .Include(b => b.Verlaufseintrag.Benutzer)
            .Include(b => b.Verlaufseintrag.Auftrag.Kunde)
            .Include(b => b.Verlaufseintrag.Auftrag.Gruppe)
            .Include(b => b.Verlaufseintrag.Auftrag.Bearbeiter)
            .Include(b => b.Verlaufseintrag.Auftrag.BevorzugterBearbeiter)
            .AsSplitQuery()
            .ToListAsync(abbruch);

        var gesendet = 0;
        foreach (var benachrichtigung in faellig)
        {
            if (versand is null)
            {
                Entfaellt(benachrichtigung, "Der Mailversand ist ausgeschaltet.");
                continue;
            }

            var (mail, grund) = Mailtexte.Erstellen(benachrichtigung.Verlaufseintrag, o.BasisUrl);
            if (mail is null)
            {
                Entfaellt(benachrichtigung, grund);
                continue;
            }

            benachrichtigung.An = mail.An;
            benachrichtigung.Betreff = Kuerzen(mail.Betreff, 300);
            benachrichtigung.Versuche++;
            try
            {
                await versand.SendenAsync(AlsNachricht(mail, benachrichtigung, o), abbruch);
                benachrichtigung.Status = BenachrichtigungStatus.Gesendet;
                benachrichtigung.GesendetAm = uhr.GetUtcNow().UtcDateTime;
                benachrichtigung.Hinweis = null;
                gesendet++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                benachrichtigung.Hinweis = Kuerzen(ex.Message, 1000);
                if (benachrichtigung.Versuche >= Hoechstversuche)
                {
                    benachrichtigung.Status = BenachrichtigungStatus.Fehlgeschlagen;
                }
                else
                {
                    var warten = Wartezeiten[Math.Min(benachrichtigung.Versuche, Wartezeiten.Length) - 1];
                    benachrichtigung.NaechsterVersuchAm = jetzt.AddMinutes(warten);
                }
                log.LogWarning(ex, "Mail zu Auftrag {Auftrag} an {An} konnte nicht gesendet werden (Versuch {Versuch}).",
                    benachrichtigung.Verlaufseintrag.AuftragId, mail.An, benachrichtigung.Versuche);

                // Der Mailserver hat vermutlich ein Problem. Die übrigen Mails warten auf den nächsten Durchlauf.
                await db.SaveChangesAsync(abbruch);
                break;
            }

            await db.SaveChangesAsync(abbruch);
        }

        await db.SaveChangesAsync(abbruch);
        return gesendet;
    }

    private static void Entfaellt(Benachrichtigung benachrichtigung, string? grund)
    {
        benachrichtigung.Status = BenachrichtigungStatus.Entfallen;
        benachrichtigung.Hinweis = grund;
    }

    private static MimeMessage AlsNachricht(Mailinhalt mail, Benachrichtigung benachrichtigung, MailOptionen o)
    {
        var nachricht = new MimeMessage();
        nachricht.From.Add(new MailboxAddress(o.Absendername, o.Absender ?? "avis@localhost"));
        nachricht.To.Add(new MailboxAddress(mail.AnName, mail.An));
        nachricht.Subject = mail.Betreff;
        nachricht.Body = new TextPart("plain") { Text = mail.Text };

        // Abwesenheitsnotizen und andere automatische Antworten sollen ausbleiben.
        nachricht.Headers.Add("Auto-Submitted", "auto-generated");
        nachricht.Headers.Add("X-AVIS-Auftrag", benachrichtigung.Verlaufseintrag.AuftragId.ToString());
        return nachricht;
    }

    private static string Kuerzen(string text, int laenge) =>
        text.Length <= laenge ? text : text[..laenge];
}

/// <summary>Ruft den Versand in festen Abständen auf, solange die Anwendung läuft.</summary>
public class Mailhintergrund(IServiceScopeFactory scopes, IOptions<MailOptionen> optionen, ILogger<Mailhintergrund> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken abbruch)
    {
        using var takt = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, optionen.Value.AbstandSekunden)));
        try
        {
            while (await takt.WaitForNextTickAsync(abbruch))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<Benachrichtigungsversand>().VerarbeitenAsync(abbruch);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Der Mailversand im Hintergrund ist auf einen Fehler gelaufen.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Die Anwendung wird beendet.
        }
    }
}
