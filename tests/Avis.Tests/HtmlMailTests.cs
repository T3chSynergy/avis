using Avis.Web;
using Avis.Web.Domain;
using Avis.Web.Posteingang;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Avis.Tests;

public class HtmlMailTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    private static string Html(string markdown)
    {
        using var schreiber = new StringWriter();
        Markdowntext.AlsHtml(markdown).WriteTo(schreiber, System.Text.Encodings.Web.HtmlEncoder.Default);
        return schreiber.ToString();
    }

    private const string OutlookMail = """
        <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word">
        <head><meta http-equiv=Content-Type content="text/html; charset=utf-8">
        <style><!-- p.MsoNormal, li.MsoNormal {margin:0cm; font-family:"Calibri",sans-serif;} --></style>
        <!--[if gte mso 9]><xml><o:shapedefaults v:ext="edit" spidmax="1026" /></xml><![endif]-->
        </head>
        <body lang=DE link="#0563C1" vlink="#954F72">
        <div class=WordSection1>
        <p class=MsoNormal>Guten Tag,<o:p></o:p></p>
        <p class=MsoNormal><o:p>&nbsp;</o:p></p>
        <p class=MsoNormal>der Drucker meldet <b>Papierstau</b>, obwohl <i>kein</i> Papier klemmt.<o:p></o:p></p>
        <p class=MsoNormal><o:p>&nbsp;</o:p></p>
        <ul style='margin-top:0cm' type=disc>
         <li class=MsoListParagraph style='margin-left:0cm;mso-list:l0 level1 lfo1'>Gerät neu gestartet<o:p></o:p></li>
         <li class=MsoListParagraph style='margin-left:0cm;mso-list:l0 level1 lfo1'><span style='color:red'>Papierfach</span> geprüft<o:p></o:p></li>
        </ul>
        <p class=MsoNormal><o:p>&nbsp;</o:p></p>
        <p class=MsoNormal>Anleitung: <a href="https://example.org/hilfe?a=1&amp;b=2"><span style='color:#0563C1'>Hersteller</span></a><o:p></o:p></p>
        <p class=MsoNormal><o:p>&nbsp;</o:p></p>
        <p class=MsoNormal><span style='font-size:10.0pt;color:#1F497D'>Viele Grüße<o:p></o:p></span></p>
        <p class=MsoNormal><span style='font-size:10.0pt;color:#1F497D'>Gabi Müller<o:p></o:p></span></p>
        </div>
        </body></html>
        """;

    [Fact]
    public void Eine_Mail_aus_Outlook_behaelt_fett_kursiv_Liste_und_Link()
    {
        var markdown = HtmlZuMarkdown.Wandeln(OutlookMail);

        Assert.Equal("""
            Guten Tag,

            der Drucker meldet **Papierstau**, obwohl *kein* Papier klemmt.

            - Gerät neu gestartet
            - Papierfach geprüft

            Anleitung: [Hersteller](https://example.org/hilfe?a=1&b=2)

            Viele Grüße
            Gabi Müller
            """.ReplaceLineEndings("\n"), markdown);

        var html = Html(markdown);
        Assert.Contains("<strong>Papierstau</strong>", html);
        Assert.Contains("<li>Papierfach geprüft</li>", html);
        Assert.Contains("<a href=\"https://example.org/hilfe?a=1&amp;b=2\"", html);
    }

    [Fact]
    public void Listen_die_Outlook_als_Absaetze_schreibt_werden_erkannt()
    {
        var markdown = HtmlZuMarkdown.Wandeln("""
            <p class=MsoNormal>Schritte:</p>
            <p class=MsoListParagraph style='text-indent:-18.0pt;mso-list:l0 level1 lfo1'><![if !supportLists]><span style='font-family:Symbol'><span style='mso-list:Ignore'>·<span style='font:7.0pt "Times New Roman"'>&nbsp;&nbsp;&nbsp;&nbsp;</span></span></span><![endif]>Kabel prüfen<o:p></o:p></p>
            <p class=MsoListParagraph style='text-indent:-18.0pt;mso-list:l0 level1 lfo1'><![if !supportLists]><span style='font-family:Symbol'><span style='mso-list: Ignore'>·<span>&nbsp;</span></span></span><![endif]>Neu starten<o:p></o:p></p>
            <p class=MsoListParagraphCxSpFirst style='mso-list:l1 level1 lfo2'><![if !supportLists]><span style='mso-list:Ignore'>1.<span>&nbsp;&nbsp;</span></span><![endif]>Erstens</p>
            <p class=MsoListParagraphCxSpLast style='mso-list:l1 level1 lfo2'><![if !supportLists]><span style='mso-list:Ignore'>2.<span>&nbsp;&nbsp;</span></span><![endif]>Zweitens</p>
            """);

        Assert.Equal("Schritte:\n- Kabel prüfen\n- Neu starten\n1. Erstens\n2. Zweitens", markdown);
    }

    [Theory]
    // Gmail und andere: jede Zeile ein div, eine Leerzeile ist ein div mit br
    [InlineData("<div dir=\"ltr\">Hallo,<div><br></div><div>Zeile zwei</div><div>Zeile drei</div></div>", "Hallo,\n\nZeile zwei\nZeile drei")]
    // Thunderbird: Absätze und br
    [InlineData("<p>Erster Absatz<br>mit Umbruch</p><p>Zweiter Absatz</p>", "Erster Absatz\nmit Umbruch\n\nZweiter Absatz")]
    [InlineData("Nur Text ohne Tags", "Nur Text ohne Tags")]
    [InlineData("<h1>Störung</h1><p>Text</p><h4>Klein</h4>", "# Störung\n\nText\n\n### Klein")]
    [InlineData("<blockquote><p>Zitiert</p></blockquote><p>Danach</p>", "> Zitiert\n\nDanach")]
    [InlineData("<pre>Zeile 1\n  eingerückt *stern*</pre>", "```\nZeile 1\n  eingerückt *stern*\n```")]
    [InlineData("<ol start=\"3\"><li>Drei</li><li>Vier<ul><li>Unterpunkt</li></ul></li></ol>", "3. Drei\n4. Vier\n   - Unterpunkt")]
    [InlineData("Vorher<hr>Nachher", "Vorher\n\n---\n\nNachher")]
    public void Gaengige_Strukturen_werden_gewandelt(string html, string erwartet)
    {
        Assert.Equal(erwartet, HtmlZuMarkdown.Wandeln(html));
    }

    [Theory]
    [InlineData("<b>Wichtig: </b>bitte lesen", "**Wichtig:** bitte lesen")]
    [InlineData("<b>Hinweis:</b>Text", "**Hinweis:** Text")]
    [InlineData("Wort<b>(fett)</b>", "Wort **(fett)**")]
    [InlineData("<strong><em>beides</em></strong>", "***beides***")]
    [InlineData("<b> </b>nichts", "nichts")]
    [InlineData("<b>vor <i>mitte</i> nach</b>", "**vor *mitte* nach**")]
    [InlineData("<b><p>eins</p><p>zwei</p></b>", "**eins**\n\n**zwei**")]
    [InlineData("<i>offen ohne Ende", "*offen ohne Ende*")]
    [InlineData("<b>fett</i></b></b> danach", "**fett** danach")]
    public void Fett_und_kursiv_werden_so_gesetzt_dass_Markdown_sie_erkennt(string html, string erwartet)
    {
        var markdown = HtmlZuMarkdown.Wandeln(html);

        Assert.Equal(erwartet, markdown);
        Assert.DoesNotContain("*", System.Text.RegularExpressions.Regex.Replace(Html(markdown), "<[^>]+>", ""));
    }

    [Theory]
    [InlineData("<p>2 * 3 * 4 = 24</p>", "2 * 3 * 4 = 24")]
    [InlineData("<p>Datei snake_case_name und _unterstrichen_</p>", "Datei snake_case_name und _unterstrichen_")]
    [InlineData("<p>Befehl `dir` eingeben</p>", "Befehl `dir` eingeben")]
    [InlineData("<p># keine Überschrift</p>", "# keine Überschrift")]
    [InlineData("<p>- kein Listenpunkt</p>", "- kein Listenpunkt")]
    [InlineData("<p>3. OG links</p>", "3. OG links")]
    [InlineData("<p>&gt; kein Zitat</p>", "> kein Zitat")]
    [InlineData("<p>Pfad \\\\server\\freigabe\\_ordner</p>", "Pfad \\\\server\\freigabe\\_ordner")]
    [InlineData("<p>Text [x](y) kein Link</p>", "Text [x](y) kein Link")]
    [InlineData("<p>Zeichen &amp;amp; bleibt</p>", "Zeichen &amp; bleibt")]
    [InlineData("<p>Titel</p><p>-----</p>", "Titel\n-----")]
    public void Zeichen_mit_Bedeutung_in_Markdown_bleiben_gewoehnlicher_Text(string html, string sichtbar)
    {
        var angezeigt = Html(HtmlZuMarkdown.Wandeln(html));

        Assert.DoesNotContain("<em>", angezeigt);
        Assert.DoesNotContain("<code>", angezeigt);
        Assert.DoesNotContain("<h1", angezeigt);
        Assert.DoesNotContain("<h2", angezeigt);
        Assert.DoesNotContain("<li>", angezeigt);
        Assert.DoesNotContain("<blockquote>", angezeigt);
        Assert.DoesNotContain("<a ", angezeigt);
        Assert.DoesNotContain("<hr", angezeigt);
        var text = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(angezeigt, "<br />", "\n").Replace("</p>\n<p>", "\n"));
        Assert.Equal(sichtbar, System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "").Trim());
    }

    [Theory]
    [InlineData("<a href=\"javascript:alert(1)\">klick</a>", "klick")]
    [InlineData("<a href=\"/intern\">klick</a>", "klick")]
    [InlineData("<a>ohne Ziel</a>", "ohne Ziel")]
    [InlineData("<a href=\"https://example.org/a\">https://example.org/a</a>", "https://example.org/a")]
    [InlineData("<a href=\"mailto:hotline@example.org\">hotline@example.org</a>", "hotline@example.org")]
    [InlineData("Siehe <a href=\"https://example.org/a (1)\">hier</a>.", "Siehe [hier](https://example.org/a%20%281%29).")]
    [InlineData("<a href=\"https://example.org\"><b>fett</b> verlinkt</a>", "[**fett** verlinkt](https://example.org)")]
    public void Links_werden_nur_mit_erlaubter_Adresse_uebernommen(string html, string erwartet)
    {
        Assert.Equal(erwartet, HtmlZuMarkdown.Wandeln(html));
    }

    [Fact]
    public void Eine_kleine_einfache_Tabelle_wird_eine_Tabelle()
    {
        var markdown = HtmlZuMarkdown.Wandeln("""
            <p class=MsoNormal>Betroffene Geräte:</p>
            <table class=MsoTableGrid border=1 cellspacing=0 cellpadding=0>
             <tr>
              <td width=100 valign=top><p class=MsoNormal><b>Gerät</b><o:p></o:p></p></td>
              <td width=100 valign=top><p class=MsoNormal><b>Raum</b><o:p></o:p></p></td>
              <td width=100 valign=top><p class=MsoNormal><b>Fehler</b><o:p></o:p></p></td>
             </tr>
             <tr>
              <td><p class=MsoNormal>NB-1017<o:p></o:p></p></td>
              <td><p class=MsoNormal>2.14<o:p></o:p></p></td>
              <td><p class=MsoNormal>startet nicht | hängt<o:p></o:p></p></td>
             </tr>
             <tr>
              <td><p class=MsoNormal>NB-1018</p></td>
              <td><p class=MsoNormal><o:p>&nbsp;</o:p></p></td>
              <td><p class=MsoNormal>Akku</p></td>
             </tr>
            </table>
            <p class=MsoNormal>Danke.</p>
            """);

        Assert.Equal("""
            Betroffene Geräte:

            | **Gerät** | **Raum** | **Fehler** |
            | --- | --- | --- |
            | NB-1017 | 2.14 | startet nicht &#124; hängt |
            | NB-1018 |  | Akku |

            Danke.
            """.ReplaceLineEndings("\n"), markdown);

        var html = Html(markdown);
        Assert.Contains("<table>", html);
        Assert.Contains("<th><strong>Gerät</strong></th>", html);
        Assert.Contains("<td>startet nicht | hängt</td>", html);
    }

    [Theory]
    // eine Zeile: Layout einer Signatur
    [InlineData("<table><tr><td>Logo</td><td>Gabi Müller<br>Tel. 2202</td></tr></table>", "Logo\nGabi Müller\nTel. 2202")]
    // eine Spalte
    [InlineData("<table><tr><td>A</td></tr><tr><td>B</td></tr></table>", "A\n\nB")]
    // verbundene Zellen
    [InlineData("<table><tr><td colspan=2>Titel</td></tr><tr><td>A</td><td>B</td></tr></table>", "Titel\n\nA\nB")]
    // Tabelle in einer Tabelle
    [InlineData("<table><tr><td>A</td><td><table><tr><td>innen 1</td><td>innen 2</td></tr></table></td></tr><tr><td>C</td><td>D</td></tr></table>", "A\ninnen 1\ninnen 2\n\nC\nD")]
    // ungleiche Zeilen
    [InlineData("<table><tr><td>A</td><td>B</td></tr><tr><td>C</td></tr></table>", "A\nB\n\nC")]
    // eine Liste in einer Zelle
    [InlineData("<table><tr><td>A</td><td>B</td></tr><tr><td><ul><li>x</li></ul></td><td>D</td></tr></table>", "A\nB\n\n- x\nD")]
    public void Alles_andere_als_eine_einfache_Tabelle_wird_zu_Text(string html, string erwartet)
    {
        var markdown = HtmlZuMarkdown.Wandeln(html);

        Assert.Equal(erwartet, markdown);
        Assert.DoesNotContain("<table>", Html(markdown));
    }

    [Fact]
    public void Eine_zu_breite_Tabelle_wird_zu_Text()
    {
        var zeile = "<tr>" + string.Concat(Enumerable.Range(1, 7).Select(i => $"<td>S{i}</td>")) + "</tr>";

        var markdown = HtmlZuMarkdown.Wandeln($"<table>{zeile}{zeile}</table>");

        Assert.DoesNotContain("|", markdown);
        Assert.StartsWith("S1\nS2", markdown);
    }

    [Theory]
    [InlineData("<script>alert(1)</script><style>p{color:red}</style><p>Sichtbar</p><!-- Kommentar -->", "Sichtbar")]
    [InlineData("<p>Offen <b>fett <table><tr><td>Zelle", "Offen **fett**\n\n**Zelle**")]
    [InlineData("</p></div></table></b>Trotzdem", "Trotzdem")]
    [InlineData("<unbekannt attr='x'>Text <v:shape>in</v:shape> Tags</unbekannt>", "Text in Tags")]
    [InlineData("", "")]
    [InlineData("<p>&nbsp;</p><p><br></p>", "")]
    public void Kaputtes_und_unbekanntes_HTML_stoert_nicht(string html, string erwartet)
    {
        Assert.Equal(erwartet, HtmlZuMarkdown.Wandeln(html));
    }

    [Fact]
    public void Ein_umgekehrter_Schraegstrich_bleibt_in_der_Anzeige_erhalten()
    {
        var html = Html(@"Bitte \\server\freigabe\*.log prüfen und C:\Temp\_alt löschen.");

        Assert.Contains(@"\\server\freigabe\*.log", html);
        Assert.Contains(@"C:\Temp\_alt", html);
    }

    [Fact]
    public void Auch_getippte_Tabellen_werden_angezeigt()
    {
        var html = Html("| Gerät | Raum |\n| --- | --- |\n| NB-1017 | 2.14 |");

        Assert.Contains("<th>Gerät</th>", html);
        Assert.Contains("<td>2.14</td>", html);
    }

    private static readonly byte[] Bildschirmfoto = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[30_000]];
    private static readonly byte[] Logo = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[2_000]];

    [Fact]
    public async Task Ein_Bildschirmfoto_im_Text_der_Mail_steht_im_Auftrag_an_seiner_Stelle()
    {
        var mail = Mail("gabi.mueller@example.org", "Fehlermeldung beim Drucken",
            html: """
                <p class=MsoNormal>Guten Tag,</p>
                <p class=MsoNormal>beim Drucken erscheint <b>diese Meldung</b>:</p>
                <p class=MsoNormal><img width=600 height=300 id="Grafik 1" src="cid:image001.png@01DC5A11.4F2B9E70" alt="Fehlermeldung"></p>
                <p class=MsoNormal>Danach geht nichts mehr.</p>
                <p class=MsoNormal><img src="cid:image002.png@01DC5A11.4F2B9E70" alt="Logo der Behörde"></p>
                """,
            text: "Guten Tag,\nbeim Drucken erscheint diese Meldung:\n[cid:image001.png@01DC5A11.4F2B9E70]\nDanach geht nichts mehr.",
            ("image001.png", "image001.png@01DC5A11.4F2B9E70", Bildschirmfoto),
            ("image002.png", "image002.png@01DC5A11.4F2B9E70", Logo));

        var eingang = await AnnehmenAsync(mail);

        var id = eingang.AuftragId!.Value;
        var auftrag = await LadenAsync(id);
        var bild = Assert.Single(await AnhaengeAsync(id));
        Assert.Equal(("image001.png", "image/png"), (bild.Dateiname, bild.Bildtyp));
        var kennung = bild.Ablagename.Split('/')[^1];
        Assert.Equal(
            $"Guten Tag,\nbeim Drucken erscheint **diese Meldung**:\n![Fehlermeldung](anhang:{kennung})\nDanach geht nichts mehr.",
            auftrag.Beschreibung);

        // Angezeigt wird es einmal, im Text zwischen den beiden Sätzen, nicht noch einmal bei den Anhängen.
        var anna = await app.AnmeldenAlsAsync("anna");
        var seite = await anna.GetStringAsync($"/Auftraege/{id}");
        var bildImText = $"<a href=\"/Anhaenge/{bild.Id}?bild=true\"";
        Assert.Contains(bildImText, seite);
        Assert.Contains($"<img src=\"/Anhaenge/{bild.Id}?bild=true\" alt=\"Fehlermeldung\" />", seite);
        Assert.True(seite.IndexOf("diese Meldung", StringComparison.Ordinal) < seite.IndexOf(bildImText, StringComparison.Ordinal) && seite.IndexOf(bildImText, StringComparison.Ordinal) < seite.IndexOf("Danach geht nichts mehr.", StringComparison.Ordinal));
        Assert.DoesNotContain("<ul class=\"bilder\">", seite);
        Assert.DoesNotContain("cid:", seite);

        // In der Bestätigung an die Kundin und in der Suche stört der Verweis nicht.
        Assert.Contains("Fehlermeldung beim Drucken", await anna.GetStringAsync("/Auftraege?q=geht+nichts+mehr"));
    }

    [Fact]
    public async Task Bilder_aus_dem_zitierten_Verlauf_einer_Antwort_werden_nicht_noch_einmal_angehaengt()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = (await AnnehmenAsync(Mail("dieter.kunde@example.org", "Bildschirm flackert", html: "<p>Der Bildschirm flackert.</p>", text: "Der Bildschirm flackert."))).AuftragId!.Value;
        await anna.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=Uebernehmen", new() { ["version"] = "1" });
        await app.MailsVersendenAsync();

        var antwort = Mail("dieter.kunde@example.org", $"AW: [AVIS-{id}] Nachricht: Bildschirm flackert",
            html: """
                <p class=MsoNormal>So sieht es <i>jetzt</i> aus:</p>
                <p class=MsoNormal><img src="cid:neu.png@1" alt="Aktuell"></p>
                <p class=MsoNormal><o:p>&nbsp;</o:p></p>
                <div style='border:none;border-top:solid #E1E1E1 1.0pt;padding:3.0pt 0cm 0cm 0cm'>
                <p class=MsoNormal><b>Von:</b> IT-Hotline &lt;it-hotline@avis.test&gt;<br><b>Gesendet:</b> Freitag, 13. November 2026 10:00<br><b>An:</b> Dieter Kunde<br><b>Betreff:</b> [AVIS-1] Nachricht</p>
                </div>
                <p class=MsoNormal>Bitte schicken Sie ein Foto.</p>
                <p class=MsoNormal><img src="cid:alt.png@1" alt="Altes Bild"></p>
                """,
            text: "So sieht es jetzt aus:\n[cid:neu.png@1]\n\nVon: IT-Hotline\nGesendet: Freitag\n\nBitte schicken Sie ein Foto.",
            ("neu.png", "neu.png@1", Bildschirmfoto),
            ("alt.png", "alt.png@1", Bildschirmfoto));

        var eingang = await AnnehmenAsync(antwort);

        Assert.Equal(MaileingangErgebnis.Antwort, eingang.Ergebnis);
        var eintrag = (await LadenAsync(id)).Verlauf.OrderBy(v => v.Id).Last();
        var anhaenge = (await AnhaengeAsync(id)).Where(a => a.VerlaufseintragId == eintrag.Id).ToList();
        var bild = Assert.Single(anhaenge);
        Assert.Equal("neu.png", bild.Dateiname);
        Assert.Equal($"So sieht es *jetzt* aus:\n![Aktuell](anhang:{bild.Ablagename.Split('/')[^1]})", eintrag.Text);

        // Die Mail an die Bearbeiterin nennt das Bild beim Namen statt mit dem Verweis.
        var hinweis = Assert.Single(await app.MailsVersendenAsync());
        // Die Mail-Bibliothek gibt den Text mit den Zeilenenden des Rechners zurück.
        Assert.Contains("So sieht es *jetzt* aus:\n[Bild: Aktuell]", hinweis.TextBody?.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("anhang:", hinweis.TextBody);
    }

    [Fact]
    public async Task Der_Kunde_sieht_das_Bild_im_Text_ein_Fremder_bekommt_es_nicht()
    {
        var id = (await AnnehmenAsync(Mail("dieter.kunde@example.org", "Mit Bild für den Kunden",
            html: "<p>Siehe:</p><p><img src=\"cid:b@1\"></p>", text: "Siehe:",
            ("foto.png", "b@1", Bildschirmfoto)))).AuftragId!.Value;
        var bild = Assert.Single(await AnhaengeAsync(id));

        var dieter = await app.AnmeldenAlsAsync("dieter");
        var sicht = await dieter.GetStringAsync($"/Meine/{id}");
        Assert.Contains($"<img src=\"/Anhaenge/{bild.Id}?bild=true\" alt=\"foto.png\" />", sicht);
        Assert.DoesNotContain("<ul class=\"bilder\">", sicht);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await dieter.GetAsync($"/Anhaenge/{bild.Id}?bild=true")).StatusCode);

        var fremd = await app.AnmeldenAlsNeuAsync("fremder.kunde");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await fremd.GetAsync($"/Anhaenge/{bild.Id}?bild=true")).StatusCode);
    }

    [Fact]
    public async Task Ein_Bild_im_Text_wird_auch_erkannt_wenn_das_Mailprogramm_es_als_Anhang_kennzeichnet()
    {
        var mail = Mail("gabi.mueller@example.org", "Bild als Anhang gekennzeichnet",
            html: "<p>Vorher</p><p><img src=\"cid:foto@1\" alt=\"Foto\"></p><p><img src=\"cid:logo@1\"></p><p>Nachher</p>", text: "Vorher\nNachher",
            ("foto.png", "foto@1", Bildschirmfoto), ("logo.png", "logo@1", Logo));
        foreach (var teil in mail.BodyParts.OfType<MimePart>().Where(t => t.ContentType.IsMimeType("image", "*")))
            teil.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = teil.FileName };

        var id = (await AnnehmenAsync(mail)).AuftragId!.Value;

        var bild = Assert.Single(await AnhaengeAsync(id));
        Assert.Equal($"Vorher\n\n![Foto](anhang:{bild.Ablagename.Split('/')[^1]})\n\nNachher", (await LadenAsync(id)).Beschreibung);
    }

    [Fact]
    public void Ein_Verweis_auf_ein_Bild_das_nicht_zum_Eintrag_gehoert_zeigt_nur_den_Namen()
    {
        var html = Html("Vorher ![Geheim](anhang:0123456789abcdef0123456789abcdef) nachher");

        Assert.DoesNotContain("<img", html);
        Assert.Contains("[Bild: Geheim]", html);
    }

    [Fact]
    public async Task Fehlt_dem_HTML_Teil_der_Inhalt_gilt_der_Nur_Text_Teil()
    {
        var eingang = await AnnehmenAsync(Mail("gabi.mueller@example.org", "HTML fast leer",
            html: "<html><body><p>&nbsp;</p><img src=\"https://example.org/zaehlpixel.gif\"></body></html>",
            text: "Der eigentliche Text steht nur hier.\nZweite Zeile."));

        Assert.Equal("Der eigentliche Text steht nur hier.\nZweite Zeile.", (await LadenAsync(eingang.AuftragId!.Value)).Beschreibung);
    }

    private static MimeMessage Mail(string von, string betreff, string html, string text, params (string Name, string Kennung, byte[] Inhalt)[] bilder)
    {
        var inhalt = new BodyBuilder { TextBody = text, HtmlBody = html };
        foreach (var (name, kennung, bytes) in bilder)
            inhalt.LinkedResources.Add(name, bytes, new ContentType("image", "png")).ContentId = kennung;

        var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(von));
        mail.To.Add(MailboxAddress.Parse("it-hotline@avis.test"));
        mail.Subject = betreff;
        mail.Body = inhalt.ToMessageBody();
        mail.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId("example.org");
        return mail;
    }

    private async Task<Maileingang> AnnehmenAsync(MimeMessage mail)
    {
        // Wie nach dem Abruf: Die Mail wird einmal in ihre Rohform und zurück gewandelt,
        // mit den Zeilenenden, die eine Mail unterwegs immer hat – auf jedem Rechner.
        var unterwegs = FormatOptions.Default.Clone();
        unterwegs.NewLineFormat = NewLineFormat.Dos;
        using var roh = new MemoryStream();
        await mail.WriteToAsync(unterwegs, roh);
        roh.Position = 0;
        var gelesen = await MimeMessage.LoadAsync(roh);

        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Mailannahme>().AnnehmenAsync(gelesen);
    }

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));

    private Task<List<Anhang>> AnhaengeAsync(int auftragId) => app.DatenbankAsync(db => db.Anhaenge
        .AsNoTracking()
        .Where(a => a.Verlaufseintrag.AuftragId == auftragId)
        .OrderBy(a => a.Id)
        .ToListAsync());
}
