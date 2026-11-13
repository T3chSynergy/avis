using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Avis.Web.Posteingang;

/// <summary>
/// Macht aus einer Mail an die Hotline einen Auftrag, eine Antwort oder einen
/// Kommentar.
///
/// - Steht im Betreff eine Auftragsnummer in der Form [AVIS-123] und kommt die
///   Mail vom Kunden dieses Auftrags, ist sie seine Antwort. Ist der Auftrag
///   abgeschlossen, öffnet sie ihn wieder.
/// - Kommt sie von dem Bearbeiter, der den Auftrag gerade bearbeitet, wird sie
///   ein interner Kommentar. So gelangt nichts versehentlich zum Kunden.
/// - Alles andere wird ein neuer Auftrag in der Eingangsgruppe. Das gilt auch,
///   wenn ein Dritter eine Auftragsnummer nennt: Niemand kann per Mail in den
///   Auftrag eines anderen schreiben.
/// - Automatische Mails wie Abwesenheitsnotizen werden übergangen.
/// </summary>
public partial class Mailannahme(
    AvisDb db,
    Kundenzuordnung zuordnung,
    Anhangablage ablage,
    IOptions<PosteingangOptionen> optionen,
    IOptions<MailOptionen> mailOptionen,
    TimeProvider uhr,
    ILogger<Mailannahme> log)
{
    /// <summary>Anmeldename des Kontos, in dessen Namen Mails von Absendern ohne eigenes Konto eingetragen werden.</summary>
    public const string Systemkonto = "avis:e-mail-eingang";

    private const int HoechstzahlAnhaenge = 20;

    public async Task<Maileingang> AnnehmenAsync(MimeMessage mail, CancellationToken abbruch = default)
    {
        var kennung = Kennung(mail);
        var bekannt = await db.Maileingaenge.FirstOrDefaultAsync(m => m.Kennung == kennung, abbruch);
        if (bekannt is not null)
            return bekannt;

        var jetzt = uhr.GetUtcNow().UtcDateTime;
        var absender = mail.From.Mailboxes.FirstOrDefault();
        var adresse = absender?.Address.Trim().ToLowerInvariant();
        var eingang = new Maileingang
        {
            Kennung = kennung,
            EmpfangenAm = jetzt,
            Von = Kuerzen(adresse, 320),
            Betreff = Kuerzen(mail.Subject, 300),
        };
        db.Maileingaenge.Add(eingang);

        if (Automatisch(mail, adresse) is { } grund)
        {
            eingang.Ergebnis = MaileingangErgebnis.Uebergangen;
            eingang.Hinweis = grund;
            await db.SaveChangesAsync(abbruch);
            return eingang;
        }

        var (anhaenge, ausgelassen, bilderImText) = await AnhaengeAsync(mail, abbruch);
        try
        {
            // Ein Bild im Text der Mail steht auch im Auftrag an seiner Stelle.
            var text = Mailtext.Lesen(mail, (kennung, alternativtext) =>
                bilderImText.TryGetValue(kennung, out var bild) ? Markdowntext.Bildverweis(bild, alternativtext) : null);
            var bezug = await BezugAsync(mail.Subject, abbruch);
            var durch = await KontoAsync(adresse!, abbruch);

            if (bezug is not null && bezug.Kunde.Email == adresse)
            {
                var antwort = MitHinweis(OhneZitat(text, anhaenge, bilderImText), ausgelassen);
                if (bezug.Status == AuftragStatus.Abgeschlossen)
                {
                    bezug.KundeOeffnetWieder(durch, antwort, jetzt, anhaenge);
                    eingang.Ergebnis = MaileingangErgebnis.WiederGeoeffnet;
                }
                else
                {
                    bezug.KundeAntwortet(durch, antwort, jetzt, anhaenge);
                    eingang.Ergebnis = MaileingangErgebnis.Antwort;
                }
                eingang.Auftrag = bezug;
            }
            else if (bezug is not null && durch is { Aktiv: true, IstBearbeiter: true } && bezug.BearbeiterId == durch.Id)
            {
                bezug.InternKommentieren(durch, MitHinweis(OhneZitat(text, anhaenge, bilderImText), ausgelassen), jetzt, anhaenge);
                eingang.Ergebnis = MaileingangErgebnis.InternerKommentar;
                eingang.Auftrag = bezug;
            }
            else
            {
                var gruppe = await db.Gruppen.FirstOrDefaultAsync(g => g.IstEingangsgruppe && g.Aktiv, abbruch)
                    ?? throw new InvalidOperationException("Es gibt keine aktive Eingangsgruppe.");
                var kunde = await KundeAsync(absender!, adresse!, durch, abbruch);

                var beschreibung = text.Length > 0 ? text : "(kein Text)";
                if (bezug is not null)
                {
                    beschreibung = $"[Diese Mail nennt den Auftrag {bezug.Id}, kommt aber nicht von dessen Kunden. Sie wurde deshalb als neuer Auftrag erfasst.]\n\n{beschreibung}";
                    eingang.Hinweis = $"Nennt Auftrag {bezug.Id}, Absender ist nicht dessen Kunde.";
                }

                var betreff = string.IsNullOrWhiteSpace(mail.Subject) ? "(ohne Betreff)" : mail.Subject.Trim();
                var auftrag = Auftrag.Erfassen(
                    new AuftragAngaben(Kuerzen(betreff, 200)!, MitHinweis(beschreibung, ausgelassen)!),
                    kunde, durch, gruppe, Eingang.EMail, jetzt, anhaenge);
                db.Auftraege.Add(auftrag);
                eingang.Ergebnis = MaileingangErgebnis.NeuerAuftrag;
                eingang.Auftrag = auftrag;
            }

            await db.SaveChangesAsync(abbruch);
            log.LogInformation("Mail von {Von} verarbeitet: {Ergebnis}, Auftrag {Auftrag}.", adresse, eingang.Ergebnis, eingang.Auftrag?.Id);
            return eingang;
        }
        catch
        {
            ablage.Verwerfen(anhaenge);
            throw;
        }
    }

    /// <summary>
    /// Schneidet den zitierten Schriftwechsel ab. Bilder, die nur im
    /// abgeschnittenen Teil standen, gehören zu früheren Nachrichten und
    /// werden nicht noch einmal angehängt.
    /// </summary>
    private string OhneZitat(string text, List<Anhang> anhaenge, Dictionary<string, Anhang> bilderImText)
    {
        var gekuerzt = Mailtext.OhneZitat(text);
        var nurImZitat = bilderImText.Values
            .Where(bild => Markdowntext.Verweist(text, bild) && !Markdowntext.Verweist(gekuerzt, bild))
            .ToList();

        ablage.Verwerfen(nurImZitat);
        anhaenge.RemoveAll(nurImZitat.Contains);
        return gekuerzt;
    }

    /// <summary>Der Auftrag, dessen Nummer im Betreff steht.</summary>
    private async Task<Auftrag?> BezugAsync(string? betreff, CancellationToken abbruch)
    {
        var treffer = Auftragsnummer().Match(betreff ?? "");
        if (!treffer.Success || !int.TryParse(treffer.Groups[1].Value, out var nummer))
            return null;

        return await db.Auftraege
            .Include(a => a.Kunde)
            .Include(a => a.Gruppe)
            .Include(a => a.Bearbeiter)
            .FirstOrDefaultAsync(a => a.Id == nummer, abbruch);
    }

    /// <summary>Das Konto mit dieser Adresse, sonst das Systemkonto.</summary>
    private async Task<Benutzer> KontoAsync(string adresse, CancellationToken abbruch)
    {
        var konto = await db.Benutzer.FirstOrDefaultAsync(b => b.Email == adresse && b.Aktiv, abbruch);
        if (konto is not null)
            return konto;

        var system = await db.Benutzer.FirstOrDefaultAsync(b => b.Anmeldename == Systemkonto, abbruch);
        if (system is null)
        {
            // Nicht aktiv: Mit diesem Konto kann sich niemand anmelden.
            system = new Benutzer { Anmeldename = Systemkonto, Name = "E-Mail-Eingang", Aktiv = false };
            db.Benutzer.Add(system);
        }
        return system;
    }

    private async Task<Kunde> KundeAsync(MailboxAddress absender, string adresse, Benutzer konto, CancellationToken abbruch)
    {
        var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Email == adresse, abbruch);
        if (kunde is not null)
            return kunde;

        if (konto.Anmeldename != Systemkonto)
            return await zuordnung.FuerBenutzerAsync(konto.Id);

        var name = string.IsNullOrWhiteSpace(absender.Name) ? adresse : absender.Name.Trim();
        kunde = new Kunde { Name = Kuerzen(name, 200)!, Email = Kuerzen(adresse, 320) };
        db.Kunden.Add(kunde);
        return kunde;
    }

    /// <summary>Warum die Mail nicht verarbeitet wird, oder null.</summary>
    private string? Automatisch(MimeMessage mail, string? adresse)
    {
        if (adresse is null)
            return "Die Mail hat keinen Absender.";

        var eigene = mailOptionen.Value.Absender?.Trim().ToLowerInvariant();
        if (adresse == eigene)
            return "Die Mail kommt von der eigenen Absenderadresse.";

        var ort = adresse.Split('@')[0];
        if (ort is "mailer-daemon" or "postmaster")
            return "Zustellbericht eines Mailservers.";
        if (mail.Body is MultipartReport)
            return "Zustell- oder Lesebericht.";

        var automatisch = mail.Headers["Auto-Submitted"];
        if (automatisch is not null && !automatisch.Trim().StartsWith("no", StringComparison.OrdinalIgnoreCase))
            return "Automatisch erzeugte Mail, zum Beispiel eine Abwesenheitsnotiz.";

        var vorrang = mail.Headers["Precedence"]?.Trim().ToLowerInvariant();
        if (vorrang is "bulk" or "junk" or "list" or "auto_reply")
            return "Massen- oder Listenmail.";

        if (mail.Headers.Contains("X-Autoreply") || mail.Headers.Contains("X-Autorespond"))
            return "Automatische Antwort.";

        return null;
    }

    /// <summary>
    /// Speichert die Anhänge der Mail. Zu große werden ausgelassen und im
    /// Text genannt. Kleine Bilder im Text gelten als Logo und entfallen.
    /// </summary>
    private async Task<(List<Anhang> Anhaenge, List<string> Ausgelassen, Dictionary<string, Anhang> BilderImText)> AnhaengeAsync(
        MimeMessage mail, CancellationToken abbruch)
    {
        var anhaenge = new List<Anhang>();
        var ausgelassen = new List<string>();
        // Bilder, auf die der HTML-Teil mit cid: verweist, nach ihrer Kennung.
        var bilderImText = new Dictionary<string, Anhang>(StringComparer.OrdinalIgnoreCase);
        var mindestgroesseBild = optionen.Value.BilderImTextAbKb * 1024L;
        var html = mail.HtmlBody ?? "";

        try
        {
            foreach (var teil in mail.BodyParts.Concat(mail.Attachments).Distinct())
            {
                using var inhalt = new MemoryStream();
                string name;
                string? typ;
                string? kennung = null;

                if (teil is MessagePart { Message: not null } weitergeleitet)
                {
                    await weitergeleitet.Message.WriteToAsync(inhalt, abbruch);
                    name = (string.IsNullOrWhiteSpace(weitergeleitet.Message.Subject) ? "Weitergeleitete Mail" : weitergeleitet.Message.Subject.Trim()) + ".eml";
                    typ = "message/rfc822";
                }
                else if (teil is MimePart datei && datei.Content is not null)
                {
                    // Ein Bild steht im Text, wenn der HTML-Teil mit cid: darauf verweist.
                    // Ob das Mailprogramm es als "inline" oder als "Anhang" kennzeichnet, ist uneinheitlich.
                    var istBild = datei.ContentType.IsMimeType("image", "*");
                    var verwiesen = istBild && !string.IsNullOrEmpty(datei.ContentId)
                        && html.Contains("cid:" + datei.ContentId, StringComparison.OrdinalIgnoreCase);
                    var bildImText = verwiesen || (istBild && !datei.IsAttachment);
                    if (!datei.IsAttachment && !bildImText)
                        continue; // der Text der Mail selbst

                    await datei.Content.DecodeToAsync(inhalt, abbruch);
                    if (bildImText && inhalt.Length < mindestgroesseBild)
                        continue;

                    name = string.IsNullOrWhiteSpace(datei.FileName) ? $"Anhang.{datei.ContentType.MediaSubtype}" : datei.FileName;
                    typ = datei.ContentType.MimeType;
                    if (verwiesen)
                        kennung = datei.ContentId;
                }
                else
                {
                    continue;
                }

                if (inhalt.Length == 0)
                    continue;
                if (inhalt.Length > ablage.HoechstgroesseBytes)
                {
                    ausgelassen.Add($"{name} ({Anzeige.Dateigroesse(inhalt.Length)}) ist größer als {ablage.HoechstgroesseBytes / (1024 * 1024)} MB");
                    continue;
                }
                if (anhaenge.Count >= HoechstzahlAnhaenge)
                {
                    ausgelassen.Add($"{name}: mehr als {HoechstzahlAnhaenge} Anhänge");
                    continue;
                }

                inhalt.Position = 0;
                var anhang = await ablage.SpeichernAsync(name, typ, inhalt, abbruch);
                anhaenge.Add(anhang);
                if (!string.IsNullOrEmpty(kennung) && anhang.Bildtyp is not null)
                    bilderImText[kennung] = anhang;
            }
        }
        catch
        {
            ablage.Verwerfen(anhaenge);
            throw;
        }
        return (anhaenge, ausgelassen, bilderImText);
    }

    private static string? MitHinweis(string text, List<string> ausgelassen)
    {
        if (ausgelassen.Count == 0)
            return text;
        var hinweis = "[Nicht übernommene Anhänge: " + string.Join("; ", ausgelassen) + "]";
        return text.Length == 0 ? hinweis : $"{text}\n\n{hinweis}";
    }

    /// <summary>Die Message-ID, sonst ein Fingerabdruck aus Absender, Datum und Betreff.</summary>
    private static string Kennung(MimeMessage mail)
    {
        if (!string.IsNullOrWhiteSpace(mail.MessageId))
            return Kuerzen(mail.MessageId.Trim(), 500)!;

        var grundlage = $"{mail.From}|{mail.Date.UtcDateTime:O}|{mail.Subject}";
        return "ohne-id:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(grundlage)));
    }

    private static string? Kuerzen(string? text, int laenge) =>
        text is null ? null : text.Length <= laenge ? text : text[..laenge];

    [GeneratedRegex(@"\[AVIS-(\d{1,9})\]", RegexOptions.IgnoreCase)]
    private static partial Regex Auftragsnummer();
}
