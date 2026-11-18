using System.Globalization;
using System.Net;
using System.Text;

namespace Avis.Kurzvideos;

/// <summary>Baut die Seite, auf der die Videos veröffentlicht werden: eine einzige Datei ohne Skripte.</summary>
internal static class Seite
{
    /// <summary>Wo die Seite veröffentlicht wird. Verweise aus dem PDF brauchen die volle Adresse.</summary>
    public const string Adresse = "https://t3chsynergy.github.io/avis/";

    private const string Quelltext = "https://github.com/T3chSynergy/avis";

    public static void Schreiben(Optionen optionen, IReadOnlyList<Aufnahme> aufnahmen)
    {
        File.WriteAllText(Path.Combine(optionen.Ziel, "index.html"), Html(optionen, aufnahmen, DateTime.Today));
        using var bild = typeof(Seite).Assembly.GetManifestResourceStream("favicon.ico")!;
        using var datei = File.Create(Path.Combine(optionen.Ziel, "favicon.ico"));
        bild.CopyTo(datei);
    }

    private static string Html(Optionen optionen, IReadOnlyList<Aufnahme> aufnahmen, DateTime heute)
    {
        var deutsch = CultureInfo.GetCultureInfo("de-DE");
        var gesamt = Math.Max(1, (int)Math.Round(aufnahmen.Sum(a => a.Laenge) / 60));
        var html = new StringBuilder();
        html.Append(deutsch, $$"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>AVIS – Kurzvideos</title>
            <link rel="icon" href="favicon.ico">
            <style>
            {{Quelle.Lesen("Seite.css")}}</style>
            </head>
            <body>
            {{Kopf("Kurzvideos")}}
            <main>
            <h1>AVIS in Kurzvideos</h1>
            <p>AVIS ist ein schlankes Ticketsystem für die IT. Diese {{aufnahmen.Count}} kurzen Videos zeigen, wie Bearbeiter
            damit arbeiten: jedes zu einem Thema, zusammen rund {{gesamt}} Minuten. Zum Nachlesen gibt es die
            <a href="{{Anleitung.Seitenname}}">Kurzanleitung für Bearbeiter</a>, auch als
            <a href="{{Anleitung.Pdf}}">PDF</a>, und die Hilfe, die in AVIS auf jeder Seite oben erreichbar ist.</p>
            <p class="hinweis"><strong>Mit KI erstellt.</strong> {{KiHinweis(optionen)}}
            Alle gezeigten Namen und Daten sind erfunden.</p>
            <ol class="inhalt">

            """);
        foreach (var a in aufnahmen)
        {
            html.Append(deutsch, $"""
                <li><a href="#{Anker(a)}">{Text(a.Drehbuch.Titel)}</a> <span class="dauer">{Dauer(a.Laenge)} min</span></li>

                """);
        }

        html.Append("</ol>\n");
        foreach (var (a, nummer) in aufnahmen.Select((a, i) => (a, i + 1)))
        {
            var name = Path.GetFileNameWithoutExtension(a.Datei);
            html.Append(deutsch, $"""
                <section id="{Anker(a)}">
                <h2>{nummer}. {Text(a.Drehbuch.Titel)}</h2>
                <p>{Text(a.Drehbuch.Worum)} <span class="dauer">{Dauer(a.Laenge)} min</span></p>
                <video controls preload="none" width="{Kamera.Breite}" height="{Kamera.Hoehe}" poster="{name}.jpg">
                <source src="{a.Datei}" type="video/mp4">
                <track kind="subtitles" srclang="de" label="Deutsch" src="{name}.vtt">
                </video>
                </section>

                """);
        }

        html.Append(deutsch, $"""
            </main>
            {Fuss(optionen, heute)}
            </body>
            </html>

            """);
        return html.ToString();
    }

    /// <summary>Der Kopf beider Seiten, mit dem Wechsel zwischen Videos und Kurzanleitung.</summary>
    public static string Kopf(string hier)
    {
        string Verweis(string name, string ziel) =>
            $"<a href=\"{ziel}\"{(name == hier ? " aria-current=\"page\"" : "")}>{name}</a>";
        return $"<header><div><strong>AVIS</strong> {Verweis("Kurzvideos", "./")} {Verweis("Kurzanleitung", Anleitung.Seitenname)}</div></header>";
    }

    public static string Fuss(Optionen optionen, DateTime heute) => string.Create(CultureInfo.GetCultureInfo("de-DE"),
        $"<footer>Aufgenommen am {heute:d. MMMM yyyy} mit AVIS {Text(optionen.StandOderEntwicklung)}. " +
        $"Quelltext und Dokumentation: <a href=\"{Quelltext}\">AVIS auf GitHub</a>.</footer>");

    private static string KiHinweis(Optionen optionen) => optionen.MitTon
        ? "Die Videos wurden durch KI vertont und bearbeitet: Die Stimme ist künstlich erzeugt, Aufnahme und Schnitt laufen "
          + "automatisch nach einem Drehbuch. Betonung und Aussprache sind deshalb nicht überall richtig. Was gesprochen wird, "
          + "lässt sich als Untertitel einblenden."
        : "Die Videos wurden durch KI bearbeitet: Aufnahme und Schnitt laufen automatisch nach einem Drehbuch. Diese Fassung "
          + "hat keinen Ton; was sonst gesprochen wird, lässt sich als Untertitel einblenden.";

    private static string Anker(Aufnahme a) => "video-" + Path.GetFileNameWithoutExtension(a.Datei)[..2];

    private static string Dauer(double sekunden) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)sekunden / 60}:{(int)sekunden % 60:00}");

    private static string Text(string text) => WebUtility.HtmlEncode(text);
}
