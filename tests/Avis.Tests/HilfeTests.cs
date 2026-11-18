using System.Net;
using Avis.Web.Hilfe;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Avis.Tests;

/// <summary>Die Hilfeseiten und der Verweis "Hilfe" im Kopf jeder Seite.</summary>
public class HilfeTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Fact]
    public async Task Bearbeiter_sehen_ihre_Hilfe_und_koennen_zu_der_fuer_Kunden_wechseln()
    {
        var bernd = await app.AnmeldenAlsAsync("bernd");

        var hilfe = await bernd.GetStringAsync("/Hilfe");

        Assert.Contains("<h1>Hilfe für Bearbeiter</h1>", hilfe);
        Assert.Contains("<h2 id=\"uebernehmen\">Übernehmen</h2>", hilfe);
        // Das Inhaltsverzeichnis nennt die Abschnitte, und Tabellen kommen als Tabellen an.
        Assert.Contains("<a href=\"#geraete-nennen\">Geräte nennen</a>", hilfe);
        Assert.Contains("<th>Aktion</th>", hilfe);
        Assert.Contains("Für Kunden</a>", hilfe);
        Assert.DoesNotContain("Für Administratoren", hilfe);

        // Kurzvideos und Kurzanleitung liegen im Internet. Der Verweis dorthin gibt die Adresse dieser Seite nicht weiter.
        Assert.Contains("<a href=\"https://t3chsynergy.github.io/avis/\" rel=\"noopener noreferrer\">Kurzvideos</a>", hilfe);
        Assert.Contains("<a href=\"https://t3chsynergy.github.io/avis/kurzanleitung.html\" rel=\"noopener noreferrer\">Kurzanleitung mit Bildern</a>", hilfe);

        var fuerKunden = await bernd.GetStringAsync("/Hilfe/kunden");
        Assert.DoesNotContain("t3chsynergy.github.io", fuerKunden);
        Assert.Contains("<h2 id=\"einen-auftrag-stellen\">Einen Auftrag stellen</h2>", fuerKunden);
        Assert.DoesNotContain("id=\"uebernehmen\"", fuerKunden);
        Assert.Equal(HttpStatusCode.NotFound, (await bernd.GetAsync("/Hilfe/verwaltung")).StatusCode);
    }

    [Fact]
    public async Task Administratoren_sehen_ausserdem_die_Hilfe_zur_Verwaltung()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        // Ohne Angabe sehen auch sie die Hilfe für Bearbeiter: Damit arbeiten sie die meiste Zeit.
        var hilfe = await anna.GetStringAsync("/Hilfe");
        Assert.Contains("<h1>Hilfe für Bearbeiter</h1>", hilfe);
        Assert.Contains("href=\"/Hilfe/administratoren\">Für Administratoren</a>", hilfe);

        var verwaltung = await anna.GetStringAsync("/Hilfe/administratoren");
        Assert.Contains("<h1>Hilfe für Administratoren</h1>", verwaltung);
        Assert.Contains("<h2 id=\"benutzer-und-rollen\">Benutzer und Rollen</h2>", verwaltung);
        Assert.Contains("<a href=\"#geraeteabgleich\">Geräteabgleich</a>", verwaltung);
        Assert.Matches("<a aria-current=\"page\"[^>]*>Für Administratoren</a>", verwaltung);
    }

    [Theory]
    [InlineData("bernd")]
    [InlineData("dieter")]
    public async Task Wer_kein_Administrator_ist_bekommt_statt_der_Hilfe_zur_Verwaltung_seine_eigene(string anmeldename)
    {
        var client = await app.AnmeldenAlsAsync(anmeldename);

        var umgeleitet = await client.GetAsync("/Hilfe/administratoren");

        Assert.Equal("/Hilfe", umgeleitet.RequestMessage!.RequestUri!.AbsolutePath);
        var html = await umgeleitet.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Benutzer und Rollen", html);
        Assert.DoesNotContain("Für Administratoren", html);
    }

    [Fact]
    public async Task Kunden_sehen_nur_die_Hilfe_fuer_Kunden()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");

        var hilfe = await dieter.GetStringAsync("/Hilfe");

        Assert.Contains("<h1>Hilfe</h1>", hilfe);
        Assert.Contains("id=\"einen-auftrag-stellen\"", hilfe);
        Assert.DoesNotContain("Für Bearbeiter", hilfe);
        Assert.DoesNotContain("Interner Kommentar", hilfe);

        // Wer die Adresse der Hilfe für Bearbeiter aufruft, landet in seiner eigenen.
        var umgeleitet = await dieter.GetAsync("/Hilfe/bearbeiter");
        Assert.Equal("/Hilfe", umgeleitet.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("Interner Kommentar", await umgeleitet.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/Auftraege/Neu", "href=\"/Hilfe#auftrag-erfassen\"")]
    [InlineData("/Auftraege/1", "href=\"/Hilfe#am-auftrag-arbeiten\"")]
    [InlineData("/Gruppen", "href=\"/Hilfe#die-listen\"")]
    [InlineData("/Kunden", "href=\"/Hilfe#kunden\"")]
    [InlineData("/Gruppen/Meine", "href=\"/Hilfe#meine-gruppen\"")]
    [InlineData("/Meine", "href=\"/Hilfe/kunden#stand-eines-auftrags\"")]
    [InlineData("/Einstellungen", "href=\"/Hilfe#einstellungen\"")]
    [InlineData("/Verwaltung/Gruppen", "href=\"/Hilfe/administratoren#gruppen\"")]
    [InlineData("/Verwaltung/Benutzer/1", "href=\"/Hilfe/administratoren#benutzer-und-rollen\"")]
    [InlineData("/Verwaltung/Kundenabgleich", "href=\"/Hilfe/administratoren#kundenabgleich\"")]
    [InlineData("/Geraete/Bearbeiten", "href=\"/Hilfe/administratoren#geraete-anlegen-und-aendern\"")]
    public async Task Hilfe_im_Kopf_fuehrt_zum_Abschnitt_der_zur_Seite_gehoert(string seite, string verweis)
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var html = await anna.GetStringAsync(seite);

        Assert.Matches($"<a class=\"hilfe\"[^>]*{System.Text.RegularExpressions.Regex.Escape(verweis)}", html);
    }

    [Fact]
    public async Task Fuer_Kunden_fuehrt_Hilfe_im_Kopf_in_ihre_eigene_Hilfe()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");

        Assert.Matches("<a class=\"hilfe\"[^>]*href=\"/Hilfe#einen-auftrag-stellen\"", await dieter.GetStringAsync("/Meine/Neu"));
        Assert.Matches("<a class=\"hilfe\"[^>]*href=\"/Hilfe#stand-eines-auftrags\"", await dieter.GetStringAsync("/Meine"));
        // Die Einstellungen benutzen alle; jeder landet im Abschnitt seiner eigenen Hilfe.
        Assert.Matches("<a class=\"hilfe\"[^>]*href=\"/Hilfe#einstellungen\"", await dieter.GetStringAsync("/Einstellungen"));
    }

    [Fact]
    public void Jeder_Verweis_einer_Seite_trifft_einen_Abschnitt_und_jede_Seite_ist_bedacht()
    {
        var texte = app.Services.GetRequiredService<Hilfetexte>();
        var abschnitte = Hilfetexte.Themen
            .ToDictionary(t => t, t => texte.Lesen(t).Abschnitte.Select(a => a.Anker).ToHashSet());

        // Ändert jemand eine Überschrift, ändert sich ihre Sprungmarke. Dann fällt es hier auf und nicht erst beim Anwender.
        Assert.All(Hilfeverweise.Seiten, v => Assert.True(abschnitte[v.Value.Thema].Contains(v.Value.Anker), $"{v.Key} verweist auf #{v.Value.Anker}, den es in der Hilfe für {v.Value.Thema} nicht gibt."));

        // Jede Seite hat einen Abschnitt. Ausgenommen sind die Hilfe selbst und Seiten ohne eigene Ansicht.
        var ohne = new[] { "/Hilfe/", "/Anhaenge/", "/Anmeldung", "/Fehler", "/Status", "/Kunden/Treffer", "/Vorschau" };
        var seiten = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<PageActionDescriptor>()
            .Select(p => p.ViewEnginePath)
            .Where(p => !ohne.Any(p.StartsWith))
            .Distinct()
            .ToList();
        Assert.NotEmpty(seiten);
        Assert.All(seiten, s => Assert.True(Hilfeverweise.Seiten.ContainsKey(s) || Hilfeverweise.FuerAlle.ContainsKey(s), $"Die Seite {s} hat keinen Abschnitt in der Hilfe."));

        // Seiten für alle haben ihren Abschnitt in beiden Hilfen, unter derselben Sprungmarke.
        Assert.All(Hilfeverweise.FuerAlle, v =>
        {
            Assert.False(Hilfeverweise.Seiten.ContainsKey(v.Key), $"{v.Key} steht doppelt.");
            Assert.Contains(v.Value, abschnitte[Hilfetexte.Bearbeiter]);
            Assert.Contains(v.Value, abschnitte[Hilfetexte.Kunden]);
        });

        // Was nur Administratoren öffnen können, führt in ihre Hilfe, und nichts anderes: Sonst landete ein
        // Bearbeiter von seiner Seite am Anfang der Hilfe statt beim Abschnitt.
        Assert.All(seiten.Where(Hilfeverweise.Seiten.ContainsKey), s => Assert.Equal(
            s.StartsWith("/Verwaltung/", StringComparison.Ordinal) || s == "/Geraete/Bearbeiten",
            Hilfeverweise.Seiten[s].Thema == Hilfetexte.Administratoren));
    }

    [Fact]
    public void Verweise_innerhalb_der_Hilfe_treffen_einen_Abschnitt()
    {
        var texte = app.Services.GetRequiredService<Hilfetexte>();
        foreach (var thema in Hilfetexte.Themen)
        {
            var html = Html(texte.Lesen(thema).Inhalt);
            var marken = System.Text.RegularExpressions.Regex.Matches(html, " id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
            var verweise = System.Text.RegularExpressions.Regex.Matches(html, "href=\"#([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.All(verweise, v => Assert.Contains(v, marken));
        }
    }

    [Theory]
    [InlineData("Geräte nennen", "geraete-nennen")]
    [InlineData("  Angaben und Kunde ändern ", "angaben-und-kunde-aendern")]
    [InlineData("E-Mails von AVIS", "e-mails-von-avis")]
    [InlineData("Größe: 10 MB?", "groesse-10-mb")]
    [InlineData("???", "abschnitt")]
    public void Die_Sprungmarke_ergibt_sich_aus_der_Ueberschrift(string ueberschrift, string marke) =>
        Assert.Equal(marke, Hilfetexte.Anker(ueberschrift));

    [Fact]
    public void Aus_Markdown_wird_HTML_mit_Sprungmarken_und_eingegebenes_HTML_bleibt_Text()
    {
        var seite = Hilfetexte.Aufbereiten("# Titel\n\nVorweg.\n\n## Erster Teil\n\n<script>alert(1)</script>\n\n### Unterpunkt\n\n## Erster Teil\n\n| A | B |\n|---|---|\n| 1 | 2 |\n");

        Assert.Equal("Titel", seite.Titel);
        Assert.Equal([new("erster-teil", "Erster Teil"), new("erster-teil-2", "Erster Teil")], seite.Abschnitte);
        var html = Html(seite.Inhalt);
        Assert.DoesNotContain("<h1", html);
        Assert.Contains("<h3 id=\"unterpunkt\">Unterpunkt</h3>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("<table>", html);
    }

    [Fact]
    public async Task Eigener_Text_des_Hauses_steht_ueber_der_Hilfe_wenn_es_die_Datei_gibt()
    {
        var ordner = Directory.CreateTempSubdirectory("avis-hilfe-").FullName;
        var datei = Path.Combine(ordner, "Hilfe.md");
        using var basis = new AvisAnwendung();
        using var eigene = basis.WithWebHostBuilder(b => b.UseSetting("Hilfe:EigenerText", datei));
        var client = eigene.CreateClient();
        await client.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = "dieter" });

        // Ohne Datei steht dort nichts.
        Assert.DoesNotContain("Hinweise der IT", await client.GetStringAsync("/Hilfe"));

        // Mit Datei erscheint ihr Inhalt, ohne Neustart, bei Kunden wie bei Bearbeitern.
        await File.WriteAllTextAsync(datei, "## So erreichen Sie uns\n\nDie Hotline hat die Nummer **1234**. <b>kein HTML</b>");
        var hilfe = await client.GetStringAsync("/Hilfe");
        Assert.Contains("aria-label=\"Hinweise der IT\"", hilfe);
        Assert.Contains("Die Hotline hat die Nummer <strong>1234</strong>.", hilfe);
        Assert.Contains("&lt;b&gt;kein HTML&lt;/b&gt;", hilfe);
        Assert.True(hilfe.IndexOf("So erreichen Sie uns", StringComparison.Ordinal) < hilfe.IndexOf("Einen Auftrag stellen</h2>", StringComparison.Ordinal));

        Directory.Delete(ordner, recursive: true);
    }

    [Fact]
    public async Task Die_Formulare_weisen_auf_Prioritaet_und_Formatierung_hin()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var dieter = await app.AnmeldenAlsAsync("dieter");

        var erfassen = await anna.GetStringAsync("/Auftraege/Neu");
        Assert.Contains("ergeben zusammen die Priorität", erfassen);
        Assert.Contains("href=\"/Hilfe#prioritaet\"", erfassen);
        Assert.Contains("href=\"/Hilfe#texte-formatieren\"", erfassen);

        // Auf den Seiten für Kunden führt der Hinweis in deren Hilfe, auch für einen Bearbeiter.
        Assert.Contains("href=\"/Hilfe#text-formatieren\"", await dieter.GetStringAsync("/Meine/Neu"));
        Assert.Contains("href=\"/Hilfe/kunden#text-formatieren\"", await anna.GetStringAsync("/Meine/Neu"));
    }

    private static string Html(Microsoft.AspNetCore.Html.IHtmlContent inhalt)
    {
        using var schreiber = new StringWriter();
        inhalt.WriteTo(schreiber, System.Text.Encodings.Web.HtmlEncoder.Default);
        return schreiber.ToString();
    }
}
