using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MimeKit.Text;

namespace Avis.Web.Posteingang;

/// <summary>Holt den lesbaren Text aus einer Mail.</summary>
public static partial class Mailtext
{
    /// <summary>
    /// Der Text der Mail als Markdown. Hat die Mail einen HTML-Teil, bleiben
    /// dessen Formatierungen erhalten (siehe HtmlZuMarkdown). Geht dabei
    /// etwas schief oder kommt deutlich weniger Text heraus als im
    /// Nur-Text-Teil steht, gilt der Nur-Text-Teil.
    /// </summary>
    /// <param name="bild">Liefert zu einem Bild im Text der Mail das Markdown für seine Stelle, oder null.</param>
    public static string Lesen(MimeMessage mail, Func<string, string?, string?>? bild = null)
    {
        var nurText = Aufgeraeumt(mail.TextBody ?? "");
        if (string.IsNullOrWhiteSpace(mail.HtmlBody))
            return nurText;

        try
        {
            var markdown = HtmlZuMarkdown.Wandeln(mail.HtmlBody, bild);
            if (markdown.Length > 0 && Buchstaben(markdown) >= Buchstaben(nurText) / 2)
                return markdown;
        }
        catch (Exception)
        {
            // Der Nur-Text-Teil genügt.
        }

        return nurText.Length > 0 ? nurText : Aufgeraeumt(AusHtml(mail.HtmlBody));
    }

    private static int Buchstaben(string text) => text.Count(char.IsLetterOrDigit);

    public static string AusHtml(string html)
    {
        var text = new StringBuilder();
        var tokenizer = new HtmlTokenizer(new StringReader(html));
        var ausgeblendet = 0;

        while (tokenizer.ReadNextToken(out var token))
        {
            switch (token)
            {
                case HtmlTagToken tag:
                    if (tag.Id is HtmlTagId.Script or HtmlTagId.Style or HtmlTagId.Head or HtmlTagId.Title)
                    {
                        if (!tag.IsEmptyElement)
                            ausgeblendet += tag.IsEndTag ? -1 : 1;
                    }
                    else if (tag.Id == HtmlTagId.Br
                        || (tag.IsEndTag && tag.Id is HtmlTagId.P or HtmlTagId.Div or HtmlTagId.TR or HtmlTagId.LI
                            or HtmlTagId.H1 or HtmlTagId.H2 or HtmlTagId.H3 or HtmlTagId.H4 or HtmlTagId.Table or HtmlTagId.BlockQuote))
                    {
                        text.Append('\n');
                    }
                    break;

                case HtmlDataToken daten when ausgeblendet <= 0 && token.Kind == HtmlTokenKind.Data:
                    // In HTML ist ein Zeilenumbruch im Quelltext nur ein Leerzeichen.
                    text.Append(Leerraum().Replace(daten.Data, " "));
                    break;
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// Schneidet bei einer Antwort den zitierten bisherigen Schriftwechsel ab.
    /// Der steht ohnehin im Auftrag. Bleibt nichts übrig, kommt der ganze Text zurück.
    /// </summary>
    public static string OhneZitat(string text)
    {
        var zeilen = text.Split('\n');
        var ende = zeilen.Length;

        for (var i = 0; i < zeilen.Length; i++)
        {
            var zeile = zeilen[i].Trim();
            var kopfzeilenFolgen = zeilen.Skip(i + 1).Take(4).Any(z => Kopfzeile().IsMatch(z.Trim()));
            if (Trennzeile().IsMatch(zeile)
                || SchriebAm().IsMatch(zeile)
                || (Absenderzeile().IsMatch(zeile) && kopfzeilenFolgen))
            {
                ende = i;
                break;
            }
        }

        // Zitatzeilen und Trennlinien am Ende, wie sie andere Mailprogramme setzen.
        while (ende > 0 && (zeilen[ende - 1].TrimStart().StartsWith('>') || zeilen[ende - 1].Trim() is "" or "---"))
            ende--;

        var ohne = Aufgeraeumt(string.Join('\n', zeilen.Take(ende)));
        return ohne.Length > 0 ? ohne : Aufgeraeumt(text);
    }

    /// <summary>Einheitliche Zeilenenden, kein Leerraum am Zeilenende, höchstens eine Leerzeile hintereinander.</summary>
    public static string Aufgeraeumt(string text)
    {
        var zeilen = text.ReplaceLineEndings("\n").Replace(' ', ' ').Split('\n').Select(z => z.TrimEnd());
        return MehrereLeerzeilen().Replace(string.Join('\n', zeilen), "\n\n").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MehrereLeerzeilen();

    [GeneratedRegex(@"^(-{2,}\s*(Ursprüngliche Nachricht|Urspr\S+ Nachricht|Original Message|Weitergeleitete Nachricht)\s*-{2,}|_{10,})$", RegexOptions.IgnoreCase)]
    private static partial Regex Trennzeile();

    [GeneratedRegex(@"^(Am .+ schrieb .+:|On .+ wrote:)$", RegexOptions.IgnoreCase)]
    private static partial Regex SchriebAm();

    // Aus HTML gewandelt stehen die Bezeichnungen fett: **Von:** ...
    [GeneratedRegex(@"^\**(Von|From):\**\s+\S", RegexOptions.IgnoreCase)]
    private static partial Regex Absenderzeile();

    [GeneratedRegex(@"^\**(Gesendet|Sent|Datum|Date):\**\s+\S", RegexOptions.IgnoreCase)]
    private static partial Regex Kopfzeile();
}
