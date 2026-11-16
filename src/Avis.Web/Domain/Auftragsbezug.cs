using System.Globalization;
using System.Text.RegularExpressions;

namespace Avis.Web.Domain;

/// <summary>
/// Findet in einem Text die Aufträge, die mit "AVIS" und ihrer Nummer genannt
/// sind, zum Beispiel "AVIS 123". So entsteht der Bezug zwischen zwei
/// Aufträgen: Wer einen anderen nennt, verknüpft ihn, in der Beschreibung wie
/// in jedem späteren Text. Mehr als diesen Verweis gibt es nicht: Jeder
/// Auftrag steht für sich, mit eigener Gruppe, eigenem Bearbeiter und eigenem
/// Abschluss.
/// </summary>
public static partial class Auftragsbezug
{
    // "AVIS" als eigenes Wort, dann ein Trennzeichen oder gleich die Nummer. Auch die Form aus dem Betreff
    // der Mails gilt: "[AVIS-123]". Eine Zahl mit Nachkommastellen ist keine Auftragsnummer ("AVIS 0.1").
    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])AVIS(?:[\s:#.\-]+|(?=\d))(?<nummer>\d{1,9})(?![\p{L}\p{N}]|[.,]\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Nennung();

    /// <summary>Die Nummern der genannten Aufträge.</summary>
    public static HashSet<int> Nummern(IEnumerable<string?> texte)
    {
        var nummern = new HashSet<int>();
        foreach (var text in texte)
        {
            if (string.IsNullOrEmpty(text))
                continue;

            foreach (Match treffer in Nennung().Matches(text))
            {
                if (int.TryParse(treffer.Groups["nummer"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var nummer) && nummer > 0)
                    nummern.Add(nummer);
            }
        }
        return nummern;
    }
}
