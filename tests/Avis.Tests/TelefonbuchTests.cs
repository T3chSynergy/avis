using Avis.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Tests;

/// <summary>
/// Kunden und Bearbeiter führen zu ihrer Seite in einem Telefonbuch des
/// Hauses, wenn dessen Adresse eingestellt ist. Dort endet die Adresse mit der
/// Kennung ohne die Domäne.
/// </summary>
public class TelefonbuchTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    private const string Adresse = "https://telefonbuch.example.org/person/";

    [Theory]
    [InlineData("beispiel\\musteruser", "musteruser")]
    [InlineData("musteruser", "musteruser")]
    [InlineData("musteruser@example.org", "musteruser")]
    [InlineData(" a\\b\\hanna ", "hanna")]
    [InlineData("avis:e-mail-eingang", null)]
    [InlineData("beispiel\\", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Die_Kennung_ist_der_Anmeldename_ohne_die_Domaene(string? anmeldename, string? erwartet) =>
        Assert.Equal(erwartet, Telefonbuch.Kennung(anmeldename));

    [Theory]
    [InlineData("https://telefonbuch.example.org/person/", "beispiel\\musteruser", "https://telefonbuch.example.org/person/musteruser")]
    [InlineData("https://telefonbuch.example.org/suche?kennung={kennung}&ansicht=voll", "beispiel\\musteruser", "https://telefonbuch.example.org/suche?kennung=musteruser&ansicht=voll")]
    [InlineData("https://telefonbuch.example.org/person/{KENNUNG}", "müller ä", "https://telefonbuch.example.org/person/m%C3%BCller%20%C3%A4")]
    [InlineData("https://telefonbuch.example.org/person/", null, null)]
    [InlineData("telefonbuch.example.org/person/", "musteruser", null)]
    [InlineData("javascript:alert(1)//", "musteruser", null)]
    [InlineData("", "musteruser", null)]
    [InlineData(null, "musteruser", null)]
    public void Die_Adresse_entsteht_aus_Einstellung_und_Kennung(string? adresse, string? anmeldename, string? erwartet)
    {
        var telefonbuch = new Telefonbuch(Options.Create(new TelefonbuchOptionen { Adresse = adresse }));

        Assert.Equal(erwartet, telefonbuch.AdresseFuer(anmeldename));
    }

    [Fact]
    public async Task Kunde_und_Bearbeiter_fuehren_ins_Telefonbuch()
    {
        using var mit = app.WithWebHostBuilder(b => b.UseSetting("Telefonbuch:Adresse", Adresse).UseSetting("Telefonbuch:Bezeichnung", "Personensuche"));
        var anna = await AnmeldenAsync(mit, "anna");
        // Hanna Verzeichnis meldet sich mit "avis\hanna" an, Eva Test hat kein Konto.
        var hanna = await KundeIdAsync("Hanna Verzeichnis");
        var eva = await KundeIdAsync("Eva Test");

        var seite = await anna.GetStringAsync($"/Kunden/{hanna}");
        Assert.Contains($"<a href=\"{Adresse}hanna\" rel=\"noopener noreferrer\">Personensuche</a>", seite);
        Assert.DoesNotContain("telefonbuch.example.org", await anna.GetStringAsync($"/Kunden/{eva}"));

        // Am Auftrag: beim Kunden, beim bevorzugten Bearbeiter und, nach dem Übernehmen, beim Bearbeiter.
        var id = await ErfassenAsync(anna, hanna, bevorzugt: "bernd");
        var auftrag = await anna.GetStringAsync($"/Auftraege/{id}");
        Assert.Contains($"<a href=\"{Adresse}hanna\" rel=\"noopener noreferrer\">Personensuche</a>", auftrag);
        Assert.Contains($"bevorzugt an <a href=\"{Adresse}bernd\" rel=\"noopener noreferrer\" title=\"Personensuche\"><strong>Bernd Muster</strong></a>", auftrag);
        var clara = await AnmeldenAsync(mit, "clara");
        await clara.UebernehmenAsync(app, id);
        Assert.Contains($"bei <a href=\"{Adresse}clara\" rel=\"noopener noreferrer\" title=\"Personensuche\"><strong>Clara Probe</strong></a>", await anna.GetStringAsync($"/Auftraege/{id}"));
    }

    [Fact]
    public async Task Ohne_eingestellte_Adresse_gibt_es_keinen_Verweis()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var hanna = await KundeIdAsync("Hanna Verzeichnis");
        var id = await ErfassenAsync(anna, hanna, bevorzugt: "bernd");

        Assert.DoesNotContain(">Telefonbuch</a>", await anna.GetStringAsync($"/Kunden/{hanna}"));
        var auftrag = await anna.GetStringAsync($"/Auftraege/{id}");
        Assert.DoesNotContain(">Telefonbuch</a>", auftrag);
        Assert.Contains("bevorzugt an <strong>Bernd Muster</strong>", auftrag);
    }

    private static async Task<HttpClient> AnmeldenAsync(WebApplicationFactory<Program> anwendung, string anmeldename)
    {
        var client = anwendung.CreateClient();
        await client.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = anmeldename });
        return client;
    }

    private async Task<int> ErfassenAsync(HttpClient client, int kunde, string bevorzugt)
    {
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["kundeId"] = kunde.ToString(),
            ["Eingabe.Felder.Betreff"] = "Für einen Test",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync())).ToString(),
            ["Eingabe.BevorzugtId"] = (await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == bevorzugt).Select(b => b.Id).FirstAsync())).ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private Task<int> KundeIdAsync(string name) =>
        app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == name).Select(k => k.Id).FirstAsync());
}
