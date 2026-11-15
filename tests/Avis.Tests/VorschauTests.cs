using System.Net;

namespace Avis.Tests;

/// <summary>Die Vorschau an den Textfeldern: Der Server zeigt den Text so, wie er später im Auftrag steht.</summary>
public class VorschauTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Fact]
    public async Task Die_Vorschau_zeigt_den_Text_wie_spaeter_der_Auftrag()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");

        var antwort = await dieter.PostFormularAsync("/Meine/Neu", "/Vorschau", new()
        {
            ["text"] = "Der Drucker **druckt nicht**.\nZweite Zeile\n\n- Toner geprüft\n\n<b>kein HTML</b> und [Falle](javascript:alert(1))",
        });

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("text/html", antwort.Content.Headers.ContentType?.MediaType);
        var html = await antwort.Content.ReadAsStringAsync();
        Assert.StartsWith("<div class=\"md\">", html.TrimStart());
        Assert.Contains("<strong>druckt nicht</strong>", html);
        // Wie bei der Anzeige: Ein Zeilenumbruch bleibt einer, HTML bleibt Text, und Links führen nur ins Netz.
        Assert.Contains("<br", html);
        Assert.Contains("<li>Toner geprüft</li>", html);
        Assert.Contains("&lt;b&gt;kein HTML&lt;/b&gt;", html);
        Assert.DoesNotContain("javascript:", html);
        // Es kommt nur der Text zurück, keine ganze Seite.
        Assert.DoesNotContain("<html", html);
    }

    [Fact]
    public async Task Die_Vorschau_stimmt_mit_der_Anzeige_im_Auftrag_ueberein()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        const string text = "Seit heute **kein Netz**.\n\n1. Kabel geprüft\n2. Neustart\n\n> Meldung: Kein Zugriff";

        var vorschau = await (await dieter.PostFormularAsync("/Meine/Neu", "/Vorschau", new() { ["text"] = text })).Content.ReadAsStringAsync();
        var auftrag = await (await dieter.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = "Vorschau und Anzeige",
            ["Eingabe.Beschreibung"] = text,
        })).Content.ReadAsStringAsync();

        Assert.Contains(vorschau.Trim().ReplaceLineEndings("\n"), auftrag.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Ein_leerer_Text_ergibt_eine_leere_Vorschau()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");

        var antwort = await dieter.PostFormularAsync("/Meine/Neu", "/Vorschau", new() { ["text"] = "  " });

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal("", (await antwort.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task Die_Vorschau_antwortet_nur_angemeldeten_Formularen()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");

        // Aufrufen lässt sie sich nicht, und ohne das Merkmal eines Formulars von AVIS antwortet sie nicht.
        Assert.Equal(HttpStatusCode.NotFound, (await dieter.GetAsync("/Vorschau")).StatusCode);
        var ohneMerkmal = await dieter.PostAsync("/Vorschau", new FormUrlEncodedContent(new Dictionary<string, string> { ["text"] = "**fett**" }));
        Assert.Equal(HttpStatusCode.BadRequest, ohneMerkmal.StatusCode);

        // Ohne Anmeldung gibt es keine Vorschau, sondern die Anmeldeseite.
        var fremd = app.CreateClient();
        var antwort = await fremd.PostAsync("/Vorschau", new FormUrlEncodedContent(new Dictionary<string, string> { ["text"] = "**fett**" }));
        Assert.DoesNotContain("<strong>fett</strong>", await antwort.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("anna", "/Auftraege/Neu")]
    [InlineData("dieter", "/Meine/Neu")]
    public async Task Die_Seiten_mit_Textfeld_nennen_dem_Skript_die_Adresse_der_Vorschau(string anmeldename, string seite)
    {
        var client = await app.AnmeldenAlsAsync(anmeldename);

        var html = await client.GetStringAsync(seite);

        Assert.Contains("<body data-vorschau=\"/Vorschau\">", html);
        Assert.Matches("<textarea[^>]* data-markdown", html);
        Assert.Contains("/js/editor.js", html);
    }

    [Fact]
    public async Task Ohne_Anmeldung_nennt_die_Seite_keine_Adresse_der_Vorschau()
    {
        var html = await app.CreateClient().GetStringAsync("/Anmeldung");

        // Das Merkmal bleibt leer; das Skript bietet dann keine Vorschau an.
        Assert.Contains("<body data-vorschau=\"\">", html);
    }
}
