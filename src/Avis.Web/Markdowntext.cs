using System.Text.RegularExpressions;
using Avis.Web.Domain;
using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Html;

namespace Avis.Web;

/// <summary>
/// Zeigt Text mit Markdown als HTML an. Gespeichert wird immer der reine
/// Text; nur die Anzeige formatiert. Was jemand eingibt, kann im Browser
/// nichts ausführen:
/// - Eingegebenes HTML wird als Text gezeigt.
/// - Links führen nur zu http, https und mailto.
/// - Bilder von fremden Adressen werden nicht geladen. Angezeigt werden nur
///   Bilder, die als Anhang am selben Eintrag hängen (Verweis "anhang:...").
/// </summary>
public static partial class Markdowntext
{
    private const string Schema = "anhang:";

    private static readonly MarkdownPipeline Pipeline = Bauen();

    private static MarkdownPipeline Bauen()
    {
        var bauplan = new MarkdownPipelineBuilder()
            .DisableHtml()
            // Ein Zeilenumbruch bleibt ein Zeilenumbruch, wie in gewöhnlichem Text.
            .UseSoftlineBreakAsHardlineBreak()
            .UseAutoLinks()
            .UsePipeTables()
            .UseReferralLinks("noopener", "noreferrer", "nofollow");

        // Ein umgekehrter Schrägstrich ist immer er selbst. Sonst ginge in
        // Pfaden wie \\server\freigabe einer verloren.
        bauplan.InlineParsers.TryRemove<EscapeInlineParser>();
        return bauplan.Build();
    }

    /// <summary>Der Verweis, der ein Bild aus den Anhängen des Eintrags an eine Stelle im Text setzt.</summary>
    public static string Bildverweis(Anhang anhang, string? alternativtext = null)
    {
        var text = string.IsNullOrWhiteSpace(alternativtext) ? anhang.Dateiname : alternativtext.Trim();
        return $"![{text.Replace("[", "(").Replace("]", ")")}]({Schema}{Kennung(anhang)})";
    }

    /// <summary>Ob der Text das Bild an einer Stelle zeigt. Dann steht es nicht noch einmal bei den Anhängen.</summary>
    public static bool Verweist(string? text, Anhang anhang) =>
        text is not null && text.Contains(Schema + Kennung(anhang), StringComparison.Ordinal);

    /// <summary>Für Mails und andere Stellen ohne Bilder: Aus dem Verweis wird "[Bild: Name]".</summary>
    public static string OhneBilder(string text) => Verweismuster().Replace(text, "[Bild: $1]");

    /// <summary>Der letzte Teil des Ablagenamens, den AVIS beim Speichern zufällig vergeben hat.</summary>
    private static string Kennung(Anhang anhang) => anhang.Ablagename[(anhang.Ablagename.LastIndexOf('/') + 1)..];

    /// <param name="text">Der gespeicherte Text.</param>
    /// <param name="anhaenge">Die Anhänge, die im Text als Bild erscheinen dürfen.</param>
    /// <param name="bildadresse">Die Adresse, unter der ein solcher Anhang als Bild abrufbar ist.</param>
    public static IHtmlContent AlsHtml(string? text, IEnumerable<Anhang>? anhaenge = null, Func<Anhang, string?>? bildadresse = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return HtmlString.Empty;

        var dokument = Markdown.Parse(text, Pipeline);
        foreach (var link in dokument.Descendants<LinkInline>().ToList())
        {
            if (link.IsImage && link.Url is { } verweis && verweis.StartsWith(Schema, StringComparison.Ordinal))
            {
                var anhang = anhaenge?.FirstOrDefault(a => a.Bildtyp is not null && Kennung(a) == verweis[Schema.Length..]);
                var adresse = anhang is null ? null : bildadresse?.Invoke(anhang);
                if (adresse is null)
                {
                    link.ReplaceBy(new LiteralInline($"[Bild: {Beschriftung(link)}]"), copyChildren: false);
                    continue;
                }

                // Das Bild steht im Text und öffnet sich bei einem Klick in voller Größe.
                link.Url = adresse;
                var huelle = new LinkInline(adresse, "");
                link.ReplaceBy(huelle, copyChildren: false);
                huelle.AppendChild(link);
                continue;
            }

            if (link.IsImage)
            {
                // Ein Bild von einer fremden Adresse wird zu einem gewöhnlichen Link dorthin.
                link.IsImage = false;
            }

            if (!Erlaubt(link.Url))
                link.ReplaceBy(new LiteralInline(Beschriftung(link) is { Length: > 0 } t ? t : link.Url ?? ""), copyChildren: false);
        }

        return new HtmlString($"<div class=\"md\">{Markdown.ToHtml(dokument, Pipeline)}</div>");
    }

    private static bool Erlaubt(string? adresse) =>
        Uri.TryCreate(adresse, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto);

    private static string Beschriftung(LinkInline link) =>
        string.Concat(link.Descendants<LiteralInline>().Select(l => l.Content.ToString()));

    [GeneratedRegex(@"!\[([^\]]*)\]\(anhang:[0-9a-f]{32}\)")]
    private static partial Regex Verweismuster();
}
