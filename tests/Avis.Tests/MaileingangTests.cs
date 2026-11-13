using System.Net;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Posteingang;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Avis.Tests;

public class MaileingangTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Fact]
    public async Task Eine_Mail_eines_bekannten_Kunden_wird_ein_Auftrag_bei_der_Hotline()
    {
        await app.MailsVersendenAsync();

        var eingang = await AnnehmenAsync(Mail("Frank Müller <Frank.Mueller@Example.org>", "Beamer im Sitzungssaal defekt", "Der Beamer bleibt dunkel.\r\n\r\nGruß\r\nFrank"));

        Assert.Equal(MaileingangErgebnis.NeuerAuftrag, eingang.Ergebnis);
        var auftrag = await LadenAsync(eingang.AuftragId!.Value);
        Assert.Equal(("Beamer im Sitzungssaal defekt", "Der Beamer bleibt dunkel.\n\nGruß\nFrank"), (auftrag.Betreff, auftrag.Beschreibung));
        Assert.Equal((Eingang.EMail, AuftragStatus.Offen), (auftrag.Eingang, auftrag.Status));
        Assert.True(auftrag.Gruppe.IstEingangsgruppe);
        Assert.Equal("Frank Müller", auftrag.Kunde.Name);
        Assert.Equal("E-Mail-Eingang", auftrag.ErfasstVon.Name);

        // Die Bestätigung an den Kunden trägt die Auftragsnummer im Betreff.
        var bestaetigung = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal("frank.mueller@example.org", bestaetigung.To.Mailboxes.Single().Address);
        Assert.StartsWith($"[AVIS-{auftrag.Id}] Erfasst:", bestaetigung.Subject);
        Assert.Equal("All", bestaetigung.Headers["X-Auto-Response-Suppress"]);
    }

    [Fact]
    public async Task Ein_unbekannter_Absender_wird_als_Kunde_angelegt()
    {
        var eingang = await AnnehmenAsync(Mail("\"Neumann, Vera\" <vera.neumann@example.org>", "", "Ich brauche einen Zugang."));

        var auftrag = await LadenAsync(eingang.AuftragId!.Value);
        Assert.Equal("(ohne Betreff)", auftrag.Betreff);
        Assert.Equal(("Neumann, Vera", "vera.neumann@example.org", null), (auftrag.Kunde.Name, auftrag.Kunde.Email, auftrag.Kunde.Anmeldename));

        // Die nächste Mail derselben Person legt keinen zweiten Kunden an.
        var zweite = await AnnehmenAsync(Mail("vera.neumann@example.org", "Noch etwas", "Und ein Telefon."));
        Assert.Equal(auftrag.KundeId, (await LadenAsync(zweite.AuftragId!.Value)).KundeId);
    }

    [Fact]
    public async Task Der_Absender_mit_Anmeldekonto_wird_dessen_Kunde()
    {
        var eingang = await AnnehmenAsync(Mail("clara.probe@example.org", "Mein Headset", "Ist defekt."));

        var auftrag = await LadenAsync(eingang.AuftragId!.Value);
        Assert.Equal(("Clara Probe", "clara"), (auftrag.Kunde.Name, auftrag.Kunde.Anmeldename));
        Assert.Equal("Clara Probe", auftrag.ErfasstVon.Name);
    }

    [Fact]
    public async Task Die_Antwort_des_Kunden_mit_Auftragsnummer_landet_im_Auftrag_ohne_das_Zitat()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = (await AnnehmenAsync(Mail("gabi.mueller@example.org", "Drucker druckt blass", "Bitte Toner tauschen."))).AuftragId!.Value;
        await anna.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=Uebernehmen", new() { ["version"] = "1" });
        await app.MailsVersendenAsync();

        var antwort = await AnnehmenAsync(Mail("gabi.mueller@example.org", $"AW: [AVIS-{id}] Nachricht: Drucker druckt blass",
            "Es ist der Drucker im 3. OG.\r\n\r\nViele Grüße\r\nGabi\r\n\r\n-----Ursprüngliche Nachricht-----\r\nVon: IT-Hotline <it-hotline@avis.test>\r\nGesendet: Freitag, 13. November 2026 10:00\r\nAn: Gabi Müller\r\nBetreff: ...\r\n\r\nWelcher Drucker ist es?"));

        Assert.Equal((MaileingangErgebnis.Antwort, id), (antwort.Ergebnis, antwort.AuftragId!.Value));
        var eintrag = (await LadenAsync(id)).Verlauf.OrderBy(v => v.Id).Last();
        Assert.Equal(VerlaufArt.AntwortDesKunden, eintrag.Art);
        Assert.Equal("Es ist der Drucker im 3. OG.\n\nViele Grüße\nGabi", eintrag.Text);

        // Die zuständige Bearbeiterin erfährt es per Mail.
        var hinweis = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal("anna.beispiel@example.org", hinweis.To.Mailboxes.Single().Address);
        Assert.Contains("Es ist der Drucker im 3. OG.", hinweis.TextBody);
    }

    [Fact]
    public async Task Eine_Antwort_auf_einen_abgeschlossenen_Auftrag_oeffnet_ihn_wieder()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = (await AnnehmenAsync(Mail("gabi.mueller@example.org", "Passwort vergessen", "Bitte zurücksetzen."))).AuftragId!.Value;
        await anna.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=Uebernehmen", new() { ["version"] = "1" });
        await anna.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=Abschliessen", new() { ["version"] = "2", ["text"] = "Zurückgesetzt." });

        var antwort = await AnnehmenAsync(Mail("gabi.mueller@example.org", $"AW: [avis-{id}] Abgeschlossen: Passwort vergessen", "Das neue Passwort geht nicht."));

        Assert.Equal(MaileingangErgebnis.WiederGeoeffnet, antwort.Ergebnis);
        var auftrag = await LadenAsync(id);
        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.Equal(VerlaufArt.VomKundenWiederGeoeffnet, auftrag.Verlauf.OrderBy(v => v.Id).Last().Art);
    }

    [Fact]
    public async Task Ein_Fremder_schreibt_per_Mail_nicht_in_den_Auftrag_eines_anderen()
    {
        var id = (await AnnehmenAsync(Mail("gabi.mueller@example.org", "Vertrauliche Sache", "Nur für die IT."))).AuftragId!.Value;
        var eintraege = (await LadenAsync(id)).Verlauf.Count;

        var fremd = await AnnehmenAsync(Mail("jemand.anderes@example.org", $"WG: [AVIS-{id}] Vertrauliche Sache", "Ich hänge mich hier dran."));

        Assert.Equal(MaileingangErgebnis.NeuerAuftrag, fremd.Ergebnis);
        Assert.NotEqual(id, fremd.AuftragId!.Value);
        Assert.Equal(eintraege, (await LadenAsync(id)).Verlauf.Count);
        var neu = await LadenAsync(fremd.AuftragId.Value);
        Assert.StartsWith($"[Diese Mail nennt den Auftrag {id}, kommt aber nicht von dessen Kunden.", neu.Beschreibung);
        Assert.Contains("Ich hänge mich hier dran.", neu.Beschreibung);
    }

    [Fact]
    public async Task Die_Mail_des_zustaendigen_Bearbeiters_wird_ein_interner_Kommentar()
    {
        var id = (await AnnehmenAsync(Mail("dieter.kunde@example.org", "Monitor flimmert", "Seit heute."))).AuftragId!.Value;
        var bernd = await app.AnmeldenAlsAsync("bernd");
        await bernd.UebernehmenAsync(app, id);

        var eingang = await AnnehmenAsync(Mail("bernd.muster@example.org", $"AW: [AVIS-{id}] Monitor flimmert", "NUR-INTERN: Kabel ist bestellt."));

        Assert.Equal(MaileingangErgebnis.InternerKommentar, eingang.Ergebnis);
        var eintrag = (await app.DatenbankAsync(db => db.Verlauf.AsNoTracking().Include(v => v.Benutzer)
            .Where(v => v.AuftragId == id).OrderByDescending(v => v.Id).FirstAsync()));
        Assert.Equal((VerlaufArt.InternerKommentar, "Bernd Muster"), (eintrag.Art, eintrag.Benutzer.Name));

        var dieter = await app.AnmeldenAlsAsync("dieter");
        Assert.DoesNotContain("NUR-INTERN", await dieter.GetStringAsync($"/Meine/{id}"));
    }

    [Fact]
    public async Task Die_Mail_eines_Bearbeiters_der_den_Auftrag_nicht_bearbeitet_landet_nicht_im_Auftrag()
    {
        var id = (await AnnehmenAsync(Mail("dieter.kunde@example.org", "Tastatur prellt", "Seit gestern."))).AuftragId!.Value;
        var eintraege = (await LadenAsync(id)).Verlauf.Count;

        // Clara hat den Auftrag nicht übernommen.
        var eingang = await AnnehmenAsync(Mail("clara.probe@example.org", $"AW: [AVIS-{id}] Tastatur prellt", "Ich mische mich ein."));

        Assert.Equal(MaileingangErgebnis.NeuerAuftrag, eingang.Ergebnis);
        Assert.NotEqual(id, eingang.AuftragId!.Value);
        Assert.Equal(eintraege, (await LadenAsync(id)).Verlauf.Count);
    }

    [Fact]
    public async Task Automatische_Mails_werden_uebergangen()
    {
        var vorher = await app.DatenbankAsync(db => db.Auftraege.CountAsync());

        var abwesend = Mail("gabi.mueller@example.org", "Automatische Antwort: [AVIS-1] Erfasst", "Ich bin bis Montag abwesend.");
        abwesend.Headers.Add("Auto-Submitted", "auto-replied");
        var rundmail = Mail("newsletter@example.org", "Neuigkeiten", "Viele Angebote.");
        rundmail.Headers.Add("Precedence", "bulk");
        var zustellbericht = Mail("MAILER-DAEMON@example.org", "Undelivered Mail Returned to Sender", "Zustellung fehlgeschlagen.");
        var eigene = Mail("IT-Hotline <it-hotline@avis.test>", "[AVIS-1] Erfasst: Schleife", "Diese Mail stammt von AVIS selbst.");

        foreach (var mail in new[] { abwesend, rundmail, zustellbericht, eigene })
        {
            var eingang = await AnnehmenAsync(mail);
            Assert.Equal(MaileingangErgebnis.Uebergangen, eingang.Ergebnis);
            Assert.Null(eingang.AuftragId);
            Assert.NotNull(eingang.Hinweis);
        }
        Assert.Equal(vorher, await app.DatenbankAsync(db => db.Auftraege.CountAsync()));

        // Eine von Hand geschriebene Mail trägt "Auto-Submitted: no" und zählt.
        var vonHand = Mail("gabi.mueller@example.org", "Von Hand", "Text.");
        vonHand.Headers.Add("Auto-Submitted", "no");
        Assert.Equal(MaileingangErgebnis.NeuerAuftrag, (await AnnehmenAsync(vonHand)).Ergebnis);
    }

    [Fact]
    public async Task Dieselbe_Mail_wird_nur_einmal_verarbeitet()
    {
        var mail = Mail("gabi.mueller@example.org", "Nur einmal bitte", "Text.");
        var vorher = await app.DatenbankAsync(db => db.Auftraege.CountAsync());

        var erste = await AnnehmenAsync(mail);
        var zweite = await AnnehmenAsync(mail);

        Assert.Equal(erste.Id, zweite.Id);
        Assert.Equal(vorher + 1, await app.DatenbankAsync(db => db.Auftraege.CountAsync()));
    }

    [Fact]
    public async Task Anhaenge_werden_uebernommen_Logos_und_zu_grosse_Dateien_nicht()
    {
        var gestaltet = new BodyBuilder { TextBody = "Siehe Anhang.", HtmlBody = "<p>Siehe Anhang.</p>" };
        gestaltet.Attachments.Add("fehler.log", "Fehler 42\r\n"u8.ToArray(), new ContentType("text", "plain"));
        gestaltet.Attachments.Add("riesig.iso", new byte[1024 * 1024 + 5], new ContentType("application", "octet-stream"));
        gestaltet.LinkedResources.Add("logo.png", new byte[2_000], new ContentType("image", "png"));
        gestaltet.LinkedResources.Add("bildschirmfoto.png", new byte[40_000], new ContentType("image", "png"));
        var mail = Mail("gabi.mueller@example.org", "Mit Anhängen", "");
        mail.Body = gestaltet.ToMessageBody();

        var eingang = await AnnehmenAsync(mail);

        var id = eingang.AuftragId!.Value;
        var anhaenge = await app.DatenbankAsync(db => db.Anhaenge.AsNoTracking()
            .Where(a => a.Verlaufseintrag.AuftragId == id).OrderBy(a => a.Dateiname).ToListAsync());
        Assert.Equal(["bildschirmfoto.png", "fehler.log"], anhaenge.Select(a => a.Dateiname));
        Assert.Equal(11, anhaenge[1].Groesse);
        var auftrag = await LadenAsync(id);
        Assert.StartsWith("Siehe Anhang.", auftrag.Beschreibung);
        Assert.Contains("[Nicht übernommene Anhänge: riesig.iso (1,0 MB) ist größer als 1 MB]", auftrag.Beschreibung);

        var anna = await app.AnmeldenAlsAsync("anna");
        Assert.Equal("Fehler 42\r\n", await anna.GetStringAsync($"/Anhaenge/{anhaenge[1].Id}"));
    }

    [Fact]
    public async Task Aus_einer_reinen_HTML_Mail_wird_lesbarer_Text()
    {
        var mail = Mail("gabi.mueller@example.org", "Nur HTML", "");
        mail.Body = new TextPart("html")
        {
            Text = "<html><head><style>p { color: red }</style></head><body><p>Guten Tag,</p><p>die Maus &amp; die Tastatur<br>sind defekt.</p><div>Gr&uuml;&szlig;e</div></body></html>",
        };

        var eingang = await AnnehmenAsync(mail);

        Assert.Equal("Guten Tag,\n\ndie Maus & die Tastatur\nsind defekt.\nGrüße", (await LadenAsync(eingang.AuftragId!.Value)).Beschreibung);
    }

    [Fact]
    public async Task Die_Verwaltung_zeigt_was_mit_den_Mails_geschah()
    {
        await AnnehmenAsync(Mail("gabi.mueller@example.org", "Erscheint im Posteingang", "Text."));
        var anna = await app.AnmeldenAlsAsync("anna");

        var seite = await anna.GetStringAsync("/Verwaltung/Posteingang");

        Assert.Contains("Erscheint im Posteingang", seite);
        Assert.Contains("neuer Auftrag", seite);
        // Das Konto des E-Mail-Eingangs taucht in der Benutzerverwaltung nicht auf.
        Assert.DoesNotContain("avis:e-mail-eingang", await anna.GetStringAsync("/Verwaltung/Benutzer"));
        var bernd = await app.AnmeldenAlsAsync("bernd");
        Assert.Contains("Kein Zugriff", await bernd.GetStringAsync("/Verwaltung/Posteingang"));
    }

    [Theory]
    [InlineData("Neuer Text.\n\n-----Ursprüngliche Nachricht-----\nVon: A\nAlt.", "Neuer Text.")]
    [InlineData("Neuer Text.\n\nVon: IT-Hotline <it@x>\nGesendet: Montag\nAn: B\n\nAlt.", "Neuer Text.")]
    [InlineData("Neuer Text.\n\nAm 13.11.2026 um 10:00 schrieb IT-Hotline:\n> Alt.\n> Noch älter.", "Neuer Text.")]
    [InlineData("Neuer Text.\n________________________________\nFrom: A\nSent: B\n\nAlt.", "Neuer Text.")]
    [InlineData("Neuer Text.\n\n> zitiert\n> zitiert\n", "Neuer Text.")]
    [InlineData("Von: Herrn Meier habe ich gehört,\ndass der Drucker steht.", "Von: Herrn Meier habe ich gehört,\ndass der Drucker steht.")]
    [InlineData("-----Ursprüngliche Nachricht-----\nNur Zitat.", "-----Ursprüngliche Nachricht-----\nNur Zitat.")]
    public void Der_zitierte_Schriftwechsel_wird_abgeschnitten(string text, string erwartet)
    {
        Assert.Equal(erwartet, Mailtext.OhneZitat(text));
    }

    /// <summary>
    /// Prüft den Abruf gegen einen echten IMAP-Server. Läuft nur, wenn
    /// AVIS_TEST_IMAP gesetzt ist, in der Form host:port:benutzer:kennwort.
    /// </summary>
    [Fact]
    public async Task Der_Abruf_ueber_IMAP_holt_ungelesene_Mails_und_markiert_sie_als_gelesen()
    {
        var ziel = Environment.GetEnvironmentVariable("AVIS_TEST_IMAP")?.Split(':');
        if (ziel is not { Length: 4 })
            return;

        var posteingang = new ImapPosteingang(
            Microsoft.Extensions.Options.Options.Create(new PosteingangOptionen
            {
                Art = PosteingangOptionen.Imap,
                Server = ziel[0],
                Port = int.Parse(ziel[1]),
                Benutzer = ziel[2],
                Kennwort = ziel[3],
                Verschluesselung = "Keine",
                Erledigtordner = Environment.GetEnvironmentVariable("AVIS_TEST_IMAP_ERLEDIGT"),
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ImapPosteingang>.Instance);

        var geholt = new List<MimeMessage>();
        var erster = await posteingang.AbrufenAsync((mail, _) => { geholt.Add(mail); return Task.CompletedTask; }, CancellationToken.None);
        var zweiter = await posteingang.AbrufenAsync((mail, _) => { geholt.Add(mail); return Task.CompletedTask; }, CancellationToken.None);

        Assert.True(erster > 0, "Im Postfach lag keine ungelesene Mail.");
        Assert.Equal(0, zweiter);
        Assert.Equal(erster, geholt.Count);
        foreach (var mail in geholt)
        {
            var eingang = await AnnehmenAsync(mail);
            Assert.Equal(MaileingangErgebnis.NeuerAuftrag, eingang.Ergebnis);
        }
    }

    private static MimeMessage Mail(string von, string betreff, string text)
    {
        var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(von));
        mail.To.Add(MailboxAddress.Parse("it-hotline@avis.test"));
        mail.Subject = betreff;
        mail.Body = new TextPart("plain") { Text = text };
        mail.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId("example.org");
        return mail;
    }

    private async Task<Maileingang> AnnehmenAsync(MimeMessage mail)
    {
        // Wie nach dem Abruf: Die Mail wird einmal in ihre Rohform und zurück gewandelt,
        // mit den Zeilenenden, die eine Mail unterwegs immer hat – auf jedem Rechner.
        var unterwegs = FormatOptions.Default.Clone();
        unterwegs.NewLineFormat = NewLineFormat.Dos;
        using var roh = new MemoryStream();
        await mail.WriteToAsync(unterwegs, roh);
        roh.Position = 0;
        var gelesen = await MimeMessage.LoadAsync(roh);

        using var scope = app.Services.CreateScope();
        var eingang = await scope.ServiceProvider.GetRequiredService<Mailannahme>().AnnehmenAsync(gelesen);
        return eingang;
    }

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Kunde)
        .Include(a => a.Gruppe)
        .Include(a => a.ErfasstVon)
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
