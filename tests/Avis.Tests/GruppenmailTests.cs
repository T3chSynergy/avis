using System.Net;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

/// <summary>
/// Bernd ist Mitglied im Netzwerk, Anna und Clara sind es nicht. Jeder Test
/// stellt Bernds Einstellung selbst ein und räumt liegengebliebene Mails weg.
/// </summary>
public class GruppenmailTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Fact]
    public async Task Ohne_eigene_Einstellung_kommt_keine_Mail_zu_neuen_Auftraegen_der_Gruppe()
    {
        var anna = await AnfangAsync();
        Assert.False(await app.DatenbankAsync(db => db.Gruppenmitglieder.AnyAsync(m => m.MailBeiNeuemAuftrag)));

        await ErfassenAsync(anna, "Niemand hat etwas eingeschaltet", await NetzwerkAsync());

        Assert.Empty(await app.MailsVersendenAsync());
    }

    [Fact]
    public async Task Wer_es_einschaltet_bekommt_eine_Mail_wenn_ein_Auftrag_neu_in_der_Gruppe_liegt()
    {
        var anna = await AnfangAsync();
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var netzwerk = await NetzwerkAsync();

        var eingeschaltet = await EinstellenAsync(bernd, netzwerk, ein: true);
        Assert.Contains("Du bekommst jetzt eine Mail, wenn ein Auftrag neu in der Gruppe Netzwerk liegt.", await eingeschaltet.Content.ReadAsStringAsync());

        // Erfasst in der Gruppe.
        var erfasst = await ErfassenAsync(anna, "Patchkabel im Lager fehlen", netzwerk);
        var mail = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal("bernd.muster@example.org", mail.To.Mailboxes.Single().Address);
        Assert.Equal($"[AVIS-{erfasst}] Neu in Netzwerk: Patchkabel im Lager fehlen", mail.Subject);
        Assert.Contains("Hallo Bernd Muster,", mail.TextBody);
        Assert.Contains($"in der Gruppe Netzwerk liegt ein neuer Auftrag: {erfasst} „Patchkabel im Lager fehlen“ für Ohne Adresse.", mail.TextBody);
        Assert.Contains($"https://avis.test/Auftraege/{erfasst}", mail.TextBody);
        Assert.Contains("unter „Meine Gruppen“ je Gruppe abschalten", mail.TextBody);

        // Weitergeleitet in die Gruppe, mit dem Hinweis an die Gruppe.
        var weiter = await ErfassenAsync(anna, "Kommt aus der Hotline", await HotlineAsync());
        Assert.Empty(await app.MailsVersendenAsync());
        await anna.UebernehmenAsync(app, weiter);
        await anna.PostFormularAsync($"/Auftraege/{weiter}", $"/Auftraege/{weiter}?handler=Weiterleiten", new() { ["version"] = "2", ["gruppeId"] = netzwerk.ToString(), ["text"] = "Bitte den Port prüfen." });
        mail = Assert.Single(await app.MailsVersendenAsync());
        Assert.Equal($"[AVIS-{weiter}] Neu in Netzwerk: Kommt aus der Hotline", mail.Subject);
        Assert.Contains("Bitte den Port prüfen.", mail.TextBody);

        // Wieder ausgeschaltet kommt nichts mehr.
        var ausgeschaltet = await EinstellenAsync(bernd, netzwerk, ein: false);
        Assert.Contains("Für die Gruppe Netzwerk bekommst du keine Mails mehr.", await ausgeschaltet.Content.ReadAsStringAsync());
        await ErfassenAsync(anna, "Nach dem Ausschalten", netzwerk);
        Assert.Empty(await app.MailsVersendenAsync());
    }

    [Fact]
    public async Task Keine_Mail_an_die_Gruppe_wenn_man_selbst_handelt_bevorzugt_ist_oder_niemand_etwas_tun_muss()
    {
        var anna = await AnfangAsync();
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var netzwerk = await NetzwerkAsync();
        var berndId = await BenutzerIdAsync("bernd");
        await EinstellenAsync(bernd, netzwerk, ein: true);

        // Bernd erfasst selbst.
        await ErfassenAsync(bernd, "Von Bernd selbst erfasst", netzwerk);
        Assert.Empty(await app.MailsVersendenAsync());

        // Bernd ist bevorzugter Bearbeiter: nur die Mail "Für dich".
        var bevorzugt = await ErfassenAsync(anna, "Bernd soll es machen", netzwerk, new() { ["Eingabe.BevorzugtId"] = berndId.ToString() });
        Assert.Equal($"[AVIS-{bevorzugt}] Für dich: Bernd soll es machen", Assert.Single(await app.MailsVersendenAsync()).Subject);

        // Anna übernimmt gleich selbst: Der Auftrag liegt nie offen in der Gruppe.
        await ErfassenAsync(anna, "Gleich selbst übernommen", netzwerk, new() { ["Eingabe.SofortUebernehmen"] = "true" });
        Assert.Empty(await app.MailsVersendenAsync());

        // Bis zum Versand schon übernommen: Die Mail entfällt mit Begründung.
        var schnell = await ErfassenAsync(anna, "Schneller übernommen als gemailt", netzwerk);
        await anna.UebernehmenAsync(app, schnell);
        Assert.Empty(await app.MailsVersendenAsync());
        Assert.Contains("inzwischen übernommen", await HinweisAsync(schnell));

        // Bis zum Versand ausgeschaltet: Die Mail entfällt ebenfalls.
        var spaet = await ErfassenAsync(anna, "Zu spät ausgeschaltet", netzwerk);
        await EinstellenAsync(bernd, netzwerk, ein: false);
        Assert.Empty(await app.MailsVersendenAsync());
        Assert.Contains("Bernd Muster lässt sich über diese Gruppe nicht mehr benachrichtigen.", await HinweisAsync(spaet));
    }

    [Fact]
    public async Task Die_Einstellung_gehoert_zur_Mitgliedschaft()
    {
        await AnfangAsync();
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var clara = await app.AnmeldenAlsAsync("clara");
        var netzwerk = await NetzwerkAsync();

        // Wer kein Mitglied ist, kann nichts einschalten.
        Assert.Equal(HttpStatusCode.NotFound, (await EinstellenAsync(clara, netzwerk, ein: true)).StatusCode);
        Assert.False(await app.DatenbankAsync(db => db.Gruppenmitglieder.AnyAsync(m => m.MailBeiNeuemAuftrag)));

        // Die Seite zeigt den Schalter nur bei den eigenen Gruppen und den Stand.
        await EinstellenAsync(bernd, netzwerk, ein: true);
        var seite = await bernd.GetStringAsync("/Gruppen/Meine");
        Assert.Contains("Die Mails gehen an bernd.muster@example.org.", seite);
        Assert.Contains("aria-label=\"Mail bei neuem Auftrag in Netzwerk ausschalten\"", seite);
        Assert.Contains("aria-label=\"Mail bei neuem Auftrag in Systemverwaltung einschalten\"", seite);
        Assert.DoesNotContain("Mail bei neuem Auftrag in IT-Hotline", seite);

        // Mit dem Austreten verschwindet die Einstellung, nach dem Beitreten ist sie wieder aus.
        await bernd.PostFormularAsync("/Gruppen/Meine", $"/Gruppen/Meine?handler=Austreten&id={netzwerk}", []);
        await bernd.PostFormularAsync("/Gruppen/Meine", $"/Gruppen/Meine?handler=Beitreten&id={netzwerk}", []);
        var berndId = await BenutzerIdAsync("bernd");
        var mitglied = await app.DatenbankAsync(db => db.Gruppenmitglieder.SingleAsync(m => m.GruppeId == netzwerk && m.BenutzerId == berndId));
        Assert.False(mitglied.MailBeiNeuemAuftrag);
    }

    /// <summary>Meldet Anna an, schaltet alle Mails zur Gruppe aus und räumt weg, was frühere Tests hinterlassen haben.</summary>
    private async Task<HttpClient> AnfangAsync()
    {
        await app.DatenbankAsync(db => db.Gruppenmitglieder.ExecuteUpdateAsync(m => m.SetProperty(x => x.MailBeiNeuemAuftrag, false)));
        await app.MailsVersendenAsync();
        return await app.AnmeldenAlsAsync("anna");
    }

    private static Task<HttpResponseMessage> EinstellenAsync(HttpClient client, int gruppe, bool ein) =>
        client.PostFormularAsync("/Gruppen/Meine", $"/Gruppen/Meine?handler=Mail&id={gruppe}", new() { ["ein"] = ein ? "true" : "false" });

    /// <summary>Erfasst für einen neuen Kunden ohne Adresse, damit nur die Mails an Bearbeiter zählen.</summary>
    private static async Task<int> ErfassenAsync(HttpClient client, string betreff, int gruppe, Dictionary<string, string>? weitere = null)
    {
        var felder = new Dictionary<string, string>(weitere ?? [])
        {
            ["Eingabe.KundeName"] = "Ohne Adresse",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = gruppe.ToString(),
        };
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", felder);
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private Task<int> NetzwerkAsync() => app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Netzwerk").Select(g => g.Id).FirstAsync());

    private Task<int> HotlineAsync() => app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync());

    private Task<int> BenutzerIdAsync(string anmeldename) =>
        app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == anmeldename).Select(b => b.Id).FirstAsync());

    /// <summary>Warum die Mail an ein Mitglied der Gruppe zu diesem Auftrag entfallen ist.</summary>
    private Task<string?> HinweisAsync(int auftragId) => app.DatenbankAsync(db => db.Benachrichtigungen
        .Where(b => b.Verlaufseintrag.AuftragId == auftragId && b.EmpfaengerId != null && b.Status == BenachrichtigungStatus.Entfallen)
        .Select(b => b.Hinweis)
        .SingleAsync());
}
