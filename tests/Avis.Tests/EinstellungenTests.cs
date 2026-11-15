using System.Net;
using System.Text.RegularExpressions;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avis.Tests;

/// <summary>Die persönlichen Einstellungen und die helle und dunkle Darstellung.</summary>
public partial class EinstellungenTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Theory]
    [InlineData("bernd")]
    [InlineData("dieter")]
    public async Task Bearbeiter_und_Kunden_erreichen_die_Einstellungen_ueber_das_Menue_unter_ihrem_Namen(string anmeldename)
    {
        var client = await app.AnmeldenAlsAsync(anmeldename);

        var start = await client.GetStringAsync("/");
        Assert.Matches("<details class=\"konto\">[\\s\\S]*<a [^>]*href=\"/Einstellungen\"[^>]*>Einstellungen</a>[\\s\\S]*</details>", start);

        var seite = await client.GetStringAsync("/Einstellungen");
        Assert.Contains("<h1>Einstellungen</h1>", seite);
        // Voreingestellt ist "Wie das Gerät".
        Assert.Matches("<input [^>]*value=\"WieGeraet\"[^>]*checked", seite);
        Assert.DoesNotMatch("<input [^>]*value=\"Dunkel\"[^>]*checked", seite);
    }

    [Fact]
    public async Task Die_gewaehlte_Darstellung_gilt_fuer_das_Konto_auf_jeder_Seite_und_nur_fuer_dieses()
    {
        var clara = await app.AnmeldenAlsAsync("clara");
        Assert.Contains("<html lang=\"de\" data-darstellung=\"geraet\">", await clara.GetStringAsync("/"));

        var antwort = await clara.PostFormularAsync("/Einstellungen", "/Einstellungen", new() { ["darstellung"] = "Dunkel" });

        var seite = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Die Einstellungen wurden gespeichert.", seite);
        Assert.Contains("<html lang=\"de\" data-darstellung=\"dunkel\">", seite);
        Assert.Matches("<input [^>]*value=\"Dunkel\"[^>]*checked", seite);
        Assert.Contains("data-darstellung=\"dunkel\"", await clara.GetStringAsync("/Auftraege"));

        // Die Wahl hängt am Konto, nicht am Browser: Eine neue Anmeldung findet sie wieder vor.
        var claraWoanders = await app.AnmeldenAlsAsync("clara");
        Assert.Contains("data-darstellung=\"dunkel\"", await claraWoanders.GetStringAsync("/"));
        Assert.Equal(Darstellung.Dunkel, await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "clara").Select(b => b.Darstellung).SingleAsync()));

        // Andere Konten bleiben, wie sie waren.
        var bernd = await app.AnmeldenAlsAsync("bernd");
        Assert.Contains("data-darstellung=\"geraet\"", await bernd.GetStringAsync("/"));

        await clara.PostFormularAsync("/Einstellungen", "/Einstellungen", new() { ["darstellung"] = "Hell" });
        Assert.Contains("data-darstellung=\"hell\"", await clara.GetStringAsync("/"));
        await clara.PostFormularAsync("/Einstellungen", "/Einstellungen", new() { ["darstellung"] = "WieGeraet" });
        Assert.Contains("data-darstellung=\"geraet\"", await clara.GetStringAsync("/"));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("Bunt")]
    public async Task Eine_unbekannte_Darstellung_wird_abgelehnt(string wert)
    {
        var bernd = await app.AnmeldenAlsAsync("bernd");

        var antwort = await bernd.PostFormularAsync("/Einstellungen", "/Einstellungen", new() { ["darstellung"] = wert });

        Assert.Equal(HttpStatusCode.BadRequest, antwort.StatusCode);
        Assert.Equal(Darstellung.WieGeraet, await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "bernd").Select(b => b.Darstellung).SingleAsync()));
    }

    [Fact]
    public async Task Ohne_Anmeldung_gilt_die_Einstellung_des_Geraets_und_es_gibt_keine_Einstellungen()
    {
        var fremd = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Contains("data-darstellung=\"geraet\"", await fremd.GetStringAsync("/Anmeldung"));
        var antwort = await fremd.GetAsync("/Einstellungen");
        Assert.Equal(HttpStatusCode.Redirect, antwort.StatusCode);
        Assert.StartsWith("/Anmeldung", antwort.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public void Die_dunkle_Darstellung_steht_im_Stylesheet_zweimal_gleich_und_kennt_jede_Farbe_der_hellen()
    {
        var pfad = Path.Combine(app.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath, "css", "avis.css");
        var css = File.ReadAllText(pfad);

        // Einmal für "wie das Gerät", einmal für die ausdrückliche Wahl. CSS kann das nicht zusammenfassen;
        // wer eine Farbe nur an einer Stelle ändert, soll es hier merken.
        var hell = Variablen(Block(css, ":root {"));
        var wieGeraet = Variablen(Block(css, ":root:not([data-darstellung=\"hell\"]) {"));
        var gewaehlt = Variablen(Block(css, ":root[data-darstellung=\"dunkel\"] {"));

        Assert.NotEmpty(hell);
        Assert.Equal(wieGeraet, gewaehlt);
        Assert.Equal(hell.Keys.Order(), gewaehlt.Keys.Order());

        // Außerhalb der drei Blöcke stehen keine festen Farben mehr, nur Schatten und der weiße Grund von Bildern.
        var rest = css[(css.IndexOf(":root[data-darstellung=\"dunkel\"] {", StringComparison.Ordinal) + Block(css, ":root[data-darstellung=\"dunkel\"] {").Length)..];
        var fest = Farbe().Matches(rest).Select(m => m.Value).Where(f => f != "#fff").ToList();
        Assert.Empty(fest);
    }

    private static string Block(string css, string anfang)
    {
        var start = css.IndexOf(anfang, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Der Block \"{anfang}\" fehlt im Stylesheet.");
        return css[start..(css.IndexOf('}', start) + 1)];
    }

    private static Dictionary<string, string> Variablen(string block) =>
        Variable().Matches(block).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());

    [GeneratedRegex(@"--([a-z0-9-]+):\s*([^;]+);")]
    private static partial Regex Variable();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b")]
    private static partial Regex Farbe();
}
