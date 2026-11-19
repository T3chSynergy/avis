using System.Net;
using System.Text.RegularExpressions;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Abgleich;
using Avis.Web.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Avis.Tests;

/// <summary>Startet die Anwendung mit einer eigenen, leeren Datenbank und den Testdaten.</summary>
public class AvisAnwendung : WebApplicationFactory<Program>
{
    private readonly string _ordner = Directory.CreateTempSubdirectory("avis-test-").FullName;

    /// <summary>Fängt alle Mails auf, die die Anwendung versendet.</summary>
    public Postfach Postfach { get; } = new();

    /// <summary>Der Ordner, in dem die Anwendung Anhänge ablegt.</summary>
    public string Anhangordner => Path.Combine(_ordner, "Anhaenge");

    public int DateienInDerAblage() =>
        Directory.Exists(Anhangordner) ? Directory.EnumerateFiles(Anhangordner, "*", SearchOption.AllDirectories).Count() : 0;

    /// <summary>Steht für das Active Directory.</summary>
    public Testverzeichnis Verzeichnis { get; } = new();

    /// <summary>Steht für das Active Directory als Quelle der Bearbeiter.</summary>
    public Testbearbeiterquelle Bearbeiterquelle { get; } = new();

    /// <summary>Steht für das Active Directory als Quelle der Kunden.</summary>
    public Testkundenquelle Kundenquelle { get; } = new();

    /// <summary>Steht für Snipe-IT als Quelle der Geräte.</summary>
    public Testgeraetequelle Geraetequelle { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Avis", $"Data Source={Path.Combine(_ordner, "avis.db")};Pooling=False");
        builder.UseSetting("Anmeldung:Art", "Entwicklung");
        builder.UseSetting("Testdaten:Anlegen", "true");
        builder.UseSetting("Mail:Art", "Ordner");
        builder.UseSetting("Mail:Ablage", Path.Combine(_ordner, "Mails"));
        builder.UseSetting("Mail:Absender", "it-hotline@avis.test");
        builder.UseSetting("Mail:BasisUrl", "https://avis.test/");
        // Die Tests stoßen den Versand selbst an.
        builder.UseSetting("Mail:ImHintergrundSenden", "false");
        builder.UseSetting("Anhaenge:Ordner", Anhangordner);
        builder.UseSetting("Anhaenge:HoechstgroesseMb", "1");
        builder.UseSetting("Anhaenge:Hoechstzahl", "3");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IMailversand>(Postfach);
            services.AddSingleton<IVerzeichnis>(Verzeichnis);
            services.AddSingleton<IBearbeiterquelle>(Bearbeiterquelle);
            services.AddSingleton<IKundenquelle>(Kundenquelle);
            services.AddSingleton<IGeraetequelle>(Geraetequelle);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_ordner))
            Directory.Delete(_ordner, recursive: true);
    }

    public async Task<HttpClient> AnmeldenAlsAsync(string anmeldename)
    {
        var client = CreateClient();
        var antwort = await client.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = anmeldename });
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        return client;
    }

    /// <summary>Meldet ein Konto an, das AVIS noch nicht kennt, wie beim ersten Aufruf im Betrieb.</summary>
    public async Task<HttpClient> AnmeldenAlsNeuAsync(string anmeldename)
    {
        var client = CreateClient();
        var antwort = await client.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = "", ["neu"] = anmeldename });
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        return client;
    }

    /// <summary>Versendet alles, was fällig ist, und gibt die dabei gesendeten Mails zurück.</summary>
    public async Task<List<MimeMessage>> MailsVersendenAsync()
    {
        var vorher = Postfach.Mails.Count;
        using var scope = Services.CreateScope();
        var versand = scope.ServiceProvider.GetRequiredService<Benachrichtigungsversand>();
        var db = scope.ServiceProvider.GetRequiredService<AvisDb>();
        while (true)
        {
            await versand.VerarbeitenAsync();
            var jetzt = DateTime.UtcNow;
            if (Postfach.Gestoert || !await db.Benachrichtigungen.AnyAsync(b => b.Status == BenachrichtigungStatus.Offen && b.NaechsterVersuchAm <= jetzt))
                break;
        }
        return Postfach.Mails.Skip(vorher).ToList();
    }

    public async Task<T> DatenbankAsync<T>(Func<AvisDb, Task<T>> abfrage)
    {
        using var scope = Services.CreateScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AvisDb>());
    }
}

public class Postfach : IMailversand
{
    public List<MimeMessage> Mails { get; } = [];

    /// <summary>Solange gesetzt, schlägt jeder Versand fehl wie bei einem nicht erreichbaren Mailserver.</summary>
    public bool Gestoert { get; set; }

    public Task SendenAsync(MimeMessage mail, CancellationToken abbruch)
    {
        if (Gestoert)
            throw new IOException("Der Mailserver ist nicht erreichbar.");
        Mails.Add(mail);
        return Task.CompletedTask;
    }
}

public class Testbearbeiterquelle : IBearbeiterquelle
{
    public string Name => "ActiveDirectory";

    public List<Quellbearbeiter> Bearbeiter { get; } = [];

    /// <summary>Solange gesetzt, lässt sich die Quelle nicht lesen.</summary>
    public bool Gestoert { get; set; }

    /// <summary>Wie oft die Quelle gelesen wurde, auch vergeblich.</summary>
    public int Abrufe { get; private set; }

    public IReadOnlyList<Quellbearbeiter> Lesen()
    {
        Abrufe++;
        return Gestoert ? throw new InvalidOperationException("Der Server ist nicht funktionstüchtig.") : Bearbeiter.ToList();
    }
}

public class Testkundenquelle : IKundenquelle
{
    public string Name => "ActiveDirectory";

    public List<Quellkunde> Kunden { get; } = [];

    /// <summary>Solange gesetzt, lässt sich die Quelle nicht lesen.</summary>
    public bool Gestoert { get; set; }

    /// <summary>Wie oft die Quelle gelesen wurde, auch vergeblich.</summary>
    public int Abrufe { get; private set; }

    public IReadOnlyList<Quellkunde> Lesen()
    {
        Abrufe++;
        return Gestoert ? throw new InvalidOperationException("Der Server ist nicht funktionstüchtig.") : Kunden.ToList();
    }
}

public class Testgeraetequelle : IGeraetequelle
{
    public string Name => "SnipeIt";

    public List<Quellgeraet> Geraete { get; } = [];

    /// <summary>Solange gesetzt, lässt sich die Quelle nicht lesen.</summary>
    public bool Gestoert { get; set; }

    /// <summary>Solange gesetzt, antwortet die Quelle nicht rechtzeitig. So meldet .NET eine Zeitüberschreitung.</summary>
    public bool ZuLangsam { get; set; }

    /// <summary>Wie oft die Quelle gelesen wurde, auch vergeblich.</summary>
    public int Abrufe { get; private set; }

    public Task<IReadOnlyList<Quellgeraet>> LesenAsync(CancellationToken abbruch)
    {
        Abrufe++;
        if (ZuLangsam)
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 60 seconds elapsing.");
        return Gestoert
            ? throw new InvalidOperationException("Snipe-IT hat den Zugangsschlüssel abgelehnt.")
            : Task.FromResult<IReadOnlyList<Quellgeraet>>(Geraete.ToList());
    }
}

public class Testverzeichnis : IVerzeichnis
{
    public Dictionary<string, Verzeichniseintrag> Eintraege { get; } = [];

    public Verzeichniseintrag? Suchen(string anmeldename) => Eintraege.GetValueOrDefault(anmeldename);
}

public static partial class ClientExtensions
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenMuster();

    /// <summary>Holt das Formular, liest das Sicherheitstoken aus und sendet die Felder ab.</summary>
    public static async Task<HttpResponseMessage> PostFormularAsync(
        this HttpClient client, string formularSeite, string ziel, Dictionary<string, string> felder)
    {
        var seite = await client.GetStringAsync(formularSeite);
        felder["__RequestVerificationToken"] = TokenMuster().Match(seite).Groups[1].Value;
        return await client.PostAsync(ziel, new FormUrlEncodedContent(felder));
    }

    /// <summary>Übernimmt den Auftrag auf seinem aktuellen Stand. Das ist die Voraussetzung für alles Weitere.</summary>
    public static async Task UebernehmenAsync(this HttpClient client, AvisAnwendung app, int id)
    {
        var version = await app.DatenbankAsync(db => db.Auftraege.Where(a => a.Id == id).Select(a => a.Version).FirstAsync());
        var antwort = await client.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=Uebernehmen", new() { ["version"] = version.ToString() });
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.DoesNotContain("meldung fehler", await antwort.Content.ReadAsStringAsync());
    }

    /// <summary>Wie PostFormularAsync, aber mit Dateien im Feld "dateien".</summary>
    public static async Task<HttpResponseMessage> PostMitDateienAsync(
        this HttpClient client, string formularSeite, string ziel, Dictionary<string, string> felder, params (string Name, byte[] Inhalt)[] dateien)
    {
        var seite = await client.GetStringAsync(formularSeite);
        felder["__RequestVerificationToken"] = TokenMuster().Match(seite).Groups[1].Value;

        var inhalt = new MultipartFormDataContent();
        foreach (var (name, wert) in felder)
            inhalt.Add(new StringContent(wert), name);
        foreach (var (name, bytes) in dateien)
        {
            var teil = new ByteArrayContent(bytes);
            teil.Headers.ContentType = new("application/octet-stream");
            inhalt.Add(teil, "dateien", name);
        }
        return await client.PostAsync(ziel, inhalt);
    }
}
