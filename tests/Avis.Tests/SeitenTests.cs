using System.Net;
using System.Text.RegularExpressions;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avis.Tests;

/// <summary>Startet die Anwendung mit einer eigenen, leeren Datenbank und den Testdaten.</summary>
public class AvisAnwendung : WebApplicationFactory<Program>
{
    private readonly string _ordner = Directory.CreateTempSubdirectory("avis-test-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Avis", $"Data Source={Path.Combine(_ordner, "avis.db")};Pooling=False");
        builder.UseSetting("Anmeldung:Art", "Entwicklung");
        builder.UseSetting("Testdaten:Anlegen", "true");
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

    public async Task<T> DatenbankAsync<T>(Func<AvisDb, Task<T>> abfrage)
    {
        using var scope = Services.CreateScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AvisDb>());
    }
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
    public async Task Seiten_fuer_Bearbeiter_antworten(string pfad)
    {
        var client = await app.AnmeldenAlsAsync("anna");

        var antwort = await client.GetAsync(pfad);

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
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
            ["Eingabe.Betreff"] = "Kein Netz am Arbeitsplatz",
            ["Eingabe.Beschreibung"] = "Die Netzwerkdose in Raum 2.11 ist tot.",
            ["Eingabe.Eingang"] = "Telefon",
            ["Eingabe.Dringlichkeit"] = "Hoch",
            ["Eingabe.Wichtigkeit"] = "Mittel",
            ["Eingabe.Termin"] = "2026-11-20",
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
            ["Eingabe.Betreff"] = "Bildschirm flackert",
            ["Eingabe.Beschreibung"] = "Seit gestern.",
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
