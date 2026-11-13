using System.Net;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

public class VerwaltungTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Theory]
    [InlineData("/Verwaltung/Gruppen")]
    [InlineData("/Verwaltung/Gruppen/1")]
    [InlineData("/Verwaltung/Benutzer")]
    [InlineData("/Verwaltung/Benutzer/1")]
    [InlineData("/Verwaltung/Kategorien")]
    [InlineData("/Verwaltung/Standorte")]
    public async Task Seiten_der_Verwaltung_antworten_fuer_Administratoren(string pfad)
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var antwort = await anna.GetAsync(pfad);

        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
    }

    [Fact]
    public async Task Bearbeiter_ohne_Administratorrolle_kommen_nicht_in_die_Verwaltung()
    {
        var bernd = await app.AnmeldenAlsAsync("bernd");

        var seite = await bernd.GetStringAsync("/Verwaltung/Gruppen");
        var anlegen = await bernd.PostFormularAsync("/Gruppen/Meine", "/Verwaltung/Gruppen", new() { ["name"] = "Heimlich" });

        Assert.Contains("Kein Zugriff", seite);
        Assert.Contains("Kein Zugriff", await anlegen.Content.ReadAsStringAsync());
        Assert.False(await app.DatenbankAsync(db => db.Gruppen.AnyAsync(g => g.Name == "Heimlich")));
    }

    [Fact]
    public async Task Eine_Gruppe_wird_angelegt_und_ein_doppelter_Name_abgelehnt()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        await anna.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Gruppen", new() { ["name"] = "Telefonie", ["beschreibung"] = "Telefonanlage" });
        var doppelt = await anna.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Gruppen", new() { ["name"] = "telefonie" });

        Assert.Contains("gibt es schon", await doppelt.Content.ReadAsStringAsync());
        Assert.Equal(1, await app.DatenbankAsync(db => db.Gruppen.CountAsync(g => g.Name.ToLower() == "telefonie")));
    }

    [Fact]
    public async Task Eine_neue_Gruppe_steht_Bearbeitern_zur_Auswahl_und_eine_deaktivierte_nicht_mehr()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await GruppeAnlegenAsync(anna, "Druckdienst");
        Assert.Contains("Druckdienst", await anna.GetStringAsync("/Gruppen/Meine"));
        Assert.Contains("Druckdienst", await anna.GetStringAsync("/Auftraege/Neu"));

        await GruppeSpeichernAsync(anna, id, "Druckdienst", aktiv: false);

        Assert.DoesNotContain("Druckdienst", await anna.GetStringAsync("/Gruppen/Meine"));
        Assert.DoesNotContain("Druckdienst", await anna.GetStringAsync("/Auftraege/Neu"));
    }

    [Fact]
    public async Task Eine_Gruppe_mit_laufenden_Auftraegen_laesst_sich_nicht_deaktivieren()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await GruppeAnlegenAsync(anna, "Videokonferenz");
        await AuftragErfassenAsync(anna, "Kamera defekt", id);

        var antwort = await GruppeSpeichernAsync(anna, id, "Videokonferenz", aktiv: false);

        Assert.Contains("solange sie laufende Aufträge hat (derzeit 1)", await antwort.Content.ReadAsStringAsync());
        Assert.True(await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Id == id).Select(g => g.Aktiv).FirstAsync()));
    }

    [Fact]
    public async Task Es_gibt_immer_genau_eine_Eingangsgruppe()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var bisher = await app.DatenbankAsync(db => db.Gruppen.FirstAsync(g => g.IstEingangsgruppe));
        var neu = await GruppeAnlegenAsync(anna, "Servicepunkt");

        // Die Eingangsgruppe lässt sich weder abwählen noch deaktivieren.
        await GruppeSpeichernAsync(anna, bisher.Id, bisher.Name, aktiv: false);
        Assert.Equal([bisher.Id], await EingangsgruppenAsync());
        Assert.True(await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Id == bisher.Id).Select(g => g.Aktiv).FirstAsync()));

        // Eine andere Gruppe übernimmt die Rolle.
        await GruppeSpeichernAsync(anna, neu, "Servicepunkt", aktiv: true, eingangsgruppe: true);
        Assert.Equal([neu], await EingangsgruppenAsync());

        await GruppeSpeichernAsync(anna, bisher.Id, bisher.Name, aktiv: true, eingangsgruppe: true);
        Assert.Equal([bisher.Id], await EingangsgruppenAsync());

        Task<List<int>> EingangsgruppenAsync() =>
            app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).ToListAsync());
    }

    [Fact]
    public async Task Der_Administrator_traegt_Mitglieder_ein_und_aus()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var gruppe = await GruppeAnlegenAsync(anna, "Lizenzen");
        var bernd = await BenutzerIdAsync("bernd");
        var seite = $"/Verwaltung/Gruppen/{gruppe}";

        await anna.PostFormularAsync(seite, $"{seite}?handler=MitgliedHinzufuegen", new() { ["benutzerId"] = bernd.ToString() });
        Assert.True(await IstMitgliedAsync());

        await anna.PostFormularAsync(seite, $"{seite}?handler=MitgliedEntfernen&benutzerId={bernd}", []);
        Assert.False(await IstMitgliedAsync());

        Task<bool> IstMitgliedAsync() => app.DatenbankAsync(db =>
            db.Gruppen.AnyAsync(g => g.Id == gruppe && g.Mitglieder.Any(m => m.Id == bernd)));
    }

    [Fact]
    public async Task Eine_Kategorie_wird_angelegt_umbenannt_und_deaktiviert()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        const string seite = "/Verwaltung/Kategorien";

        await anna.PostFormularAsync(seite, $"{seite}?handler=Anlegen", new() { ["name"] = "Umzug" });
        var id = await app.DatenbankAsync(db => db.Kategorien.Where(k => k.Name == "Umzug").Select(k => k.Id).FirstAsync());
        Assert.Contains("Umzug", await anna.GetStringAsync("/Auftraege/Neu"));

        await anna.PostFormularAsync(seite, $"{seite}?handler=Speichern&id={id}", new() { ["name"] = "Büroumzug", ["aktiv"] = "true" });
        Assert.Contains("Büroumzug", await anna.GetStringAsync("/Auftraege/Neu"));

        await anna.PostFormularAsync(seite, $"{seite}?handler=Speichern&id={id}", new() { ["name"] = "Büroumzug" });
        Assert.DoesNotContain("Büroumzug", await anna.GetStringAsync("/Auftraege/Neu"));
        Assert.Contains("Büroumzug", await anna.GetStringAsync(seite));
    }

    [Fact]
    public async Task Ein_doppelter_Standort_wird_abgelehnt()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        const string seite = "/Verwaltung/Standorte";

        var antwort = await anna.PostFormularAsync(seite, $"{seite}?handler=Anlegen", new() { ["name"] = "haupthaus" });

        Assert.Contains("gibt es schon", await antwort.Content.ReadAsStringAsync());
        Assert.Equal(1, await app.DatenbankAsync(db => db.Standorte.CountAsync(s => s.Name.ToLower() == "haupthaus")));
    }

    [Fact]
    public async Task Ein_vorab_angelegter_Bearbeiter_kann_sofort_arbeiten()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        await BenutzerAnlegenAsync(anna, @"Amt\Fritz", "Fritz Neu", "Bearbeiter");

        var fritz = await app.AnmeldenAlsAsync(@"amt\fritz");
        Assert.Equal(HttpStatusCode.OK, (await fritz.GetAsync("/Gruppen")).StatusCode);
        Assert.Contains("Kein Zugriff", await fritz.GetStringAsync("/Verwaltung/Benutzer"));
    }

    [Fact]
    public async Task Wer_die_Rolle_Bearbeiter_verliert_gibt_seine_Auftraege_an_die_Gruppen_zurueck()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        await BenutzerAnlegenAsync(anna, "gerda", "Gerda Geht", "Bearbeiter");
        var gerdaId = await BenutzerIdAsync("gerda");
        var gruppe = await GruppeAnlegenAsync(anna, "Archiv");
        var gruppenseite = $"/Verwaltung/Gruppen/{gruppe}";
        await anna.PostFormularAsync(gruppenseite, $"{gruppenseite}?handler=MitgliedHinzufuegen", new() { ["benutzerId"] = gerdaId.ToString() });

        var auftrag = await AuftragErfassenAsync(anna, "Altakten scannen", gruppe);
        var gerda = await app.AnmeldenAlsAsync("gerda");
        await gerda.PostFormularAsync($"/Auftraege/{auftrag}", $"/Auftraege/{auftrag}?handler=Uebernehmen", new() { ["version"] = "1" });
        Assert.Equal(gerdaId, (await LadenAsync(auftrag)).BearbeiterId);

        var seite = $"/Verwaltung/Benutzer/{gerdaId}";
        var antwort = await anna.PostFormularAsync(seite, seite, new() { ["name"] = "Gerda Geht", ["rolle"] = "Keine", ["aktiv"] = "true" });

        Assert.Contains("1 Auftrag liegt wieder offen", await antwort.Content.ReadAsStringAsync());
        var danach = await LadenAsync(auftrag);
        Assert.Equal(AuftragStatus.Offen, danach.Status);
        Assert.Equal(gruppe, danach.GruppeId);
        Assert.Equal(VerlaufArt.Weitergeleitet, danach.Verlauf.OrderBy(v => v.Id).Last().Art);
        Assert.False(await app.DatenbankAsync(db => db.Gruppen.AnyAsync(g => g.Mitglieder.Any(m => m.Id == gerdaId))));
        Assert.Contains("Kein Zugriff", await gerda.GetStringAsync("/Gruppen"));
    }

    [Fact]
    public async Task Ein_deaktivierter_Benutzer_hat_keinen_Zugriff_mehr()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        await BenutzerAnlegenAsync(anna, "horst", "Horst Fort", "Bearbeiter");
        var horst = await app.AnmeldenAlsAsync("horst");
        var seite = $"/Verwaltung/Benutzer/{await BenutzerIdAsync("horst")}";

        await anna.PostFormularAsync(seite, seite, new() { ["name"] = "Horst Fort", ["rolle"] = "Bearbeiter" });

        Assert.Contains("Kein Zugriff", await horst.GetStringAsync("/"));
    }

    [Fact]
    public async Task Am_eigenen_Konto_aendert_niemand_Rolle_oder_Status()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var seite = $"/Verwaltung/Benutzer/{await BenutzerIdAsync("anna")}";

        await anna.PostFormularAsync(seite, seite, new() { ["name"] = "Anna Beispiel", ["email"] = "Anna@Example.org", ["rolle"] = "Keine" });

        var danach = await app.DatenbankAsync(db => db.Benutzer.FirstAsync(b => b.Anmeldename == "anna"));
        Assert.True(danach is { IstAdministrator: true, IstBearbeiter: true, Aktiv: true });
        Assert.Equal("anna@example.org", danach.Email);
    }

    private async Task<int> GruppeAnlegenAsync(HttpClient client, string name)
    {
        await client.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Gruppen", new() { ["name"] = name });
        return await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == name).Select(g => g.Id).FirstAsync());
    }

    private static Task<HttpResponseMessage> GruppeSpeichernAsync(
        HttpClient client, int id, string name, bool aktiv, bool eingangsgruppe = false)
    {
        var felder = new Dictionary<string, string> { ["name"] = name };
        if (aktiv)
            felder["aktiv"] = "true";
        if (eingangsgruppe)
            felder["eingangsgruppe"] = "true";
        return client.PostFormularAsync($"/Verwaltung/Gruppen/{id}", $"/Verwaltung/Gruppen/{id}?handler=Speichern", felder);
    }

    private static Task<HttpResponseMessage> BenutzerAnlegenAsync(HttpClient client, string anmeldename, string name, string rolle) =>
        client.PostFormularAsync("/Verwaltung/Benutzer", "/Verwaltung/Benutzer", new()
        {
            ["anmeldename"] = anmeldename,
            ["name"] = name,
            ["rolle"] = rolle,
        });

    private static async Task<int> AuftragErfassenAsync(HttpClient client, string betreff, int gruppeId)
    {
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = gruppeId.ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private Task<int> BenutzerIdAsync(string anmeldename) =>
        app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == anmeldename).Select(b => b.Id).FirstAsync());

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
