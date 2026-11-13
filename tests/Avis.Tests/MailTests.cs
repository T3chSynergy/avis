using System.Net;
using System.Net.Sockets;
using System.Text;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Avis.Tests;

public class MailTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Fact]
    public async Task Der_Kunde_bekommt_Mails_bei_Erfassung_Nachricht_und_Abschluss()
    {
        var anna = await AnfangAsync();
        var id = await ErfassenAsync(anna, "Scanner zieht schief ein", "Frank Müller");
        var seite = $"/Auftraege/{id}";

        var erfasst = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal("frank.mueller@example.org", erfasst.To.Mailboxes.Single().Address);
        Assert.Equal("it-hotline@avis.test", erfasst.From.Mailboxes.Single().Address);
        Assert.Equal($"[AVIS-{id}] Erfasst: Scanner zieht schief ein", erfasst.Subject);
        Assert.Contains("Guten Tag Frank Müller,", erfasst.TextBody);
        Assert.Contains($"https://avis.test/Meine/{id}", erfasst.TextBody);
        Assert.Equal("auto-generated", erfasst.Headers["Auto-Submitted"]);

        await AktionAsync(anna, seite, "Uebernehmen");
        await AktionAsync(anna, seite, "NachrichtAnKunden", new() { ["text"] = "Welches Papier liegt im Einzug?" });
        var nachricht = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal($"[AVIS-{id}] Nachricht: Scanner zieht schief ein", nachricht.Subject);
        Assert.Contains("Welches Papier liegt im Einzug?", nachricht.TextBody);
        Assert.Contains($"zum Auftrag {id} „Scanner zieht schief ein“ gibt es eine Nachricht der IT:", nachricht.TextBody);
        Assert.Contains("Antworten ist hier möglich:", nachricht.TextBody);

        await AktionAsync(anna, seite, "Abschliessen", new() { ["text"] = "Die Einzugsrolle wurde getauscht." });
        var abschluss = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal($"[AVIS-{id}] Abgeschlossen: Scanner zieht schief ein", abschluss.Subject);
        Assert.Contains("Die Einzugsrolle wurde getauscht.", abschluss.TextBody);
    }

    [Fact]
    public async Task Internes_loest_keine_Mail_aus_und_steht_in_keiner_Mail_an_den_Kunden()
    {
        var anna = await AnfangAsync();
        var id = await ErfassenAsync(anna, "Zugang zur Fachanwendung", "Gabi Müller");
        var seite = $"/Auftraege/{id}";
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Netzwerk").Select(g => g.Id).FirstAsync());
        await app.MailsVersendenAsync();
        var vorher = await app.DatenbankAsync(db => db.Benachrichtigungen.CountAsync());

        await AktionAsync(anna, seite, "Uebernehmen");
        await AktionAsync(anna, seite, "InternerKommentar", new() { ["text"] = "GEHEIM: Sonderrechte nötig." });
        await AktionAsync(anna, seite, "Weiterleiten", new() { ["gruppeId"] = netzwerk.ToString(), ["text"] = "NUR-FUER-IT" });
        await anna.PostFormularAsync($"{seite}/Bearbeiten", $"{seite}/Bearbeiten", new()
        {
            ["Version"] = (await LadenAsync(id)).Version.ToString(),
            ["Felder.Betreff"] = "Zugang zur Fachanwendung fehlt",
            ["Felder.Beschreibung"] = "Für einen Test.",
        });

        Assert.Empty(await app.MailsVersendenAsync());
        Assert.Equal(vorher, await app.DatenbankAsync(db => db.Benachrichtigungen.CountAsync()));

        await AktionAsync(anna, seite, "Uebernehmen");
        await AktionAsync(anna, seite, "NachrichtAnKunden", new() { ["text"] = "Der Zugang ist beantragt." });
        await AktionAsync(anna, seite, "Abschliessen", new() { ["text"] = "Zugang eingerichtet." });
        var mails = await app.MailsVersendenAsync();

        Assert.Equal(2, mails.Count);
        Assert.All(mails, mail =>
        {
            Assert.Equal("gabi.mueller@example.org", mail.To.Mailboxes.Single().Address);
            Assert.DoesNotContain("GEHEIM", mail.TextBody);
            Assert.DoesNotContain("NUR-FUER-IT", mail.TextBody);
            Assert.DoesNotContain("Netzwerk", mail.TextBody);
        });
    }

    [Fact]
    public async Task Die_Antwort_des_Kunden_geht_an_den_zustaendigen_Bearbeiter()
    {
        var anna = await AnfangAsync();
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var antwort = await dieter.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = "Telefon ohne Freizeichen",
            ["Eingabe.Beschreibung"] = "Für einen Test.",
        });
        var id = int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);

        // Solange niemand zuständig ist, steht die Antwort nur im Verlauf.
        await dieter.PostFormularAsync($"/Meine/{id}", $"/Meine/{id}", new() { ["text"] = "Betrifft Apparat 1234." });
        var ohneBearbeiter = await app.MailsVersendenAsync();
        Assert.Equal("dieter.kunde@example.org", Assert.Single(ohneBearbeiter).To.Mailboxes.Single().Address);
        Assert.Contains("keinen zuständigen Bearbeiter", await LetzterHinweisAsync(id, VerlaufArt.AntwortDesKunden));

        await AktionAsync(anna, $"/Auftraege/{id}", "Uebernehmen");
        await dieter.PostFormularAsync($"/Meine/{id}", $"/Meine/{id}", new() { ["text"] = "Jetzt ist die Leitung ganz tot." });

        var mail = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal("anna.beispiel@example.org", mail.To.Mailboxes.Single().Address);
        Assert.Equal($"[AVIS-{id}] Antwort: Telefon ohne Freizeichen", mail.Subject);
        Assert.Contains("Dieter Kunde hat zum Auftrag", mail.TextBody);
        Assert.Contains("Jetzt ist die Leitung ganz tot.", mail.TextBody);
        Assert.Contains($"https://avis.test/Auftraege/{id}", mail.TextBody);
    }

    [Fact]
    public async Task Oeffnet_der_Kunde_wieder_erfaehrt_es_der_bisherige_Bearbeiter()
    {
        var anna = await AnfangAsync();
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var gestellt = await dieter.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = "Lautsprecher knackt",
            ["Eingabe.Beschreibung"] = "Für einen Test.",
        });
        var id = int.Parse(gestellt.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        await AktionAsync(anna, $"/Auftraege/{id}", "Uebernehmen");
        await AktionAsync(anna, $"/Auftraege/{id}", "Abschliessen", new() { ["text"] = "Kabel getauscht." });
        await app.MailsVersendenAsync();

        await dieter.PostFormularAsync($"/Meine/{id}", $"/Meine/{id}", new() { ["text"] = "Es knackt immer noch." });

        var mail = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal("anna.beispiel@example.org", mail.To.Mailboxes.Single().Address);
        Assert.Equal($"[AVIS-{id}] Wieder geöffnet: Lautsprecher knackt", mail.Subject);
        Assert.Contains("Dieter Kunde hat den abgeschlossenen Auftrag", mail.TextBody);
        Assert.Contains("Es knackt immer noch.", mail.TextBody);
        Assert.Contains("liegt in der Gruppe IT-Hotline", mail.TextBody);
    }

    [Fact]
    public async Task Der_bevorzugte_Bearbeiter_bekommt_einen_Hinweis()
    {
        var anna = await AnfangAsync();
        var id = await ErfassenAsync(anna, "Switch im Keller blinkt rot", "Eva Test");
        var seite = $"/Auftraege/{id}";
        await app.MailsVersendenAsync();
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Netzwerk").Select(g => g.Id).FirstAsync());
        var bernd = await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "bernd").Select(b => b.Id).FirstAsync());

        await AktionAsync(anna, seite, "Uebernehmen");
        await AktionAsync(anna, seite, "Weiterleiten", new() { ["gruppeId"] = netzwerk.ToString(), ["bevorzugtId"] = bernd.ToString(), ["text"] = "Du kennst den Schrank." });

        var mail = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal("bernd.muster@example.org", mail.To.Mailboxes.Single().Address);
        Assert.Equal($"[AVIS-{id}] Für dich: Switch im Keller blinkt rot", mail.Subject);
        Assert.Contains("liegt in der Gruppe Netzwerk und nennt dich als bevorzugten Bearbeiter", mail.TextBody);
        Assert.Contains("Du kennst den Schrank.", mail.TextBody);
    }

    [Fact]
    public async Task Der_Hinweis_entfaellt_wenn_der_Auftrag_schon_uebernommen_ist()
    {
        var anna = await AnfangAsync();
        var id = await ErfassenAsync(anna, "Patchfeld beschriften", "Eva Test");
        var seite = $"/Auftraege/{id}";
        await app.MailsVersendenAsync();
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Netzwerk").Select(g => g.Id).FirstAsync());
        var bernd = await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "bernd").Select(b => b.Id).FirstAsync());

        await AktionAsync(anna, seite, "Uebernehmen");
        await AktionAsync(anna, seite, "Weiterleiten", new() { ["gruppeId"] = netzwerk.ToString(), ["bevorzugtId"] = bernd.ToString() });
        await AktionAsync(anna, seite, "Uebernehmen");

        Assert.Empty(await app.MailsVersendenAsync());
        Assert.Contains("inzwischen übernommen", await LetzterHinweisAsync(id, VerlaufArt.Weitergeleitet));
    }

    [Fact]
    public async Task Ohne_Adresse_entfaellt_die_Mail_mit_Begruendung()
    {
        var anna = await AnfangAsync();

        var id = await ErfassenAsync(anna, "Neuer Kollege ohne Adresse", kundeName: null);

        Assert.Empty(await app.MailsVersendenAsync());
        var benachrichtigung = await app.DatenbankAsync(db => db.Benachrichtigungen.SingleAsync(b => b.Verlaufseintrag.AuftragId == id));
        Assert.Equal(BenachrichtigungStatus.Entfallen, benachrichtigung.Status);
        Assert.Equal("Für Ohne Adresse ist keine E-Mail-Adresse hinterlegt.", benachrichtigung.Hinweis);
    }

    [Fact]
    public async Task Ist_der_Mailserver_nicht_erreichbar_gelingt_die_Aktion_und_die_Mail_geht_spaeter_hinaus()
    {
        var anna = await AnfangAsync();
        app.Postfach.Gestoert = true;
        try
        {
            var id = await ErfassenAsync(anna, "Mail kommt später", "Frank Müller");
            Assert.Empty(await app.MailsVersendenAsync());

            var wartend = await EinzigeAsync(id);
            Assert.Equal((BenachrichtigungStatus.Offen, 1), (wartend.Status, wartend.Versuche));
            Assert.Equal("Der Mailserver ist nicht erreichbar.", wartend.Hinweis);
            Assert.True(wartend.NaechsterVersuchAm > DateTime.UtcNow);

            // Vor Ablauf der Wartezeit passiert nichts, auch wenn der Server wieder da ist.
            app.Postfach.Gestoert = false;
            Assert.Empty(await app.MailsVersendenAsync());

            await FaelligStellenAsync(id);
            var mail = Assert.Single(await app.MailsVersendenAsync());
            Assert.Contains("Mail kommt später", mail.Subject);
            var gesendet = await EinzigeAsync(id);
            Assert.Equal((BenachrichtigungStatus.Gesendet, 2), (gesendet.Status, gesendet.Versuche));
            Assert.Null(gesendet.Hinweis);
        }
        finally
        {
            app.Postfach.Gestoert = false;
        }
    }

    [Fact]
    public async Task Nach_sechs_Versuchen_gilt_die_Mail_als_fehlgeschlagen_und_laesst_sich_erneut_anstossen()
    {
        var anna = await AnfangAsync();
        app.Postfach.Gestoert = true;
        try
        {
            var id = await ErfassenAsync(anna, "Mail scheitert endgültig", "Gabi Müller");
            for (var versuch = 1; versuch <= Benachrichtigungsversand.Hoechstversuche; versuch++)
            {
                await FaelligStellenAsync(id);
                await app.MailsVersendenAsync();
            }

            var gescheitert = await EinzigeAsync(id);
            Assert.Equal((BenachrichtigungStatus.Fehlgeschlagen, 6), (gescheitert.Status, gescheitert.Versuche));
            Assert.Contains("fehlgeschlagen", await anna.GetStringAsync("/Verwaltung/Mails"));

            app.Postfach.Gestoert = false;
            await anna.PostFormularAsync("/Verwaltung/Mails", $"/Verwaltung/Mails?handler=Erneut&id={gescheitert.Id}", []);
            Assert.Single(await app.MailsVersendenAsync());
            Assert.Equal(BenachrichtigungStatus.Gesendet, (await EinzigeAsync(id)).Status);
        }
        finally
        {
            app.Postfach.Gestoert = false;
        }
    }

    [Fact]
    public async Task Ist_der_Mailversand_ausgeschaltet_entfallen_die_Mails()
    {
        var anna = await AnfangAsync();
        var id = await ErfassenAsync(anna, "Ohne Mailversand", "Frank Müller");

        using (var scope = app.Services.CreateScope())
        {
            var versand = new Benachrichtigungsversand(
                scope.ServiceProvider.GetRequiredService<AvisDb>(),
                scope.ServiceProvider.GetRequiredService<IOptions<MailOptionen>>(),
                TimeProvider.System,
                NullLogger<Benachrichtigungsversand>.Instance,
                versand: null);
            Assert.Equal(0, await versand.VerarbeitenAsync());
        }

        var benachrichtigung = await EinzigeAsync(id);
        Assert.Equal(BenachrichtigungStatus.Entfallen, benachrichtigung.Status);
        Assert.Equal("Der Mailversand ist ausgeschaltet.", benachrichtigung.Hinweis);
        Assert.DoesNotContain(app.Postfach.Mails, m => m.Subject!.Contains("Ohne Mailversand"));
    }

    [Fact]
    public async Task Die_Verwaltung_zeigt_die_Benachrichtigungen()
    {
        var anna = await AnfangAsync();
        await ErfassenAsync(anna, "Erscheint in der Mailliste", "Frank Müller");
        await app.MailsVersendenAsync();

        var seite = await anna.GetStringAsync("/Verwaltung/Mails");

        Assert.Contains("frank.mueller@example.org", seite);
        Assert.Contains("gesendet", seite);
        var bernd = await app.AnmeldenAlsAsync("bernd");
        Assert.Contains("Kein Zugriff", await bernd.GetStringAsync("/Verwaltung/Mails"));
    }

    [Fact]
    public async Task Der_Versand_ueber_SMTP_liefert_die_Mail_beim_Server_ab()
    {
        using var server = new SmtpAttrappe();
        var versand = new SmtpMailversand(Options.Create(new MailOptionen
        {
            Art = MailOptionen.Smtp,
            Server = "127.0.0.1",
            Port = server.Port,
            Verschluesselung = "Keine",
        }));

        await versand.SendenAsync(Beispielmail(), CancellationToken.None);

        var empfangen = await server.Empfangen;
        Assert.Equal("<it-hotline@avis.test>", empfangen.Von);
        Assert.Equal("<kunde@avis.test>", empfangen.An);
        var mail = await MimeMessage.LoadAsync(new MemoryStream(Encoding.UTF8.GetBytes(empfangen.Daten)));
        Assert.Equal("[AVIS-7] Erfasst: Prüfung über SMTP", mail.Subject);
        Assert.Contains("Größe und Umlaute bleiben erhalten: „so“.", mail.TextBody);
    }

    [Fact]
    public async Task Die_Ablage_im_Ordner_schreibt_jede_Mail_als_Datei()
    {
        var ordner = Directory.CreateTempSubdirectory("avis-mails-").FullName;
        try
        {
            var versand = new OrdnerMailversand(
                Options.Create(new MailOptionen { Art = MailOptionen.Ordner, Ablage = "Ablage" }),
                new Umgebung(ordner),
                TimeProvider.System);

            await versand.SendenAsync(Beispielmail(), CancellationToken.None);

            var datei = Assert.Single(Directory.GetFiles(Path.Combine(ordner, "Ablage"), "*.eml"));
            Assert.Equal("[AVIS-7] Erfasst: Prüfung über SMTP", (await MimeMessage.LoadAsync(datei)).Subject);
        }
        finally
        {
            Directory.Delete(ordner, recursive: true);
        }
    }

    private static MimeMessage Beispielmail()
    {
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress("IT-Hotline", "it-hotline@avis.test"));
        mail.To.Add(new MailboxAddress("Kunde", "kunde@avis.test"));
        mail.Subject = "[AVIS-7] Erfasst: Prüfung über SMTP";
        mail.Body = new TextPart("plain") { Text = "Größe und Umlaute bleiben erhalten: „so“.\n" };
        return mail;
    }

    /// <summary>Meldet Anna an und räumt weg, was frühere Tests an Mails hinterlassen haben.</summary>
    private async Task<HttpClient> AnfangAsync()
    {
        app.Postfach.Gestoert = false;
        await app.MailsVersendenAsync();
        return await app.AnmeldenAlsAsync("anna");
    }

    /// <summary>Erfasst für einen Kunden aus den Testdaten oder, ohne Namen, für einen neuen Kunden ohne Adresse.</summary>
    private async Task<int> ErfassenAsync(HttpClient client, string betreff, string? kundeName)
    {
        var felder = new Dictionary<string, string>
        {
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync())).ToString(),
        };
        if (kundeName is null)
            felder["Eingabe.KundeName"] = "Ohne Adresse";
        else
            felder["kundeId"] = (await app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == kundeName).Select(k => k.Id).FirstAsync())).ToString();

        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", felder);
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private async Task<HttpResponseMessage> AktionAsync(HttpClient client, string seite, string aktion, Dictionary<string, string>? felder = null)
    {
        felder ??= [];
        felder["version"] = (await LadenAsync(int.Parse(seite.Split('/')[^1]))).Version.ToString();
        return await client.PostFormularAsync(seite, $"{seite}?handler={aktion}", felder);
    }

    private Task<Auftrag> LadenAsync(int id) =>
        app.DatenbankAsync(db => db.Auftraege.AsNoTracking().FirstAsync(a => a.Id == id));

    private Task<Benachrichtigung> EinzigeAsync(int auftragId) =>
        app.DatenbankAsync(db => db.Benachrichtigungen.AsNoTracking().SingleAsync(b => b.Verlaufseintrag.AuftragId == auftragId));

    private Task<string?> LetzterHinweisAsync(int auftragId, VerlaufArt art) =>
        app.DatenbankAsync(db => db.Benachrichtigungen
            .Where(b => b.Verlaufseintrag.AuftragId == auftragId && b.Verlaufseintrag.Art == art)
            .OrderByDescending(b => b.Id)
            .Select(b => b.Hinweis)
            .FirstAsync());

    private Task<int> FaelligStellenAsync(int auftragId) =>
        app.DatenbankAsync(db => db.Benachrichtigungen
            .Where(b => b.Verlaufseintrag.AuftragId == auftragId)
            .ExecuteUpdateAsync(b => b.SetProperty(x => x.NaechsterVersuchAm, DateTime.UtcNow.AddMinutes(-1))));

    private sealed class Umgebung(string ordner) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = ordner;
        public string WebRootPath { get; set; } = ordner;
        public string ApplicationName { get; set; } = "Avis.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>Ein Mailserver, der genau eine Mail annimmt und festhält, was er bekommen hat.</summary>
    private sealed class SmtpAttrappe : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        public SmtpAttrappe()
        {
            _listener.Start();
            Empfangen = Task.Run(AnnehmenAsync);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public Task<(string Von, string An, string Daten)> Empfangen { get; }

        private async Task<(string, string, string)> AnnehmenAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var strom = client.GetStream();
            using var leser = new StreamReader(strom, Encoding.UTF8);
            await using var schreiber = new StreamWriter(strom, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

            string von = "", an = "";
            var daten = new StringBuilder();
            await schreiber.WriteLineAsync("220 attrappe ESMTP");
            while (await leser.ReadLineAsync() is { } zeile)
            {
                var befehl = zeile.ToUpperInvariant();
                if (befehl.StartsWith("EHLO") || befehl.StartsWith("HELO"))
                {
                    await schreiber.WriteLineAsync("250-attrappe");
                    await schreiber.WriteLineAsync("250 8BITMIME");
                }
                else if (befehl.StartsWith("MAIL FROM:"))
                {
                    von = zeile[10..].Split(' ')[0];
                    await schreiber.WriteLineAsync("250 OK");
                }
                else if (befehl.StartsWith("RCPT TO:"))
                {
                    an = zeile[8..].Split(' ')[0];
                    await schreiber.WriteLineAsync("250 OK");
                }
                else if (befehl == "DATA")
                {
                    await schreiber.WriteLineAsync("354 Bitte senden");
                    while (await leser.ReadLineAsync() is { } inhalt && inhalt != ".")
                        daten.Append(inhalt.StartsWith("..") ? inhalt[1..] : inhalt).Append("\r\n");
                    await schreiber.WriteLineAsync("250 OK");
                }
                else if (befehl == "QUIT")
                {
                    await schreiber.WriteLineAsync("221 Auf Wiedersehen");
                    break;
                }
                else
                {
                    await schreiber.WriteLineAsync("250 OK");
                }
            }
            return (von, an, daten.ToString());
        }

        public void Dispose() => _listener.Stop();
    }
}
