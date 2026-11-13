using Markdig;
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
/// - Bilder von fremden Adressen werden nicht geladen. Bilder gehören als
///   Anhang an den Eintrag.
/// </summary>
public static class Markdowntext
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        // Ein Zeilenumbruch bleibt ein Zeilenumbruch, wie in gewöhnlichem Text.
        .UseSoftlineBreakAsHardlineBreak()
        .UseAutoLinks()
        .UseReferralLinks("noopener", "noreferrer", "nofollow")
        .Build();

    public static IHtmlContent AlsHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return HtmlString.Empty;

        var dokument = Markdown.Parse(text, Pipeline);
        foreach (var link in dokument.Descendants<LinkInline>().ToList())
        {
            if (link.IsImage)
            {
                // Aus dem Bild wird ein gewöhnlicher Link auf seine Adresse.
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
}
