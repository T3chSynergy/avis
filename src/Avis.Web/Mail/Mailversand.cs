using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Avis.Web.Mail;

/// <summary>Bringt eine fertige Mail auf den Weg.</summary>
public interface IMailversand
{
    Task SendenAsync(MimeMessage mail, CancellationToken abbruch);
}

public class SmtpMailversand(IOptions<MailOptionen> optionen) : IMailversand
{
    public async Task SendenAsync(MimeMessage mail, CancellationToken abbruch)
    {
        var o = optionen.Value;
        var verschluesselung = o.Verschluesselung switch
        {
            "Keine" => SecureSocketOptions.None,
            "StartTls" => SecureSocketOptions.StartTls,
            "Ssl" => SecureSocketOptions.SslOnConnect,
            "Auto" => SecureSocketOptions.Auto,
            _ => throw new InvalidOperationException($"Unbekannte Einstellung Mail:Verschluesselung = {o.Verschluesselung}."),
        };

        using var client = new SmtpClient { Timeout = 15_000 };
        await client.ConnectAsync(o.Server!, o.Port, verschluesselung, abbruch);
        if (!string.IsNullOrEmpty(o.Benutzer))
            await client.AuthenticateAsync(o.Benutzer, o.Kennwort ?? "", abbruch);
        await client.SendAsync(new SendRequest(mail), abbruch);
        await client.DisconnectAsync(quit: true, abbruch);
    }
}

/// <summary>Legt jede Mail als Datei ab, zum Ausprobieren ohne Mailserver.</summary>
public class OrdnerMailversand(IOptions<MailOptionen> optionen, IWebHostEnvironment umgebung, TimeProvider uhr) : IMailversand
{
    public async Task SendenAsync(MimeMessage mail, CancellationToken abbruch)
    {
        var ordner = Path.GetFullPath(optionen.Value.Ablage, umgebung.ContentRootPath);
        Directory.CreateDirectory(ordner);

        var name = $"{uhr.GetUtcNow():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml";
        await mail.WriteToAsync(Path.Combine(ordner, name), abbruch);
    }
}

public static class MailExtensions
{
    public static void AddAvisMail(this WebApplicationBuilder builder)
    {
        var abschnitt = builder.Configuration.GetSection("Mail");
        builder.Services.Configure<MailOptionen>(abschnitt);
        var optionen = abschnitt.Get<MailOptionen>() ?? new MailOptionen();

        switch (optionen.Art)
        {
            case MailOptionen.Aus:
                break;
            case MailOptionen.Ordner:
                builder.Services.AddSingleton<IMailversand, OrdnerMailversand>();
                break;
            case MailOptionen.Smtp:
                if (string.IsNullOrWhiteSpace(optionen.Server) || string.IsNullOrWhiteSpace(optionen.Absender))
                    throw new InvalidOperationException("Für Mail:Art = Smtp müssen Mail:Server und Mail:Absender gesetzt sein.");
                builder.Services.AddSingleton<IMailversand, SmtpMailversand>();
                break;
            default:
                throw new InvalidOperationException($"Unbekannte Einstellung Mail:Art = {optionen.Art}.");
        }

        builder.Services.AddScoped<Benachrichtigungsversand>();
        if (optionen.ImHintergrundSenden)
            builder.Services.AddHostedService<Mailhintergrund>();
    }
}
