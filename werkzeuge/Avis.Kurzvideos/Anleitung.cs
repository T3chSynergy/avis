using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace Avis.Kurzvideos;

/// <summary>
/// Baut die Kurzanleitung für Bearbeiter aus Kurzanleitung.md: als Webseite und, von dort gedruckt, als PDF. Die
/// Bilder dazu nimmt <see cref="Bildschirmfotos"/> auf.
/// </summary>
internal static partial class Anleitung
{
    public const string Seitenname = "kurzanleitung.html";
    public const string Pdf = "AVIS-Kurzanleitung-Bearbeiter.pdf";
    private const string Titel = "AVIS – Kurzanleitung für Bearbeiter";

    public static async Task BauenAsync(Kamera kamera, Optionen optionen, IReadOnlyList<Drehbuch> drehbuecher)
    {
        await new Bildschirmfotos(kamera, Path.Combine(optionen.Ziel, "bilder")).AufnehmenAsync();
        File.WriteAllText(Path.Combine(optionen.Ziel, "bilder", "ablauf.svg"), Quelle.Lesen("Ablauf.svg"));

        var seite = Path.Combine(optionen.Ziel, Seitenname);
        File.WriteAllText(seite, Html(optionen, drehbuecher, DateTime.Today));
        await kamera.PdfAsync(seite, Path.Combine(optionen.Ziel, Pdf), $"{Titel} · AVIS {optionen.StandOderEntwicklung}");
        Console.WriteLine($"Kurzanleitung: {seite}");
    }

    private static string Html(Optionen optionen, IReadOnlyList<Drehbuch> drehbuecher, DateTime heute)
    {
        var deutsch = CultureInfo.GetCultureInfo("de-DE");
        var markdown = Kommentar().Replace(Quelle.Lesen("Kurzanleitung.md"), "")
            .Replace("{{stand}}", optionen.StandOderEntwicklung, StringComparison.Ordinal)
            .Replace("{{datum}}", heute.ToString("d. MMMM yyyy", deutsch), StringComparison.Ordinal);
        var pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseGenericAttributes().UseAutoIdentifiers().UseTaskLists().Build();
        var text = Markdown.ToHtml(markdown, pipeline);

        // Abbildungen: Ein Absatz nur aus Bildern bekommt eine Nummer und den Text des ersten Bildes.
        var nummern = new Dictionary<string, int>();
        text = Bildabsatz().Replace(text, absatz =>
        {
            var bilder = Bild().Matches(absatz.Value);
            var nummer = nummern.Count + 1;
            foreach (Match bild in bilder)
            {
                nummern[Path.GetFileName(bild.Groups["datei"].Value)] = nummer;
            }

            var unterschrift = Alt().Match(bilder[0].Value).Groups["text"].Value;
            var art = bilder.Count > 1 ? " class=\"paar\"" : "";
            return $"<figure{art}><div>{string.Concat(bilder.Select(b => b.Value))}</div>" +
                $"<figcaption><strong>Abb. {nummer}:</strong> {unterschrift}</figcaption></figure>";
        });
        text = Abbildung().Replace(text, m => nummern.TryGetValue(m.Groups["datei"].Value, out var nummer)
            ? nummer.ToString(CultureInfo.InvariantCulture)
            : throw new InvalidOperationException($"Die Kurzanleitung nennt die Abbildung {m.Groups["datei"].Value}, die es nicht gibt."));

        // Verweise auf die Videos. Sie nennen die volle Adresse, damit sie auch aus dem PDF heraus stimmen.
        string Adresse(int nummer) => string.Create(CultureInfo.InvariantCulture, $"{Seite.Adresse}#video-{nummer:00}");
        text = Video().Replace(text, m =>
        {
            var nummer = int.Parse(m.Groups["nr"].Value, CultureInfo.InvariantCulture);
            return $"<p class=\"video\"><a href=\"{Adresse(nummer)}\">Kurzvideo: {WebUtility.HtmlEncode(drehbuecher[nummer - 1].Titel)}</a></p>";
        });
        var videos = new StringBuilder("<table class=\"videos\"><thead><tr><th>Video</th><th>Worum es geht</th></tr></thead><tbody>\n");
        foreach (var (drehbuch, nummer) in drehbuecher.Select((d, i) => (d, i + 1)))
        {
            videos.Append(CultureInfo.InvariantCulture,
                $"<tr><td><a href=\"{Adresse(nummer)}\">{nummer}. {WebUtility.HtmlEncode(drehbuch.Titel)}</a></td><td>{WebUtility.HtmlEncode(drehbuch.Worum)}</td></tr>\n");
        }

        text = text.Replace("<p>{{videos}}</p>", videos.Append("</tbody></table>").ToString(), StringComparison.Ordinal);

        // Inhaltsverzeichnis aus den Überschriften der zweiten Ebene
        var inhalt = new StringBuilder("<nav class=\"inhalt\" aria-label=\"Inhalt\"><h2>Inhalt</h2><ol>\n");
        foreach (Match kapitel in Kapitel().Matches(text))
        {
            inhalt.Append(CultureInfo.InvariantCulture, $"<li><a href=\"#{kapitel.Groups["id"].Value}\">{kapitel.Groups["text"].Value}</a></li>\n");
        }

        text = text.Replace("<p>{{inhalt}}</p>", inhalt.Append("</ol></nav>").ToString(), StringComparison.Ordinal);
        if (text.Contains("{{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("In der Kurzanleitung ist ein Platzhalter stehen geblieben.");
        }

        return $$"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{Titel}}</title>
            <link rel="icon" href="favicon.ico">
            <style>
            {{Quelle.Lesen("Seite.css")}}
            {{Quelle.Lesen("Anleitung.css")}}</style>
            </head>
            <body class="anleitung">
            {{Seite.Kopf("Kurzanleitung")}}
            <main>
            <p class="herunterladen"><a href="{{Pdf}}">Als PDF herunterladen oder drucken</a></p>
            {{text}}
            </main>
            {{Seite.Fuss(optionen, heute)}}
            </body>
            </html>

            """;
    }

    [GeneratedRegex(@"<!--.*?-->\s*", RegexOptions.Singleline)]
    private static partial Regex Kommentar();

    [GeneratedRegex(@"<p>(?:<img [^>]*>\s*)+</p>")]
    private static partial Regex Bildabsatz();

    [GeneratedRegex(@"<img src=""(?<datei>[^""]+)""[^>]*>")]
    private static partial Regex Bild();

    [GeneratedRegex(@"alt=""(?<text>[^""]*)""")]
    private static partial Regex Alt();

    [GeneratedRegex(@"\{\{abb:(?<datei>[^}]+)\}\}")]
    private static partial Regex Abbildung();

    [GeneratedRegex(@"<p>\{\{video:(?<nr>\d+)\}\}</p>")]
    private static partial Regex Video();

    [GeneratedRegex(@"<h2 id=""(?<id>[^""]+)""[^>]*>(?<text>.*?)</h2>")]
    private static partial Regex Kapitel();
}
