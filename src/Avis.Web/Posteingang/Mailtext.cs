using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MimeKit.Text;

namespace Avis.Web.Posteingang;

/// <summary>Holt den lesbaren Text aus einer Mail.</summary>
public static partial class Mailtext
{
    /// <summary>Der Text der Mail. Hat sie nur einen HTML-Teil, wird daraus Text.</summary>
    public static string Lesen(MimeMessage mail)
    {
        var text = mail.TextBody;
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(mail.HtmlBody))
            text = AusHtml(mail.HtmlBody);
        return Aufgeraeumt(text ?? "");
    }

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

        // Zitatzeilen am Ende, wie sie andere Mailprogramme setzen.
        while (ende > 0 && (zeilen[ende - 1].TrimStart().StartsWith('>') || string.IsNullOrWhiteSpace(zeilen[ende - 1])))
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

    [GeneratedRegex(@"^(Von|From):\s+\S", RegexOptions.IgnoreCase)]
    private static partial Regex Absenderzeile();

    [GeneratedRegex(@"^(Gesendet|Sent|Datum|Date):\s+\S", RegexOptions.IgnoreCase)]
    private static partial Regex Kopfzeile();
}
