using System.Net;
using Avis.Web;
using Avis.Web.Anhaenge;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

public class MarkdownTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    private static string Html(string text)
    {
        using var schreiber = new StringWriter();
        Markdowntext.AlsHtml(text).WriteTo(schreiber, System.Text.Encodings.Web.HtmlEncoder.Default);
        return schreiber.ToString();
    }

    [Theory]
    [InlineData("Das ist **wichtig** und *betont*.", "<strong>wichtig</strong>", "<em>betont</em>")]
    [InlineData("- eins\n- zwei", "<ul>", "<li>eins</li>")]
    [InlineData("1. erstens\n2. zweitens", "<ol>", "<li>zweitens</li>")]
    [InlineData("Fehler `0x80070005` im Protokoll", "<code>0x80070005</code>")]
    [InlineData("```\nnet use Z: \\\\server\\freigabe\n```", "<pre><code>", "net use Z: \\\\server\\freigabe")]
    [InlineData("> zitiert", "<blockquote>")]
    public void Markdown_wird_formatiert(string text, params string[] erwartet)
    {
        var html = Html(text);

        Assert.All(erwartet, e => Assert.Contains(e, html));
    }

    [Fact]
    public void Gewoehnlicher_Text_behaelt_Zeilenumbrueche_und_Umlaute()
    {
        var html = Html("Guten Tag,\nder Drucker im 3. OG läuft nicht.\n\nGrüße\nMüller & Söhne");

        Assert.Contains("Guten Tag,<br />", html);
        Assert.Contains("der Drucker im 3. OG läuft nicht.</p>", html);
        Assert.Contains("Grüße<br />", html);
        Assert.Contains("Müller &amp; Söhne", html);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("Text <b onmouseover=\"alert(1)\">fett</b>")]
    public void Eingegebenes_HTML_wird_als_Text_gezeigt(string text)
    {
        var html = Html(text);

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b ", html);
        Assert.Contains("&lt;", html);
    }

    [Theory]
    [InlineData("[klick](javascript:alert(1))")]
    [InlineData("[klick](JaVaScRiPt:alert(1))")]
    [InlineData("[klick](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[klick](vbscript:msgbox(1))")]
    [InlineData("[klick](/Verwaltung/Benutzer)")]
    public void Links_fuehren_nur_zu_http_https_und_mailto(string text)
    {
        var html = Html(text);

        Assert.DoesNotContain("<a", html);
        Assert.DoesNotContain("href", html);
        Assert.Contains("klick", html);
    }

    [Fact]
    public void Erlaubte_Links_bekommen_einen_Schutz_fuer_das_neue_Fenster()
    {
        var html = Html("Siehe [Anleitung](https://example.org/hilfe) oder https://example.org/direkt oder schreib an <hotline@example.org>.");

        Assert.Contains("<a href=\"https://example.org/hilfe\" rel=\"noopener noreferrer nofollow\">Anleitung</a>", html);
        Assert.Contains("<a href=\"https://example.org/direkt\"", html);
        Assert.Contains("href=\"mailto:hotline@example.org\"", html);
    }

    [Fact]
    public void Fremde_Bilder_werden_nicht_geladen()
    {
        var html = Html("![Zählpixel](https://example.org/pixel.png) und ![x](javascript:alert(1))");

        Assert.DoesNotContain("<img", html);
        Assert.Contains("<a href=\"https://example.org/pixel.png\"", html);
        Assert.DoesNotContain("javascript", html);
    }

    [Fact]
    public async Task Beschreibung_und_Verlauf_zeigen_Markdown_fuer_Bearbeiter_und_Kunde()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var gestellt = await dieter.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = "Markdown in Texten",
            ["Eingabe.Beschreibung"] = "Der Fehler lautet `E42`.\n\n- Schritt eins\n- Schritt zwei\n\n<script>alert('x')</script>",
        });
        var id = int.Parse(gestellt.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        await anna.UebernehmenAsync(app, id);
        await anna.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=NachrichtAnKunden", new() { ["version"] = "2", ["text"] = "Bitte **neu starten**." });

        foreach (var seite in new[] { await anna.GetStringAsync($"/Auftraege/{id}"), await dieter.GetStringAsync($"/Meine/{id}") })
        {
            Assert.Contains("<code>E42</code>", seite);
            Assert.Contains("<li>Schritt zwei</li>", seite);
            Assert.Contains("<strong>neu starten</strong>", seite);
            Assert.DoesNotContain("<script>alert", seite);
        }

        // Gespeichert ist der reine Text, und die Mail an den Kunden enthält ihn unverändert.
        var auftrag = await app.DatenbankAsync(db => db.Auftraege.AsNoTracking().Include(a => a.Verlauf).FirstAsync(a => a.Id == id));
        Assert.Equal("Bitte **neu starten**.", auftrag.Verlauf.OrderBy(v => v.Id).Last().Text);
        Assert.Contains("Markdown in Texten", await anna.GetStringAsync("/Auftraege?q=schritt+zwei"));
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 73, 72, 68, 82, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70, 0, 1, 1];

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 1, 2, 3 }, "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 9, 9, 9, 9, 0x57, 0x45, 0x42, 0x50, 1 }, "image/webp")]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20, 0x78, 0x6D, 0x6C, 0x6E, 0x73 }, null)] // <svg xmlns
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E }, null)]                         // <html>
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 }, null)]                           // nur der Anfang eines PNG
    [InlineData(new byte[] { 0x89 }, null)]
    public async Task Bilder_werden_am_Inhalt_erkannt_nicht_am_Namen(byte[] inhalt, string? erwartet)
    {
        Assert.Equal(erwartet, await Anhangablage.BildtypAsync(new MemoryStream(inhalt)));
    }

    [Fact]
    public async Task Bilder_erscheinen_im_Verlauf_als_Vorschau_und_werden_als_Bild_ausgeliefert()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Mit Bildschirmfoto", ("Bildschirmfoto 2026-11-13.png", Png), ("foto.jpg", Jpeg), ("protokoll.txt", "Fehler\n"u8.ToArray()));
        var anhaenge = await AnhaengeAsync(id);
        var bild = anhaenge.Single(a => a.Dateiname.EndsWith(".png", StringComparison.Ordinal));
        var text = anhaenge.Single(a => a.Dateiname == "protokoll.txt");
        Assert.Equal(("image/png", "image/jpeg", null), (bild.Bildtyp, anhaenge.Single(a => a.Dateiname == "foto.jpg").Bildtyp, text.Bildtyp));

        var seite = await anna.GetStringAsync($"/Auftraege/{id}");
        Assert.Contains($"<img src=\"/Anhaenge/{bild.Id}?bild=true\" alt=\"Bildschirmfoto 2026-11-13.png\"", seite);
        Assert.DoesNotContain($"<img src=\"/Anhaenge/{text.Id}", seite);

        var antwort = await anna.GetAsync($"/Anhaenge/{bild.Id}?bild=true");
        Assert.Equal("image/png", antwort.Content.Headers.ContentType!.MediaType);
        Assert.Null(antwort.Content.Headers.ContentDisposition);
        Assert.Equal(Png, await antwort.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", antwort.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("default-src 'none'; sandbox", antwort.Headers.GetValues("Content-Security-Policy").Single());

        // Ohne den Zusatz bleibt es ein Download, auch für Bilder.
        var download = await anna.GetAsync($"/Anhaenge/{bild.Id}");
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
    }

    [Fact]
    public async Task Was_kein_Bild_ist_wird_auch_auf_Wunsch_nicht_angezeigt()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Getarnte Datei",
            ("harmlos.png", "<html><script>alert(1)</script></html>"u8.ToArray()),
            ("zeichnung.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray()));

        foreach (var anhang in await AnhaengeAsync(id))
        {
            Assert.Null(anhang.Bildtyp);
            var antwort = await anna.GetAsync($"/Anhaenge/{anhang.Id}?bild=true");
            Assert.Equal("application/octet-stream", antwort.Content.Headers.ContentType!.MediaType);
            Assert.Equal("attachment", antwort.Content.Headers.ContentDisposition!.DispositionType);
        }
        Assert.DoesNotContain("<img", (await anna.GetStringAsync($"/Auftraege/{id}")).Split("<main")[1]);
    }

    [Fact]
    public async Task Ein_Bild_an_einem_internen_Kommentar_bekommt_der_Kunde_auch_als_Bild_nicht()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var gestellt = await dieter.PostMitDateienAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = "Bilder intern und sichtbar",
            ["Eingabe.Beschreibung"] = "Siehe Bild.",
        }, ("vom-kunden.png", Png));
        var id = int.Parse(gestellt.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        await anna.UebernehmenAsync(app, id);
        await anna.PostMitDateienAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=InternerKommentar", new() { ["version"] = "2" }, ("intern.png", Png));

        var anhaenge = await AnhaengeAsync(id);
        var sichtbar = anhaenge.Single(a => a.Dateiname == "vom-kunden.png");
        var intern = anhaenge.Single(a => a.Dateiname == "intern.png");

        var sicht = await dieter.GetStringAsync($"/Meine/{id}");
        Assert.Contains($"<img src=\"/Anhaenge/{sichtbar.Id}?bild=true\"", sicht);
        Assert.DoesNotContain("intern.png", sicht);
        Assert.Equal(HttpStatusCode.OK, (await dieter.GetAsync($"/Anhaenge/{sichtbar.Id}?bild=true")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dieter.GetAsync($"/Anhaenge/{intern.Id}?bild=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anna.GetAsync($"/Anhaenge/{intern.Id}?bild=true")).StatusCode);
    }

    private async Task<int> ErfassenAsync(HttpClient client, string betreff, params (string Name, byte[] Inhalt)[] dateien)
    {
        var antwort = await client.PostMitDateienAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync())).ToString(),
        }, dateien);
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private Task<List<Anhang>> AnhaengeAsync(int auftragId) => app.DatenbankAsync(db => db.Anhaenge
        .AsNoTracking()
        .Where(a => a.Verlaufseintrag.AuftragId == auftragId)
        .OrderBy(a => a.Id)
        .ToListAsync());
}
