using System.Net;
using System.Text.RegularExpressions;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
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

public class SeitenTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Fact]
    public async Task Ohne_Anmeldung_geht_es_zur_Anmeldeseite()
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var antwort = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.StartsWith("/Anmeldung", antwort.Headers.Location!.PathAndQuery);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Gruppen")]
    [InlineData("/Gruppen/Meine")]
    [InlineData("/Auftraege")]
    [InlineData("/Auftraege?status=abgeschlossen")]
    [InlineData("/Auftraege/Neu")]
    [InlineData("/Auftraege/1")]
    [InlineData("/Auftraege/1/Bearbeiten")]
    public async Task Seiten_fuer_Bearbeiter_antworten(string pfad)
    {
        var client = await app.AnmeldenAlsAsync("anna");

        var antwort = await client.GetAsync(pfad);

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
    }

    [Fact]
    public async Task Der_Kopf_zeigt_die_wichtigste_Aktion_den_aktuellen_Bereich_und_was_ansteht()
    {
        var clara = await app.AnmeldenAlsAsync("clara");

        var gruppen = await clara.GetStringAsync("/Gruppen");

        Assert.Contains("class=\"neu\" href=\"/Auftraege/Neu\"", gruppen);
        Assert.Contains("<a aria-current=\"page\" href=\"/Gruppen\">Gruppen", gruppen);
        Assert.DoesNotContain("<a aria-current=\"page\" href=\"/\">Meine Aufträge", gruppen);
        // Clara bearbeitet aus den Testdaten den Auftrag zum Monitor.
        Assert.Matches("Meine Aufträge <span class=\"zahl voll\">[1-9]\\d*</span>", gruppen);
        // Die Verwaltung sieht nur, wer Administrator ist.
        Assert.DoesNotContain("/Verwaltung/", gruppen);
        var anna = await app.AnmeldenAlsAsync("anna");
        Assert.Contains("href=\"/Verwaltung/Gruppen\">Verwaltung</a>", await anna.GetStringAsync("/Gruppen"));
    }

    [Fact]
    public async Task Kunden_sehen_im_Kopf_nur_ihre_Auftraege_und_die_Aktion_fuer_einen_neuen()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");

        var seite = await dieter.GetStringAsync("/Meine");

        Assert.Contains("class=\"neu\" href=\"/Meine/Neu\"", seite);
        Assert.Contains("<a aria-current=\"page\" href=\"/Meine\">Meine Aufträge</a>", seite);
        Assert.DoesNotContain("schnellsuche", seite);
        Assert.DoesNotContain("href=\"/Gruppen\"", seite);
        Assert.DoesNotContain("href=\"/Kunden\"", seite);
    }

    [Fact]
    public async Task Ein_Kunde_kommt_nicht_auf_die_Seiten_der_Bearbeiter()
    {
        var client = await app.AnmeldenAlsAsync("dieter");

        var antwort = await client.GetAsync("/Auftraege");

        Assert.Contains("Kein Zugriff", await antwort.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ein_unbekannter_Auftrag_wird_nicht_gefunden()
    {
        var client = await app.AnmeldenAlsAsync("anna");

        var antwort = await client.GetAsync("/Auftraege/99999");

        Assert.Equal(HttpStatusCode.NotFound, antwort.StatusCode);
    }

    [Fact]
    public async Task Ein_Auftrag_durchlaeuft_Erfassen_Uebernehmen_Weiterleiten_und_Abschliessen()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var hotline = await app.DatenbankAsync(db => db.Gruppen.FirstAsync(g => g.IstEingangsgruppe));
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.FirstAsync(g => g.Name == "Netzwerk"));

        // Anna erfasst am Telefon.
        var erfasst = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Frieda Neu",
            ["Eingabe.KundeEmail"] = "Frieda.Neu@example.org",
            ["Eingabe.Felder.Betreff"] = "Kein Netz am Arbeitsplatz",
            ["Eingabe.Felder.Beschreibung"] = "Die Netzwerkdose in Raum 2.11 ist tot.",
            ["Eingabe.Eingang"] = "Telefon",
            ["Eingabe.Felder.Dringlichkeit"] = "Hoch",
            ["Eingabe.Felder.Wichtigkeit"] = "Mittel",
            ["Eingabe.Felder.Termin"] = "2026-11-20",
            ["Eingabe.GruppeId"] = hotline.Id.ToString(),
        });
        var seite = erfasst.RequestMessage!.RequestUri!.AbsolutePath;
        var id = int.Parse(seite.Split('/')[^1]);
        Assert.Contains("Kein Netz am Arbeitsplatz", await erfasst.Content.ReadAsStringAsync());

        var auftrag = await LadenAsync(id);
        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.Equal(2, auftrag.Prioritaet);
        Assert.Equal(new DateOnly(2026, 11, 20), auftrag.Termin);
        Assert.Equal("frieda.neu@example.org", auftrag.Kunde.Email);

        // Anna übernimmt und leitet an das Netzwerk weiter, bevorzugt an Bernd.
        await AktionAsync(anna, seite, "Uebernehmen", auftrag.Version);
        auftrag = await LadenAsync(id);
        Assert.Equal("Anna Beispiel", auftrag.Bearbeiter!.Name);

        var berndId = await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "bernd").Select(b => b.Id).FirstAsync());
        await AktionAsync(anna, seite, "Weiterleiten", auftrag.Version, new()
        {
            ["gruppeId"] = netzwerk.Id.ToString(),
            ["bevorzugtId"] = berndId.ToString(),
            ["text"] = "Dose patchen.",
        });
        auftrag = await LadenAsync(id);
        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.Equal("Netzwerk", auftrag.Gruppe.Name);

        // Bernd sieht ihn in seiner Gruppe und auf seiner Startseite.
        Assert.Contains("Kein Netz am Arbeitsplatz", await bernd.GetStringAsync("/Gruppen"));
        Assert.Contains("Kein Netz am Arbeitsplatz", await bernd.GetStringAsync("/"));

        // Anna kann nicht abschließen, Bernd übernimmt und schließt ab.
        await AktionAsync(anna, seite, "Abschliessen", auftrag.Version, new() { ["text"] = "Erledigt." });
        Assert.Equal(AuftragStatus.Offen, (await LadenAsync(id)).Status);

        await AktionAsync(bernd, seite, "Uebernehmen", auftrag.Version);
        auftrag = await LadenAsync(id);
        await AktionAsync(bernd, seite, "InternerKommentar", auftrag.Version, new() { ["text"] = "Port 17 war deaktiviert." });
        auftrag = await LadenAsync(id);
        await AktionAsync(bernd, seite, "Abschliessen", auftrag.Version, new() { ["text"] = "Die Dose funktioniert wieder." });

        auftrag = await LadenAsync(id);
        Assert.Equal(AuftragStatus.Abgeschlossen, auftrag.Status);
        Assert.Equal(
            [
                VerlaufArt.Erfasst, VerlaufArt.Uebernommen, VerlaufArt.Weitergeleitet,
                VerlaufArt.Uebernommen, VerlaufArt.InternerKommentar, VerlaufArt.Abgeschlossen,
            ],
            auftrag.Verlauf.OrderBy(v => v.Id).Select(v => v.Art));
    }

    [Fact]
    public async Task Uebernehmen_auf_veraltetem_Stand_wird_abgelehnt()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var erfasst = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Gustav Alt",
            ["Eingabe.Felder.Betreff"] = "Bildschirm flackert",
            ["Eingabe.Felder.Beschreibung"] = "Seit gestern.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.FirstAsync(g => g.IstEingangsgruppe))).Id.ToString(),
        });
        var seite = erfasst.RequestMessage!.RequestUri!.AbsolutePath;
        var id = int.Parse(seite.Split('/')[^1]);
        var alteVersion = (await LadenAsync(id)).Version;

        await AktionAsync(anna, seite, "Uebernehmen", alteVersion);
        var antwort = await AktionAsync(bernd, seite, "Uebernehmen", alteVersion);

        Assert.Contains("inzwischen von jemand anderem geändert", await antwort.Content.ReadAsStringAsync());
        Assert.Equal("Anna Beispiel", (await LadenAsync(id)).Bearbeiter!.Name);
    }

    [Fact]
    public async Task Angaben_eines_Auftrags_lassen_sich_aendern_und_stehen_im_Verlauf()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Maus defekt");
        var seite = $"/Auftraege/{id}/Bearbeiten";
        var formular = await anna.GetStringAsync(seite);
        Assert.Contains("Maus defekt", formular);

        var antwort = await anna.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = (await LadenAsync(id)).Version.ToString(),
            ["Felder.Betreff"] = "Maus und Tastatur defekt",
            ["Felder.Beschreibung"] = "Für einen Test.",
            ["Felder.Dringlichkeit"] = "Hoch",
            ["Felder.Wichtigkeit"] = "Hoch",
            ["Felder.Geraet"] = "PC-4711",
        });

        var text = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Die Änderungen wurden gespeichert.", text);
        Assert.Contains("Betreff: Maus defekt → Maus und Tastatur defekt", text);

        var auftrag = await LadenAsync(id);
        Assert.Equal("Maus und Tastatur defekt", auftrag.Betreff);
        Assert.Equal("PC-4711", auftrag.Geraet);
        Assert.Equal(1, auftrag.Prioritaet);
        Assert.Equal(VerlaufArt.Geaendert, auftrag.Verlauf.OrderBy(v => v.Id).Last().Art);
    }

    [Fact]
    public async Task Ein_unveraendert_gespeichertes_Formular_aendert_nichts()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Zeile eins");
        var seite = $"/Auftraege/{id}/Bearbeiten";
        var vorher = await LadenAsync(id);

        var antwort = await anna.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = vorher.Version.ToString(),
            ["Felder.Betreff"] = vorher.Betreff,
            ["Felder.Beschreibung"] = vorher.Beschreibung,
            ["Felder.Dringlichkeit"] = vorher.Dringlichkeit.ToString(),
            ["Felder.Wichtigkeit"] = vorher.Wichtigkeit.ToString(),
        });

        Assert.Contains("Es wurde nichts geändert.", await antwort.Content.ReadAsStringAsync());
        var danach = await LadenAsync(id);
        Assert.Equal(vorher.Version, danach.Version);
        Assert.Equal(vorher.Verlauf.Count, danach.Verlauf.Count);
    }

    [Fact]
    public async Task Aendern_auf_veraltetem_Stand_ueberschreibt_nichts_und_behaelt_die_Eingaben()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var id = await ErfassenAsync(anna, "Lüfter laut");
        var seite = $"/Auftraege/{id}/Bearbeiten";
        var alteVersion = (await LadenAsync(id)).Version;

        // Bernd ändert die Dringlichkeit, während Anna ihr Formular noch offen hat.
        await bernd.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = alteVersion.ToString(),
            ["Felder.Betreff"] = "Lüfter laut",
            ["Felder.Beschreibung"] = "Für einen Test.",
            ["Felder.Dringlichkeit"] = "Hoch",
            ["Felder.Wichtigkeit"] = "Mittel",
        });
        var antwort = await anna.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = alteVersion.ToString(),
            ["Felder.Betreff"] = "Lüfter sehr laut",
            ["Felder.Beschreibung"] = "Für einen Test.",
            ["Felder.Dringlichkeit"] = "Mittel",
            ["Felder.Wichtigkeit"] = "Mittel",
        });

        var text = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("inzwischen von jemand anderem geändert", text);
        Assert.Contains("Lüfter sehr laut", text);
        var auftrag = await LadenAsync(id);
        Assert.Equal("Lüfter laut", auftrag.Betreff);
        Assert.Equal(Stufe.Hoch, auftrag.Dringlichkeit);
        Assert.Contains($"name=\"Version\" value=\"{auftrag.Version}\"", text);
    }

    [Fact]
    public async Task Ein_Kommentar_macht_ein_offenes_Bearbeiten_Formular_nicht_ungueltig()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Headset rauscht");
        var seite = $"/Auftraege/{id}/Bearbeiten";
        var version = (await LadenAsync(id)).Version;

        await AktionAsync(anna, $"/Auftraege/{id}", "InternerKommentar", version, new() { ["text"] = "Ersatz liegt im Lager." });
        var antwort = await anna.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = version.ToString(),
            ["Felder.Betreff"] = "Headset rauscht stark",
            ["Felder.Beschreibung"] = "Für einen Test.",
        });

        Assert.Contains("Die Änderungen wurden gespeichert.", await antwort.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ein_abgeschlossener_Auftrag_laesst_sich_nicht_bearbeiten()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Kabel fehlt");
        var auftragsseite = $"/Auftraege/{id}";
        await AktionAsync(anna, auftragsseite, "Uebernehmen", (await LadenAsync(id)).Version);
        await AktionAsync(anna, auftragsseite, "Abschliessen", (await LadenAsync(id)).Version, new() { ["text"] = "Kabel geliefert." });

        var formular = await anna.GetStringAsync($"{auftragsseite}/Bearbeiten");
        var gespeichert = await anna.PostFormularAsync("/Auftraege/Neu", $"{auftragsseite}/Bearbeiten", new()
        {
            ["Version"] = (await LadenAsync(id)).Version.ToString(),
            ["Felder.Betreff"] = "Kabel fehlt immer noch",
            ["Felder.Beschreibung"] = "Für einen Test.",
        });

        Assert.Contains("lässt sich nicht bearbeiten", formular);
        Assert.Contains("Der Auftrag ist abgeschlossen.", await gespeichert.Content.ReadAsStringAsync());
        Assert.Equal("Kabel fehlt", (await LadenAsync(id)).Betreff);
    }

    [Fact]
    public async Task Eine_deaktivierte_Kategorie_bleibt_beim_Bearbeiten_erhalten_steht_aber_sonst_nicht_zur_Auswahl()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var (alt, andere) = await app.DatenbankAsync(async db =>
        {
            var a = new Kategorie { Name = "Altverfahren" };
            var b = new Kategorie { Name = "Stillgelegt", Aktiv = false };
            db.Kategorien.AddRange(a, b);
            await db.SaveChangesAsync();
            return (a.Id, b.Id);
        });
        var erfasst = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = "Altes Verfahren hakt",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.Felder.KategorieId"] = alt.ToString(),
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.FirstAsync(g => g.IstEingangsgruppe))).Id.ToString(),
        });
        var id = int.Parse(erfasst.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        await app.DatenbankAsync(db => db.Kategorien.Where(k => k.Id == alt).ExecuteUpdateAsync(k => k.SetProperty(x => x.Aktiv, false)));
        var seite = $"/Auftraege/{id}/Bearbeiten";

        // Die eigene, inzwischen deaktivierte Kategorie steht noch zur Auswahl und bleibt.
        Assert.Contains("Altverfahren", await anna.GetStringAsync(seite));
        await anna.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = (await LadenAsync(id)).Version.ToString(),
            ["Felder.Betreff"] = "Altes Verfahren hakt wieder",
            ["Felder.Beschreibung"] = "Für einen Test.",
            ["Felder.KategorieId"] = alt.ToString(),
        });
        var auftrag = await LadenAsync(id);
        Assert.Equal("Altes Verfahren hakt wieder", auftrag.Betreff);
        Assert.Equal(alt, auftrag.KategorieId);

        // Eine andere deaktivierte Kategorie lässt sich nicht wählen.
        var antwort = await anna.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = auftrag.Version.ToString(),
            ["Felder.Betreff"] = "Altes Verfahren hakt wieder",
            ["Felder.Beschreibung"] = "Für einen Test.",
            ["Felder.KategorieId"] = andere.ToString(),
        });
        Assert.Contains("Diese Kategorie steht nicht zur Auswahl.", await antwort.Content.ReadAsStringAsync());
        Assert.Equal(alt, (await LadenAsync(id)).KategorieId);
    }

    private async Task<int> ErfassenAsync(HttpClient client, string betreff)
    {
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.FirstAsync(g => g.IstEingangsgruppe))).Id.ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    [Fact]
    public async Task Bearbeiter_tragen_sich_selbst_in_Gruppen_ein_und_aus()
    {
        var clara = await app.AnmeldenAlsAsync("clara");
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.FirstAsync(g => g.Name == "Netzwerk"));

        await clara.PostFormularAsync("/Gruppen/Meine", $"/Gruppen/Meine?handler=Beitreten&id={netzwerk.Id}", []);
        Assert.True(await IstMitgliedAsync());

        await clara.PostFormularAsync("/Gruppen/Meine", $"/Gruppen/Meine?handler=Austreten&id={netzwerk.Id}", []);
        Assert.False(await IstMitgliedAsync());

        Task<bool> IstMitgliedAsync() => app.DatenbankAsync(db =>
            db.Gruppen.AnyAsync(g => g.Id == netzwerk.Id && g.Mitglieder.Any(m => m.Anmeldename == "clara")));
    }

    private static Task<HttpResponseMessage> AktionAsync(
        HttpClient client, string seite, string aktion, int version, Dictionary<string, string>? felder = null)
    {
        felder ??= [];
        felder["version"] = version.ToString();
        return client.PostFormularAsync(seite, $"{seite}?handler={aktion}", felder);
    }

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Kunde)
        .Include(a => a.Gruppe)
        .Include(a => a.Bearbeiter)
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
