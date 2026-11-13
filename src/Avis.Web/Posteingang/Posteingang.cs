using System.Collections.Concurrent;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Avis.Web.Posteingang;

public class PosteingangOptionen
{
    public const string Aus = "Aus";
    public const string Imap = "Imap";

    /// <summary>"Aus": Es wird kein Postfach abgerufen. "Imap": Abruf über IMAP.</summary>
    public string Art { get; set; } = Aus;

    public string? Server { get; set; }
    public int Port { get; set; } = 993;

    /// <summary>"Auto", "Keine", "StartTls" oder "Ssl".</summary>
    public string Verschluesselung { get; set; } = "Auto";

    public string? Benutzer { get; set; }
    public string? Kennwort { get; set; }

    /// <summary>Der Ordner, aus dem ungelesene Mails geholt werden.</summary>
    public string Ordner { get; set; } = "INBOX";

    /// <summary>
    /// Ordner, in den verarbeitete Mails verschoben werden. Leer lassen, dann
    /// bleiben sie als gelesen im Eingang.
    /// </summary>
    public string? Erledigtordner { get; set; }

    public int AbstandSekunden { get; set; } = 60;

    /// <summary>Kleinere Bilder im Text einer Mail gelten als Logo einer Signatur und werden nicht übernommen.</summary>
    public int BilderImTextAbKb { get; set; } = 20;
}

/// <summary>Holt neue Mails aus dem Hotline-Postfach.</summary>
public interface IPosteingang
{
    /// <summary>
    /// Übergibt jede ungelesene Mail an die Verarbeitung und markiert sie
    /// danach als gelesen. Gibt die Zahl der verarbeiteten Mails zurück.
    /// </summary>
    Task<int> AbrufenAsync(Func<MimeMessage, CancellationToken, Task> verarbeiten, CancellationToken abbruch);
}

public class ImapPosteingang(IOptions<PosteingangOptionen> optionen, ILogger<ImapPosteingang> log) : IPosteingang
{
    private const int Hoechstversuche = 3;

    /// <summary>Fehlversuche je Mail, damit eine unverarbeitbare Mail den Abruf nicht endlos beschäftigt.</summary>
    private readonly ConcurrentDictionary<string, int> _fehlversuche = new();

    public async Task<int> AbrufenAsync(Func<MimeMessage, CancellationToken, Task> verarbeiten, CancellationToken abbruch)
    {
        var o = optionen.Value;
        var verschluesselung = o.Verschluesselung switch
        {
            "Keine" => SecureSocketOptions.None,
            "StartTls" => SecureSocketOptions.StartTls,
            "Ssl" => SecureSocketOptions.SslOnConnect,
            "Auto" => SecureSocketOptions.Auto,
            _ => throw new InvalidOperationException($"Unbekannte Einstellung Posteingang:Verschluesselung = {o.Verschluesselung}."),
        };

        using var client = new ImapClient { Timeout = 30_000 };
        await client.ConnectAsync(o.Server!, o.Port, verschluesselung, abbruch);
        await client.AuthenticateAsync(o.Benutzer!, o.Kennwort ?? "", abbruch);

        var ordner = string.Equals(o.Ordner, "INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox
            : await client.GetFolderAsync(o.Ordner, abbruch);
        await ordner.OpenAsync(FolderAccess.ReadWrite, abbruch);
        var erledigt = string.IsNullOrWhiteSpace(o.Erledigtordner) ? null : await client.GetFolderAsync(o.Erledigtordner, abbruch);

        var ungelesen = (await ordner.SearchAsync(SearchOptions.None, SearchQuery.NotSeen, abbruch)).UniqueIds;
        var verarbeitet = 0;
        foreach (var uid in ungelesen.Take(50))
        {
            var mail = await ordner.GetMessageAsync(uid, abbruch);
            var schluessel = mail.MessageId ?? uid.ToString();
            try
            {
                await verarbeiten(mail, abbruch);
                await ordner.StoreAsync([uid], new StoreFlagsRequest(StoreAction.Add, MessageFlags.Seen) { Silent = true }, abbruch);
                if (erledigt is not null)
                    await ordner.MoveToAsync([uid], new MoveRequest(erledigt), abbruch);
                _fehlversuche.TryRemove(schluessel, out _);
                verarbeitet++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var versuche = _fehlversuche.AddOrUpdate(schluessel, 1, (_, bisher) => bisher + 1);
                log.LogError(ex, "Die Mail {Betreff} von {Von} konnte nicht verarbeitet werden (Versuch {Versuch}).",
                    mail.Subject, mail.From.ToString(), versuche);

                // Nach mehreren Versuchen bleibt die Mail markiert im Postfach
                // liegen, damit ein Mensch sie sich ansieht.
                if (versuche >= Hoechstversuche)
                {
                    await ordner.StoreAsync([uid], new StoreFlagsRequest(StoreAction.Add, MessageFlags.Seen | MessageFlags.Flagged) { Silent = true }, abbruch);
                    _fehlversuche.TryRemove(schluessel, out _);
                }
            }
        }

        await client.DisconnectAsync(quit: true, abbruch);
        return verarbeitet;
    }
}

/// <summary>Ruft das Postfach in festen Abständen ab, solange die Anwendung läuft.</summary>
public class Posteingangshintergrund(
    IServiceScopeFactory scopes,
    IPosteingang posteingang,
    IOptions<PosteingangOptionen> optionen,
    ILogger<Posteingangshintergrund> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken abbruch)
    {
        using var takt = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, optionen.Value.AbstandSekunden)));
        try
        {
            while (await takt.WaitForNextTickAsync(abbruch))
            {
                try
                {
                    // Jede Mail in einem eigenen Bereich: Ein Fehler bei einer Mail berührt die nächste nicht.
                    await posteingang.AbrufenAsync(async (mail, ct) =>
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<Mailannahme>().AnnehmenAsync(mail, ct);
                    }, abbruch);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Der Abruf des Postfachs ist fehlgeschlagen.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Die Anwendung wird beendet.
        }
    }
}

public static class PosteingangExtensions
{
    public static void AddAvisPosteingang(this WebApplicationBuilder builder)
    {
        var abschnitt = builder.Configuration.GetSection("Posteingang");
        builder.Services.Configure<PosteingangOptionen>(abschnitt);
        builder.Services.AddScoped<Mailannahme>();

        var optionen = abschnitt.Get<PosteingangOptionen>() ?? new PosteingangOptionen();
        switch (optionen.Art)
        {
            case PosteingangOptionen.Aus:
                break;
            case PosteingangOptionen.Imap:
                if (string.IsNullOrWhiteSpace(optionen.Server) || string.IsNullOrWhiteSpace(optionen.Benutzer))
                    throw new InvalidOperationException("Für Posteingang:Art = Imap müssen Posteingang:Server und Posteingang:Benutzer gesetzt sein.");
                builder.Services.AddSingleton<IPosteingang, ImapPosteingang>();
                builder.Services.AddHostedService<Posteingangshintergrund>();
                break;
            default:
                throw new InvalidOperationException($"Unbekannte Einstellung Posteingang:Art = {optionen.Art}.");
        }
    }
}
