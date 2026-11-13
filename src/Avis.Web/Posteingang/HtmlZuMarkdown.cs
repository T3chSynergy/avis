using System.Text;
using System.Text.RegularExpressions;
using MimeKit.Text;

namespace Avis.Web.Posteingang;

/// <summary>
/// Wandelt den HTML-Teil einer Mail in Markdown.
///
/// Übernommen wird nur, was eindeutig ist: fett, kursiv, Überschriften,
/// Links, Listen, Zitate, vorformatierter Text, Bilder aus der Mail und kleine,
/// einfache Tabellen. Alles andere wird zu gewöhnlichem Text: Schriftarten,
/// Farben, Layout. Unbekannte und kaputte Tags stören nicht, ihr Text bleibt
/// erhalten.
///
/// Zeichen, die in Markdown eine Bedeutung hätten, aber in der Mail
/// gewöhnlicher Text waren, werden als Zeichencode geschrieben. So wird aus
/// einem Sternchen im Text keine Formatierung.
/// </summary>
public sealed partial class HtmlZuMarkdown
{
    private const int HoechstSpalten = 6;
    private const int HoechstZeilen = 30;
    private const int HoechstZeichenJeZelle = 200;

    private sealed class Kontext
    {
        public List<string> Zeilen { get; } = [];
        public StringBuilder Aktuell { get; } = new();

        /// <summary>In der aktuellen Zeile geöffnete Auszeichnungen, 'b' für fett und 'i' für kursiv.</summary>
        public List<char> Offen { get; } = [];

        /// <summary>Für Tabellenzellen: Die Zelle enthält eine Liste, ein Zitat oder Ähnliches.</summary>
        public bool HatBlock { get; set; }
    }

    private sealed class Liste
    {
        public bool Geordnet { get; init; }
        public int Zaehler { get; set; } = 1;

        /// <summary>Breite der Marke des aktuellen Punkts. Folgezeilen werden so weit eingerückt.</summary>
        public int Markenbreite { get; set; }
        public bool NeuerPunkt { get; set; }
        public bool ImPunkt { get; set; }
    }

    private sealed class Tabelle
    {
        public List<List<Kontext>> Zeilen { get; } = [];
        public bool NichtEinfach { get; set; }
    }

    private readonly Func<string, string?, string?>? _bild;
    private readonly Stack<Kontext> _kontexte = new();
    private readonly Stack<Liste> _listen = new();

    private Kontext K => _kontexte.Peek();

    private int _ausgeblendet;
    private int _fett;
    private int _kursiv;
    private int _zitat;
    private int _ueberschrift;

    private StringBuilder? _vorformatiert;

    // Link: Der Text wird gesammelt und am Ende mit der Adresse zusammengesetzt.
    private StringBuilder? _linktext;
    private StringBuilder? _linkRoh;
    private string? _linkadresse;
    private int _offenBeiLinkstart;

    // Outlook schreibt Listen manchmal als gewöhnliche Absätze mit einer Marke davor.
    private bool _outlookListenpunkt;
    private StringBuilder? _outlookMarke;
    private int _outlookMarkeTiefe;
    private string? _outlookPraefix;

    private Tabelle? _tabelle;
    private int _tabellenTiefe;
    private bool _zelleOffen;

    // Für leere Absätze von Outlook, die eine Leerzeile bedeuten.
    private int _zeilenBeiAbsatzbeginn;

    private HtmlZuMarkdown(Func<string, string?, string?>? bild)
    {
        _bild = bild;
        _kontexte.Push(new Kontext());
    }

    /// <summary>
    /// Wandelt HTML in Markdown.
    /// </summary>
    /// <param name="html">Der HTML-Teil der Mail.</param>
    /// <param name="bild">
    /// Liefert zu einem Bild aus der Mail (Kennung aus <c>cid:</c>, Alternativtext)
    /// das Markdown, das an seiner Stelle stehen soll, oder null, wenn es entfällt.
    /// </param>
    public static string Wandeln(string html, Func<string, string?, string?>? bild = null)
    {
        var wandler = new HtmlZuMarkdown(bild);
        var tokenizer = new HtmlTokenizer(new StringReader(html));
        while (tokenizer.ReadNextToken(out var token))
        {
            switch (token)
            {
                case HtmlTagToken tag:
                    wandler.Tag(tag);
                    break;
                case HtmlDataToken daten when token.Kind == HtmlTokenKind.Data:
                    wandler.Daten(daten.Data);
                    break;
            }
        }
        return wandler.Ergebnis();
    }

    private string Ergebnis()
    {
        // Was am Ende noch offen ist, wird geschlossen.
        while (_tabellenTiefe > 0)
            TabelleEnde();
        VorformatiertEnde();
        ZeileBeenden();

        var zeilen = _kontexte.Last().Zeilen;
        var text = new StringBuilder();
        var leer = true;
        foreach (var zeile in zeilen)
        {
            if (zeile.Length == 0)
            {
                if (!leer)
                    text.Append('\n');
                leer = true;
                continue;
            }
            text.Append(zeile).Append('\n');
            leer = false;
        }
        return text.ToString().Trim('\n', ' ');
    }

    // ----- Tags -----

    private void Tag(HtmlTagToken tag)
    {
        var name = tag.Name.ToLowerInvariant();
        var ende = tag.IsEndTag;

        if (name is "script" or "style" or "head" or "title")
        {
            if (!tag.IsEmptyElement)
                _ausgeblendet = Math.Max(0, _ausgeblendet + (ende ? -1 : 1));
            return;
        }
        if (_ausgeblendet > 0)
            return;

        if (_vorformatiert is not null)
        {
            if (name == "pre" && ende)
                VorformatiertEnde();
            else if (name == "br")
                _vorformatiert.Append('\n');
            return;
        }

        switch (name)
        {
            case "b" or "strong":
                Auszeichnung('b', ref _fett, ende);
                break;
            case "i" or "em":
                Auszeichnung('i', ref _kursiv, ende);
                break;

            case "br":
                if (_ueberschrift > 0 || _linktext is not null)
                    Daten(" ");
                else if (K.Aktuell.ToString().Trim().Length == 0)
                    Leerzeile();
                else
                    ZeileBeenden();
                break;

            case "p":
                AbsatzTag(tag, ende);
                break;
            case "div":
                if (_ueberschrift > 0)
                    Daten(" ");
                else
                    ZeileBeenden();
                break;

            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                Absatz();
                K.HatBlock = true;
                _ueberschrift = ende ? 0 : Math.Min(name[1] - '0', 3);
                break;

            case "ul" or "ol":
                ListeTag(tag, name == "ol", ende);
                break;
            case "li":
                ListenpunktTag(ende);
                break;

            case "blockquote":
                Absatz();
                K.HatBlock = true;
                _zitat = Math.Max(0, _zitat + (ende ? -1 : 1));
                break;

            case "pre":
                if (!ende)
                {
                    Absatz();
                    K.HatBlock = true;
                    _vorformatiert = new StringBuilder();
                }
                break;

            case "hr":
                Absatz();
                K.HatBlock = true;
                K.Zeilen.Add("---");
                Leerzeile();
                break;

            case "a":
                if (ende)
                    LinkEnde();
                else
                    LinkAnfang(Attribut(tag, "href"));
                break;

            case "img":
                Bild(Attribut(tag, "src"), Attribut(tag, "alt"));
                break;

            case "span":
                OutlookMarke(tag, ende);
                break;

            case "table":
                if (ende)
                    TabelleEnde();
                else
                    TabelleAnfang();
                break;
            case "tr":
                TabellenzeileTag(ende);
                break;
            case "td" or "th":
                ZelleTag(tag, ende);
                break;
        }
    }

    private static string? Attribut(HtmlTagToken tag, string name) =>
        tag.Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private void AbsatzTag(HtmlTagToken tag, bool ende)
    {
        if (_ueberschrift > 0)
        {
            Daten(" ");
            return;
        }

        // Outlook gibt jeder Zeile einen eigenen Absatz ohne Abstand. Eine
        // Leerzeile ist dort ein leerer Absatz. Sonst ist ein Absatz ein Absatz.
        var klasse = Attribut(tag, "class") ?? "";
        var outlook = klasse.StartsWith("Mso", StringComparison.OrdinalIgnoreCase);

        if (!ende)
        {
            if (outlook)
                ZeileBeenden();
            else
                Absatz();
            _zeilenBeiAbsatzbeginn = K.Zeilen.Count;
            _outlookListenpunkt = klasse.StartsWith("MsoListParagraph", StringComparison.OrdinalIgnoreCase) && _listen.Count == 0;
            _outlookPraefix = null;
            return;
        }

        // Am Ende ist die Klasse nicht mehr bekannt. Ein Absatz, der nichts
        // ausgegeben hat, wird zur Leerzeile, jeder andere endet als Zeile.
        var hatteInhalt = K.Aktuell.ToString().Trim().Length > 0 || K.Zeilen.Count > _zeilenBeiAbsatzbeginn;
        ZeileBeenden();
        _outlookListenpunkt = false;
        _outlookPraefix = null;
        if (!hatteInhalt)
            Leerzeile();
    }

    private void Auszeichnung(char art, ref int tiefe, bool ende)
    {
        if (!ende)
        {
            tiefe++;
            return;
        }
        if (tiefe == 0)
            return;
        tiefe--;
        if (tiefe == 0)
            Schliessen(K.Offen.LastIndexOf(art));
    }

    // ----- Listen -----

    private void ListeTag(HtmlTagToken tag, bool geordnet, bool ende)
    {
        K.HatBlock = true;
        if (!ende)
        {
            if (_listen.Count == 0)
                Absatz();
            else
                ZeileBeenden();
            var start = int.TryParse(Attribut(tag, "start"), out var s) && s > 0 ? s : 1;
            _listen.Push(new Liste { Geordnet = geordnet, Zaehler = start });
            return;
        }

        ZeileBeenden();
        if (_listen.Count > 0)
            _listen.Pop();
        if (_listen.Count == 0)
            Leerzeile();
    }

    private void ListenpunktTag(bool ende)
    {
        ZeileBeenden();
        if (_listen.Count == 0)
            return;

        var liste = _listen.Peek();
        liste.ImPunkt = !ende;
        liste.NeuerPunkt = !ende;
    }

    /// <summary>
    /// Outlook kennzeichnet die Marke eines Listenpunkts ("·" oder "1.") mit
    /// mso-list:Ignore. Sie wird nicht als Text übernommen, sondern bestimmt
    /// die Art des Punkts.
    /// </summary>
    private void OutlookMarke(HtmlTagToken tag, bool ende)
    {
        if (_outlookMarke is not null)
        {
            _outlookMarkeTiefe += ende ? -1 : 1;
            if (_outlookMarkeTiefe > 0)
                return;

            var marke = _outlookMarke.ToString().Replace(' ', ' ').Trim();
            _outlookMarke = null;
            if (_outlookListenpunkt)
            {
                var nummer = Nummer().Match(marke);
                _outlookPraefix = nummer.Success ? $"{nummer.Groups[1].Value}. " : "- ";
            }
            return;
        }

        if (!ende && (Attribut(tag, "style") ?? "").Replace(" ", "").Contains("mso-list:Ignore", StringComparison.OrdinalIgnoreCase))
        {
            _outlookMarke = new StringBuilder();
            _outlookMarkeTiefe = 1;
        }
    }

    // ----- Links und Bilder -----

    private void LinkAnfang(string? adresse)
    {
        LinkEnde();
        _linktextBegannMitLeerraum = false;
        _linkadresse = Erlaubt(adresse) ? adresse!.Trim() : null;
        _linktext = new StringBuilder();
        _linkRoh = new StringBuilder();
        _offenBeiLinkstart = K.Offen.Count;
    }

    private void LinkEnde()
    {
        if (_linktext is null)
            return;

        // Was im Link geöffnet wurde, endet auch im Link.
        if (K.Offen.Count > _offenBeiLinkstart)
            Schliessen(_offenBeiLinkstart);

        var text = _linktext.ToString().Trim();
        var roh = _linkRoh!.ToString().Trim();
        var adresse = _linkadresse;
        _linktext = null;
        _linkRoh = null;
        _linkadresse = null;

        if (text.Length == 0 && adresse is null)
            return;

        var ziel = K.Aktuell;
        if (ziel.Length > 0 && !char.IsWhiteSpace(ziel[^1]) && _linktextBegannMitLeerraum)
            ziel.Append(' ');

        if (adresse is null)
        {
            ziel.Append(text);
        }
        else if (text.Length == 0 || GleicheAdresse(roh, adresse))
        {
            // Die Adresse selbst ist der Text: Markdown verlinkt sie von allein.
            ziel.Append(adresse.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? adresse[7..] : adresse);
        }
        else
        {
            var sicher = adresse.Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29").Replace("<", "%3C").Replace(">", "%3E");
            ziel.Append('[').Append(text.Replace("]", "&#93;")).Append("](").Append(sicher).Append(')');
        }
    }

    private bool _linktextBegannMitLeerraum;

    private static bool GleicheAdresse(string text, string adresse)
    {
        static string Kern(string s)
        {
            s = s.Trim().TrimEnd('/');
            foreach (var schema in new[] { "mailto:", "https://", "http://" })
            {
                if (s.StartsWith(schema, StringComparison.OrdinalIgnoreCase))
                    return s[schema.Length..];
            }
            return s;
        }
        return string.Equals(Kern(text), Kern(adresse), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Erlaubt(string? adresse) =>
        Uri.TryCreate(adresse?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto);

    private void Bild(string? quelle, string? alternativtext)
    {
        // Nur Bilder, die in der Mail selbst stecken. Fremde Adressen werden nicht geladen.
        if (_bild is null || quelle is null || !quelle.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
            return;
        if (_tabelle is not null && !_zelleOffen)
            return;

        var markdown = _bild(Uri.UnescapeDataString(quelle[4..]).Trim(), alternativtext);
        if (string.IsNullOrEmpty(markdown))
            return;

        var ziel = _linktext ?? K.Aktuell;
        if (ziel.Length > 0 && !char.IsWhiteSpace(ziel[^1]))
            ziel.Append(' ');
        ziel.Append(markdown);
    }

    // ----- Tabellen -----

    private void TabelleAnfang()
    {
        _tabellenTiefe++;
        if (_tabellenTiefe > 1)
        {
            // Eine Tabelle in einer Tabelle ist Layout, keine einfache Tabelle.
            _tabelle!.NichtEinfach = true;
            ZeileBeenden();
            return;
        }

        Absatz();
        _tabelle = new Tabelle { NichtEinfach = _zitat > 0 || _listen.Count > 0 };
    }

    private void TabellenzeileTag(bool ende)
    {
        if (_tabellenTiefe != 1)
        {
            ZeileBeenden();
            return;
        }
        ZelleBeenden();
        if (!ende)
            _tabelle!.Zeilen.Add([]);
    }

    private void ZelleTag(HtmlTagToken tag, bool ende)
    {
        if (_tabellenTiefe != 1)
        {
            ZeileBeenden();
            return;
        }

        ZelleBeenden();
        if (ende)
            return;

        if (_tabelle!.Zeilen.Count == 0)
            _tabelle.Zeilen.Add([]);
        if (Verbunden(Attribut(tag, "colspan")) || Verbunden(Attribut(tag, "rowspan")))
            _tabelle.NichtEinfach = true;

        _kontexte.Push(new Kontext());
        _zelleOffen = true;
    }

    private static bool Verbunden(string? wert) => int.TryParse(wert, out var zahl) && zahl > 1;

    private void ZelleBeenden()
    {
        if (!_zelleOffen)
            return;

        VorformatiertEnde();
        ZeileBeenden();
        var zelle = _kontexte.Pop();
        zelle.Zeilen.RemoveAll(z => z.Length == 0);
        _tabelle!.Zeilen[^1].Add(zelle);
        _zelleOffen = false;
    }

    private void TabelleEnde()
    {
        if (_tabellenTiefe == 0)
            return;
        if (_tabellenTiefe > 1)
        {
            ZeileBeenden();
            _tabellenTiefe--;
            return;
        }

        ZelleBeenden();
        _tabellenTiefe = 0;
        var tabelle = _tabelle!;
        _tabelle = null;

        var zeilen = tabelle.Zeilen.Where(z => z.Any(zelle => zelle.Zeilen.Count > 0)).ToList();
        if (zeilen.Count == 0)
            return;

        var spalten = zeilen.Max(z => z.Count);
        var einfach = !tabelle.NichtEinfach
            && zeilen.Count is >= 2 and <= HoechstZeilen
            && spalten is >= 2 and <= HoechstSpalten
            && zeilen.All(z => z.Count == spalten)
            && zeilen.SelectMany(z => z).All(zelle => !zelle.HatBlock && zelle.Zeilen.Sum(t => t.Length) <= HoechstZeichenJeZelle);

        if (einfach)
        {
            static string Zeile(List<Kontext> zellen) =>
                "| " + string.Join(" | ", zellen.Select(z => string.Join(" ", z.Zeilen).Replace("|", "&#124;"))) + " |";

            K.Zeilen.Add(Zeile(zeilen[0]));
            K.Zeilen.Add("| " + string.Join(" | ", Enumerable.Repeat("---", spalten)) + " |");
            foreach (var zeile in zeilen.Skip(1))
                K.Zeilen.Add(Zeile(zeile));
        }
        else
        {
            // Keine einfache Tabelle: Der Inhalt jeder Zelle steht für sich, Zeile für Zeile.
            foreach (var zeile in zeilen)
            {
                foreach (var zelle in zeile)
                    K.Zeilen.AddRange(zelle.Zeilen);
                K.Zeilen.Add("");
            }
        }
        Leerzeile();
    }

    // ----- Text -----

    private void Daten(string roh)
    {
        if (_ausgeblendet > 0)
            return;
        if (_vorformatiert is not null)
        {
            _vorformatiert.Append(roh);
            return;
        }
        if (_outlookMarke is not null)
        {
            _outlookMarke.Append(roh);
            return;
        }
        // Zwischen den Zellen einer Tabelle steht nur Leerraum.
        if (_tabelle is not null && !_zelleOffen)
            return;

        var text = Leerraum().Replace(roh.Replace(' ', ' '), " ");
        if (text.Length == 0)
            return;

        var ziel = _linktext ?? K.Aktuell;
        var amAnfang = ziel.Length == 0 || char.IsWhiteSpace(ziel[^1]);

        if (_linktext is not null && _linktext.Length == 0 && _linkRoh!.Length == 0)
            _linktextBegannMitLeerraum = text[0] == ' ';

        if (text[0] == ' ')
        {
            text = text.TrimStart();
            if (!amAnfang)
                ziel.Append(' ');
            amAnfang = true;
        }
        if (text.Length == 0)
            return;

        Oeffnen(ziel, text[0]);
        _linkRoh?.Append(text);

        // Ein Buchstabe direkt hinter einer schließenden Auszeichnung nach
        // einem Satzzeichen würde sie aufheben. Ein Leerzeichen verhindert das.
        if (ziel.Length >= 2 && ziel[^1] == '*' && char.IsLetterOrDigit(text[0]) && EndetMitSchlussNachSatzzeichen(ziel))
            ziel.Append(' ');

        ziel.Append(Maskiert(text, ziel));
    }

    private static bool EndetMitSchlussNachSatzzeichen(StringBuilder ziel)
    {
        var i = ziel.Length - 1;
        while (i >= 0 && ziel[i] == '*')
            i--;
        return i >= 0 && char.IsPunctuation(ziel[i]);
    }

    /// <summary>Öffnet fett und kursiv erst vor dem ersten Zeichen, damit kein Leerraum in der Auszeichnung steht.</summary>
    private void Oeffnen(StringBuilder ziel, char erstesZeichen)
    {
        var oeffnen = new List<char>();
        if (_fett > 0 && !K.Offen.Contains('b'))
            oeffnen.Add('b');
        if (_kursiv > 0 && !K.Offen.Contains('i'))
            oeffnen.Add('i');
        if (oeffnen.Count == 0)
            return;

        // Vor einem Satzzeichen direkt nach einem Buchstaben würde die Auszeichnung nicht erkannt.
        if (ziel.Length > 0 && char.IsLetterOrDigit(ziel[^1]) && char.IsPunctuation(erstesZeichen))
            ziel.Append(' ');

        foreach (var art in oeffnen)
        {
            ziel.Append(art == 'b' ? "**" : "*");
            K.Offen.Add(art);
        }
    }

    /// <summary>Schließt alle Auszeichnungen ab der genannten Stelle. Leerraum am Ende rückt hinter die Auszeichnung.</summary>
    private void Schliessen(int ab)
    {
        if (ab < 0 || ab >= K.Offen.Count)
            return;

        var ziel = _linktext ?? K.Aktuell;
        var laenge = ziel.Length;
        while (laenge > 0 && char.IsWhiteSpace(ziel[laenge - 1]))
            laenge--;
        var leerraum = ziel.ToString(laenge, ziel.Length - laenge);
        ziel.Length = laenge;

        for (var i = K.Offen.Count - 1; i >= ab; i--)
            ziel.Append(K.Offen[i] == 'b' ? "**" : "*");
        K.Offen.RemoveRange(ab, K.Offen.Count - ab);
        ziel.Append(leerraum);
    }

    /// <summary>
    /// Schreibt Zeichen als Zeichencode, die in Markdown eine Auszeichnung
    /// beginnen würden. Unterstriche mitten im Wort bleiben, wie sie sind.
    /// </summary>
    private static string Maskiert(string text, StringBuilder davor)
    {
        if (text.IndexOfAny(['*', '`', '_', '[', '&']) < 0)
            return text;

        var ergebnis = new StringBuilder(text.Length + 8);
        var linkMoeglich = text.Contains("](");
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '*':
                    ergebnis.Append("&#42;");
                    break;
                case '`':
                    ergebnis.Append("&#96;");
                    break;
                case '[' when linkMoeglich:
                    ergebnis.Append("&#91;");
                    break;
                case '&' when Zeichencode().IsMatch(text.AsSpan(i)):
                    ergebnis.Append("&amp;");
                    break;
                case '_':
                    var links = i > 0 ? text[i - 1] : davor.Length > 0 ? davor[^1] : ' ';
                    var rechts = i + 1 < text.Length ? text[i + 1] : ' ';
                    ergebnis.Append(char.IsLetterOrDigit(links) && char.IsLetterOrDigit(rechts) ? "_" : "&#95;");
                    break;
                default:
                    ergebnis.Append(c);
                    break;
            }
        }
        return ergebnis.ToString();
    }

    // ----- Zeilen -----

    private void VorformatiertEnde()
    {
        if (_vorformatiert is null)
            return;

        var inhalt = _vorformatiert.ToString().ReplaceLineEndings("\n").Trim('\n');
        _vorformatiert = null;
        if (inhalt.Trim().Length == 0)
            return;

        var zaun = inhalt.Contains("```") ? "````" : "```";
        var praefix = Zitatpraefix();
        K.Zeilen.Add(praefix + zaun);
        foreach (var zeile in inhalt.Split('\n'))
            K.Zeilen.Add(praefix + zeile);
        K.Zeilen.Add(praefix + zaun);
        Leerzeile();
    }

    private string Zitatpraefix() => string.Concat(Enumerable.Repeat("> ", _zitat));

    /// <summary>Schließt die aktuelle Zeile ab und gibt sie mit ihrem Anfang (Zitat, Listenpunkt, Überschrift) aus.</summary>
    private void ZeileBeenden()
    {
        LinkEnde();
        Schliessen(0);

        var text = K.Aktuell.ToString().Trim();
        K.Aktuell.Clear();
        if (text.Length == 0)
            return;

        text = AmZeilenanfangMaskiert(text);

        var praefix = new StringBuilder(Zitatpraefix());
        if (_listen.Count > 0 && _listen.Peek().ImPunkt)
        {
            var liste = _listen.Peek();
            praefix.Append(' ', 3 * (_listen.Count - 1));
            if (liste.NeuerPunkt)
            {
                var marke = liste.Geordnet ? $"{liste.Zaehler++}. " : "- ";
                liste.Markenbreite = marke.Length;
                liste.NeuerPunkt = false;
                praefix.Append(marke);
            }
            else
            {
                praefix.Append(' ', liste.Markenbreite);
            }
        }
        else if (_outlookListenpunkt && _outlookPraefix is not null)
        {
            praefix.Append(_outlookPraefix);
            _outlookPraefix = new string(' ', _outlookPraefix.Length);
        }

        if (_ueberschrift > 0)
            praefix.Append('#', _ueberschrift).Append(' ');

        K.Zeilen.Add(praefix + text);
    }

    private void Leerzeile()
    {
        if (K.Zeilen.Count > 0 && K.Zeilen[^1].Length > 0)
            K.Zeilen.Add("");
    }

    private void Absatz()
    {
        ZeileBeenden();
        Leerzeile();
    }

    /// <summary>Ein Zeilenanfang, der in Markdown eine Überschrift, ein Zitat oder eine Liste beginnen würde.</summary>
    private static string AmZeilenanfangMaskiert(string text)
    {
        if (Ueberschriftanfang().IsMatch(text))
            return "&#35;" + text[1..];
        if (text[0] == '>')
            return "&gt;" + text[1..];
        if (Aufzaehlungsanfang().IsMatch(text) || Trennlinie().IsMatch(text))
            return $"&#{(int)text[0]};" + text[1..];

        var nummer = Nummernanfang().Match(text);
        if (nummer.Success)
        {
            var ziffern = nummer.Groups[1].Length;
            return text[..ziffern] + $"&#{(int)text[ziffern]};" + text[(ziffern + 1)..];
        }
        return text;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();

    [GeneratedRegex(@"^(\d+)[.)]?$")]
    private static partial Regex Nummer();

    [GeneratedRegex(@"^&(#\d+|#[xX][0-9a-fA-F]+|[A-Za-z][A-Za-z0-9]*);")]
    private static partial Regex Zeichencode();

    [GeneratedRegex(@"^#{1,6}(\s|$)")]
    private static partial Regex Ueberschriftanfang();

    [GeneratedRegex(@"^[-+](\s|$)")]
    private static partial Regex Aufzaehlungsanfang();

    [GeneratedRegex(@"^(-{3,}|={3,})\s*$")]
    private static partial Regex Trennlinie();

    [GeneratedRegex(@"^(\d{1,9})[.)](\s|$)")]
    private static partial Regex Nummernanfang();
}
