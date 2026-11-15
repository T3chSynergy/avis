using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avis.Tests;

/// <summary>
/// Die Liste der Benutzer in der Verwaltung. Aus den Testdaten kommen Anna
/// (Administratorin), Bernd, Clara und Finn (Bearbeiter) und Dieter (ohne
/// Rolle). Jeder Test hat seine eigene Datenbank.
/// </summary>
public class BenutzerlisteTests
{
    private const string Liste = "/Verwaltung/Benutzer";

    [Fact]
    public async Task Die_Liste_zeigt_zuerst_nur_wer_eine_Rolle_hat()
    {
        using var app = new AvisAnwendung();
        var anna = await app.AnmeldenAlsAsync("anna");

        var seite = await anna.GetStringAsync(Liste);

        // Dieter hat sich nur als Kunde angemeldet und steht nicht da.
        Assert.Equal(["Anna Beispiel", "Bernd Muster", "Clara Probe", "Finn Verzeichnis"], Namen(seite));
        // Die Auswahl nennt, wie viele Konten es je gibt.
        Assert.Contains("Mit Rolle (4)", seite);
        Assert.Contains("Administratoren (1)", seite);
        Assert.Contains("Ohne Rolle (1)", seite);
        Assert.Contains("Alle (5)", seite);
        // Wer aus dem Verzeichnis abgeglichen wird, ist markiert.
        Assert.Matches("avis\\\\finn</td>\\s*<td>Bearbeiter\\s*<span class=\"hinweis\">abgeglichen</span>", seite);

        Assert.Equal(["Anna Beispiel"], Namen(await anna.GetStringAsync($"{Liste}?zeigen=administratoren")));
        Assert.Equal(["Dieter Kunde"], Namen(await anna.GetStringAsync($"{Liste}?zeigen=ohne")));
        Assert.Equal(5, Namen(await anna.GetStringAsync($"{Liste}?zeigen=alle")).Length);
        // Eine unbekannte Auswahl zeigt die Voreinstellung.
        Assert.Equal(4, Namen(await anna.GetStringAsync($"{Liste}?zeigen=unsinn")).Length);
    }

    [Fact]
    public async Task Die_Suche_findet_ueber_Name_Anmeldename_und_E_Mail()
    {
        using var app = new AvisAnwendung();
        var anna = await app.AnmeldenAlsAsync("anna");
        await app.DatenbankAsync(async db =>
        {
            db.Benutzer.Add(new Benutzer { Anmeldename = @"avis\oezguer", Name = "Özgür Übung", Email = "oezguer.uebung@example.org" });
            return await db.SaveChangesAsync();
        });

        Assert.Equal(["Bernd Muster"], Namen(await anna.GetStringAsync($"{Liste}?q=MUSTER")));
        Assert.Equal(["Bernd Muster"], Namen(await anna.GetStringAsync($"{Liste}?q=muster+bernd")));
        Assert.Equal(["Clara Probe"], Namen(await anna.GetStringAsync($"{Liste}?q=clara.probe%40example")));
        Assert.Equal(["Finn Verzeichnis"], Namen(await anna.GetStringAsync($"{Liste}?q=avis%5Cfinn")));

        // Özgür hat keine Rolle. In der Voreinstellung bleibt die Liste leer, die Auswahl zeigt aber, wo der Treffer steht,
        // und behält den Suchbegriff. Großschreibung und Umlaute spielen keine Rolle.
        var ohneTreffer = await anna.GetStringAsync($"{Liste}?q=%C3%96ZG%C3%9CR");
        Assert.Contains("Keine Benutzer gefunden.", ohneTreffer);
        Assert.Contains("Mit Rolle (0)", ohneTreffer);
        Assert.Contains("Ohne Rolle (1)", ohneTreffer);
        Assert.Matches("href=\"[^\"]*zeigen=ohne[^\"]*\"[^>]*>Ohne Rolle", ohneTreffer);
        Assert.Matches("href=\"[^\"]*q=[^\"]*zeigen=ohne|href=\"[^\"]*zeigen=ohne[^\"]*q=", ohneTreffer);
        Assert.Equal(["Özgür Übung"], Namen(await anna.GetStringAsync($"{Liste}?q=%C3%96ZG%C3%9CR&zeigen=ohne")));
        Assert.Equal(["Özgür Übung"], Namen(await anna.GetStringAsync($"{Liste}?q=%C3%BCbung&zeigen=alle")));

        // Ändert sich der Name, findet die Suche den neuen und nicht mehr den alten.
        var bernd = await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "bernd").Select(b => b.Id).SingleAsync());
        await anna.PostFormularAsync($"{Liste}/{bernd}", $"{Liste}/{bernd}", new() { ["name"] = "Bernd Beispielmann", ["rolle"] = "Bearbeiter", ["aktiv"] = "true" });
        Assert.Equal(["Bernd Beispielmann"], Namen(await anna.GetStringAsync($"{Liste}?q=beispielmann")));
        Assert.Empty(Namen(await anna.GetStringAsync($"{Liste}?q=muster")));
    }

    [Fact]
    public async Task Eine_lange_Liste_wird_gekuerzt_und_die_Konten_von_AVIS_selbst_stehen_nie_darin()
    {
        using var app = new AvisAnwendung();
        var anna = await app.AnmeldenAlsAsync("anna");
        await app.DatenbankAsync(async db =>
        {
            db.Benutzer.AddRange(Enumerable.Range(1, 105).Select(i => new Benutzer { Anmeldename = $@"avis\kunde{i:D3}", Name = $"Kunde {i:D3}" }));
            db.Benutzer.Add(new Benutzer { Anmeldename = "avis:e-mail-eingang", Name = "E-Mail-Eingang", Aktiv = false });
            return await db.SaveChangesAsync();
        });

        // Die vielen Konten ohne Rolle stören die Voreinstellung nicht.
        Assert.Equal(4, Namen(await anna.GetStringAsync(Liste)).Length);

        var ohne = await anna.GetStringAsync($"{Liste}?zeigen=ohne");
        Assert.Contains("Ohne Rolle (106)", ohne);
        Assert.Equal(Avis.Web.Pages.Verwaltung.BenutzerModel.Hoechstzahl, Namen(ohne).Length);
        Assert.Contains("Es werden die ersten 100 Benutzer angezeigt.", ohne);
        Assert.Equal(["Kunde 042"], Namen(await anna.GetStringAsync($"{Liste}?zeigen=ohne&q=kunde042")));

        var alle = await anna.GetStringAsync($"{Liste}?zeigen=alle&q=eingang");
        Assert.Contains("Keine Benutzer gefunden.", alle);
        Assert.Contains("Alle (0)", alle);
    }

    [Fact]
    public async Task Konten_aus_der_Zeit_vor_der_Suche_bekommen_beim_Start_ihren_Suchtext()
    {
        using var app = new AvisAnwendung();
        await app.DatenbankAsync(db => db.Database.ExecuteSqlRawAsync("UPDATE \"Benutzer\" SET \"Suchtext\" = ''"));

        // Ein zweiter Start auf derselben Datenbank, wie nach einem Update von AVIS.
        using var neuGestartet = app.WithWebHostBuilder(_ => { });
        using var scope = neuGestartet.Services.CreateScope();
        var suchtexte = await scope.ServiceProvider.GetRequiredService<AvisDb>().Benutzer.Select(b => b.Suchtext).ToListAsync();

        Assert.Contains("bernd muster bernd bernd.muster@example.org", suchtexte);
        Assert.DoesNotContain("", suchtexte);
    }

    /// <summary>Die Namen in der Tabelle, in der Reihenfolge der Seite.</summary>
    private static string[] Namen(string seite) =>
        System.Text.RegularExpressions.Regex
            .Matches(seite, "<td><a href=\"/Verwaltung/Benutzer/\\d+\">([^<]+)</a></td>")
            .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value))
            .ToArray();
}
