using System.Text.RegularExpressions;

namespace Avis.Web.Domain;

/// <summary>
/// Findet in einem Text die Geräte, die mit "INV" und ihrer Inventarnummer
/// genannt sind, zum Beispiel "INV 4532436". So kommen Geräte an einen
/// Auftrag: Wer eines nennt, verknüpft es, in der Beschreibung wie in jedem
/// späteren Text. Es gibt dafür kein eigenes Feld.
/// </summary>
public static partial class Geraetebezug
{
    // "INV" als eigenes Wort, dann ein Trennzeichen oder gleich eine Ziffer, dann die Nummer. "Inventar"
    // oder "Investition" sind damit nicht gemeint. Die Nummer endet vor einem Satzzeichen.
    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])INV(?:[\s:#.\-]+|(?=\p{N}))(?<nummer>[\p{L}\p{N}](?:[\p{L}\p{N}\-_.]*[\p{L}\p{N}])?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Nennung();

    /// <summary>
    /// Die genannten Inventarnummern als Schlüssel, wie sie am Gerät stehen
    /// (<see cref="Inventargeraet.Nummernschluessel"/>). Beginnt die Nummer
    /// im Inventar selbst mit "INV", passt auch das: Zu "INV-000123" gehören
    /// die Schlüssel "000123", "inv000123" und "inv-000123".
    /// </summary>
    public static HashSet<string> Nummern(IEnumerable<string?> texte)
    {
        var nummern = new HashSet<string>();
        foreach (var text in texte)
        {
            if (string.IsNullOrEmpty(text))
                continue;

            foreach (Match treffer in Nennung().Matches(text))
            {
                var nummer = Inventargeraet.Schluessel(treffer.Groups["nummer"].Value);
                nummern.Add(nummer);
                nummern.Add("inv" + nummer);
                nummern.Add("inv-" + nummer);
            }
        }
        return nummern;
    }
}
