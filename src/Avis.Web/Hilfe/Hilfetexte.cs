using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Html;

namespace Avis.Web.Hilfe;

/// <summary>Ein Hilfetext, fertig für die Anzeige.</summary>
/// <param name="Titel">Die erste Überschrift des Textes.</param>
/// <param name="Abschnitte">Die Abschnitte in ihrer Reihenfolge, für das Inhaltsverzeichnis und die Verweise der Seiten.</param>
public record Hilfeseite(string Titel, IHtmlContent Inhalt, IReadOnlyList<Hilfeabschnitt> Abschnitte);

/// <param name="Anker">Die Sprungmarke des Abschnitts, aus seiner Überschrift gebildet.</param>
public record Hilfeabschnitt(string Anker, string Titel);

/// <summary>
/// Liest die Hilfetexte und zeigt sie als HTML. Sie liegen als
/// Markdown-Dateien im Ordner "Hilfe" der Anwendung: eine Quelle, die auch
/// ohne AVIS lesbar ist und sich ohne Programmieren ändern lässt.
///
/// Dazu kommt, wenn es sie gibt, eine Datei mit eigenem Text des Hauses, etwa
/// Telefonnummer und Zeiten der Hotline. Sie liegt nur auf dem Server und
/// gehört nicht zur Anwendung.
/// </summary>
public class Hilfetexte(IWebHostEnvironment umgebung, IConfiguration konfiguration)
{
    public const string Bearbeiter = "bearbeiter";
    public const string Kunden = "kunden";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseAutoLinks()
        .Build();

    // Die Texte ändern sich im Betrieb nicht: einmal lesen genügt. Der eigene Text des Hauses wird
    // dagegen bei jedem Aufruf gelesen, damit eine Änderung ohne Neustart zu sehen ist.
    private readonly ConcurrentDictionary<string, Hilfeseite> _gelesen = new();

    /// <summary>Die Hilfe zu einem Thema: <see cref="Bearbeiter"/> oder <see cref="Kunden"/>.</summary>
    public Hilfeseite Lesen(string thema)
    {
        if (thema is not (Bearbeiter or Kunden))
            throw new ArgumentException($"Zu \"{thema}\" gibt es keine Hilfe.", nameof(thema));

        return _gelesen.GetOrAdd(thema, t => Aufbereiten(File.ReadAllText(Path.Combine(umgebung.ContentRootPath, "Hilfe", t + ".md"))));
    }

    /// <summary>
    /// Der eigene Text des Hauses, oder null, wenn es keinen gibt. Wo die
    /// Datei liegt, sagt die Einstellung Hilfe:EigenerText.
    /// </summary>
    public IHtmlContent? EigenerText()
    {
        var pfad = konfiguration["Hilfe:EigenerText"];
        if (string.IsNullOrWhiteSpace(pfad))
            return null;

        var datei = Path.IsPathRooted(pfad) ? pfad : Path.Combine(umgebung.ContentRootPath, pfad);
        if (!File.Exists(datei))
            return null;

        var text = File.ReadAllText(datei);
        return string.IsNullOrWhiteSpace(text) ? null : Aufbereiten(text).Inhalt;
    }

    /// <summary>
    /// Wandelt Markdown in HTML um und gibt jeder Überschrift eine
    /// Sprungmarke, die sich aus ihrem Text ergibt. Die erste Überschrift
    /// der ersten Ebene wird der Titel der Seite und steht nicht im Inhalt.
    /// </summary>
    public static Hilfeseite Aufbereiten(string markdown)
    {
        var dokument = Markdown.Parse(markdown, Pipeline);
        var titel = "";
        var abschnitte = new List<Hilfeabschnitt>();
        var vergeben = new HashSet<string>();

        foreach (var ueberschrift in dokument.Descendants<HeadingBlock>().ToList())
        {
            var text = Text(ueberschrift.Inline);
            if (ueberschrift.Level == 1 && titel.Length == 0)
            {
                titel = text;
                dokument.Remove(ueberschrift);
                continue;
            }

            // Zwei gleichlautende Überschriften bekommen verschiedene Marken.
            var anker = Anker(text);
            for (var n = 2; !vergeben.Add(anker); n++)
                anker = $"{Anker(text)}-{n}";

            ueberschrift.GetAttributes().Id = anker;
            if (ueberschrift.Level == 2)
                abschnitte.Add(new Hilfeabschnitt(anker, text));
        }

        return new Hilfeseite(titel, new HtmlString(dokument.ToHtml(Pipeline)), abschnitte);
    }

    /// <summary>
    /// Die Sprungmarke zu einer Überschrift: Kleinbuchstaben, Umlaute
    /// umschrieben, alles andere wird ein Bindestrich. Aus "Geräte nennen"
    /// wird "geraete-nennen".
    /// </summary>
    public static string Anker(string ueberschrift)
    {
        var marke = new StringBuilder();
        foreach (var zeichen in ueberschrift.Trim().ToLowerInvariant())
        {
            marke.Append(zeichen switch
            {
                'ä' => "ae",
                'ö' => "oe",
                'ü' => "ue",
                'ß' => "ss",
                >= 'a' and <= 'z' or >= '0' and <= '9' => zeichen.ToString(CultureInfo.InvariantCulture),
                _ => "-",
            });
        }

        var teile = marke.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries);
        return teile.Length == 0 ? "abschnitt" : string.Join('-', teile);
    }

    private static string Text(ContainerInline? inhalt) =>
        inhalt is null ? "" : string.Concat(inhalt.Descendants<LiteralInline>().Select(l => l.Content.ToString())).Trim();
}

/// <summary>
/// Welcher Abschnitt der Hilfe zu welcher Seite gehört. Der Eintrag "Hilfe"
/// im Kopf führt von jeder Seite an die passende Stelle. Ein Test prüft, dass
/// es jeden hier genannten Abschnitt in den Hilfetexten gibt.
/// </summary>
public static class Hilfeverweise
{
    /// <summary>Seite (wie in den Routen, etwa "/Auftraege/Neu") zu Thema und Sprungmarke.</summary>
    public static readonly IReadOnlyDictionary<string, (string Thema, string Anker)> Seiten = new Dictionary<string, (string, string)>
    {
        ["/Index"] = (Hilfetexte.Bearbeiter, "die-listen"),
        ["/Gruppen/Index"] = (Hilfetexte.Bearbeiter, "die-listen"),
        ["/Gruppen/Meine"] = (Hilfetexte.Bearbeiter, "meine-gruppen"),
        ["/Auftraege/Index"] = (Hilfetexte.Bearbeiter, "suchen"),
        ["/Auftraege/Neu"] = (Hilfetexte.Bearbeiter, "auftrag-erfassen"),
        ["/Auftraege/Details"] = (Hilfetexte.Bearbeiter, "am-auftrag-arbeiten"),
        ["/Auftraege/Bearbeiten"] = (Hilfetexte.Bearbeiter, "angaben-und-kunde-aendern"),
        ["/Auftraege/KundeAendern"] = (Hilfetexte.Bearbeiter, "angaben-und-kunde-aendern"),
        ["/Auftraege/EintragAendern"] = (Hilfetexte.Bearbeiter, "eigene-eintraege-aendern"),
        ["/Kunden/Index"] = (Hilfetexte.Bearbeiter, "kunden"),
        ["/Kunden/Details"] = (Hilfetexte.Bearbeiter, "kunden"),
        ["/Geraete/Index"] = (Hilfetexte.Bearbeiter, "geraete"),
        ["/Geraete/Details"] = (Hilfetexte.Bearbeiter, "geraete"),
        ["/Geraete/Bearbeiten"] = (Hilfetexte.Bearbeiter, "geraete"),
        ["/Meine/Index"] = (Hilfetexte.Kunden, "stand-eines-auftrags"),
        ["/Meine/Neu"] = (Hilfetexte.Kunden, "einen-auftrag-stellen"),
        ["/Meine/Details"] = (Hilfetexte.Kunden, "antworten-und-nachreichen"),
    };

    /// <summary>
    /// Das Thema und die Sprungmarke für eine Seite. Wer kein Bearbeiter ist,
    /// bekommt immer die Hilfe für Kunden. Zu einer Seite ohne eigenen
    /// Abschnitt führt der Verweis an den Anfang der Hilfe.
    /// </summary>
    public static (string Thema, string? Anker) Fuer(string seite, bool istBearbeiter)
    {
        if (Seiten.TryGetValue(seite, out var ziel) && (istBearbeiter || ziel.Thema == Hilfetexte.Kunden))
            return ziel;
        return (istBearbeiter ? Hilfetexte.Bearbeiter : Hilfetexte.Kunden, null);
    }
}
