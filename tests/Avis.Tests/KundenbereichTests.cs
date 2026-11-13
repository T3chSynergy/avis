using System.Net;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

public class KundenbereichTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Fact]
    public async Task Wer_kein_Bearbeiter_ist_landet_bei_den_eigenen_Auftraegen()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");

        var antwort = await dieter.GetAsync("/");

        Assert.Equal("/Meine", antwort.RequestMessage!.RequestUri!.AbsolutePath);
        var text = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Drucker im Flur druckt nicht", text);
        Assert.Contains("Zweiter Monitor", text);
        Assert.DoesNotContain("WLAN im Besprechungsraum", text);
    }

    [Fact]
    public async Task Fremde_Auftraege_und_die_Seiten_der_Bearbeiter_bleiben_verschlossen()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var fremd = await app.DatenbankAsync(db => db.Auftraege.Where(a => a.Betreff.StartsWith("WLAN")).Select(a => a.Id).FirstAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await dieter.GetAsync($"/Meine/{fremd}")).StatusCode);
        var antwort = await dieter.PostFormularAsync("/Meine/1", $"/Meine/{fremd}", new() { ["text"] = "Darf ich nicht." });
        Assert.Equal(HttpStatusCode.NotFound, antwort.StatusCode);

        foreach (var pfad in new[] { "/Auftraege", "/Auftraege/1", "/Gruppen", "/Kunden", "/Kunden/Treffer?q=test", "/Verwaltung/Benutzer" })
            Assert.Contains("Kein Zugriff", await dieter.GetStringAsync(pfad));
    }

    [Fact]
    public async Task Ein_Kunde_stellt_einen_Auftrag_und_er_liegt_bei_der_Hotline()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var standort = await app.DatenbankAsync(db => db.Standorte.Where(s => s.Name == "Haupthaus").Select(s => s.Id).FirstAsync());

        var id = await StellenAsync(dieter, "Beamer zeigt kein Bild", new() { ["Eingabe.StandortId"] = standort.ToString(), ["Eingabe.Geraet"] = "Beamer Raum 1.01" });

        var auftrag = await LadenAsync(id);
        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.True(auftrag.Gruppe.IstEingangsgruppe);
        Assert.Equal(Eingang.Web, auftrag.Eingang);
        Assert.Equal("Dieter Kunde", auftrag.Kunde.Name);
        Assert.Equal("Beamer Raum 1.01", auftrag.Geraet);
        Assert.Equal(standort, auftrag.StandortId);

        var anna = await app.AnmeldenAlsAsync("anna");
        Assert.Contains("Beamer zeigt kein Bild", await anna.GetStringAsync("/Gruppen"));
    }

    [Fact]
    public async Task Ohne_Betreff_wird_kein_Auftrag_gestellt()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var vorher = await app.DatenbankAsync(db => db.Auftraege.CountAsync());

        var antwort = await dieter.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new() { ["Eingabe.Beschreibung"] = "Nur Text." });

        Assert.Contains("field-validation-error", await antwort.Content.ReadAsStringAsync());
        Assert.Equal(vorher, await app.DatenbankAsync(db => db.Auftraege.CountAsync()));
    }

    [Fact]
    public async Task Der_Kunde_sieht_Nachrichten_aber_nichts_Internes()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await StellenAsync(dieter, "Laptop startet nicht");
        var seite = $"/Auftraege/{id}";
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Netzwerk").Select(g => g.Id).FirstAsync());

        await AktionAsync(anna, seite, "Uebernehmen");
        await AktionAsync(anna, seite, "InternerKommentar", new() { ["text"] = "GEHEIM: Kennwort liegt im Tresor." });
        await AktionAsync(anna, seite, "NachrichtAnKunden", new() { ["text"] = "Bitte das Netzteil prüfen." });
        await AktionAsync(anna, seite, "Weiterleiten", new() { ["gruppeId"] = netzwerk.ToString(), ["text"] = "HINWEIS-NUR-FUER-IT" });
        await anna.PostFormularAsync($"{seite}/Bearbeiten", $"{seite}/Bearbeiten", new()
        {
            ["Version"] = (await LadenAsync(id)).Version.ToString(),
            ["Felder.Betreff"] = "Laptop startet nicht",
            ["Felder.Beschreibung"] = "Für einen Test.",
            ["Felder.Dringlichkeit"] = "Hoch",
        });

        var sicht = await dieter.GetStringAsync($"/Meine/{id}");

        Assert.Contains("Bitte das Netzteil prüfen.", sicht);
        Assert.Contains("Nachricht der IT", sicht);
        Assert.DoesNotContain("GEHEIM", sicht);
        Assert.DoesNotContain("HINWEIS-NUR-FUER-IT", sicht);
        Assert.DoesNotContain("Netzwerk", sicht);
        Assert.DoesNotContain("Dringlichkeit", sicht);
        Assert.DoesNotContain("Interner Kommentar", sicht);
    }

    [Fact]
    public async Task Der_Kunde_antwortet_auf_einen_laufenden_Auftrag()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await StellenAsync(dieter, "Tastatur klemmt");

        var antwort = await dieter.PostFormularAsync($"/Meine/{id}", $"/Meine/{id}", new() { ["text"] = "Die Leertaste ist betroffen." });

        Assert.Contains("Die Nachricht wurde an die IT gesendet.", await antwort.Content.ReadAsStringAsync());
        Assert.Contains("Die Leertaste ist betroffen.", await anna.GetStringAsync($"/Auftraege/{id}"));
        var auftrag = await LadenAsync(id);
        Assert.Equal(VerlaufArt.AntwortDesKunden, auftrag.Verlauf.OrderBy(v => v.Id).Last().Art);
        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
    }

    [Fact]
    public async Task Der_Kunde_oeffnet_einen_abgeschlossenen_Auftrag_wieder()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await StellenAsync(dieter, "Maus springt");
        await AktionAsync(anna, $"/Auftraege/{id}", "Uebernehmen");
        await AktionAsync(anna, $"/Auftraege/{id}", "Abschliessen", new() { ["text"] = "Maus getauscht." });
        Assert.Contains("Auftrag wieder öffnen", await dieter.GetStringAsync($"/Meine/{id}"));

        // Ohne Begründung bleibt er abgeschlossen.
        var leer = await dieter.PostFormularAsync($"/Meine/{id}", $"/Meine/{id}", new() { ["text"] = " " });
        Assert.Contains("Bitte einen Text eingeben oder eine Datei anhängen.", await leer.Content.ReadAsStringAsync());
        Assert.Equal(AuftragStatus.Abgeschlossen, (await LadenAsync(id)).Status);

        var antwort = await dieter.PostFormularAsync($"/Meine/{id}", $"/Meine/{id}", new() { ["text"] = "Die neue Maus springt auch." });

        var sicht = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Der Auftrag ist wieder geöffnet und liegt bei der IT.", sicht);
        Assert.Contains("Die neue Maus springt auch.", sicht);
        var auftrag = await app.DatenbankAsync(db => db.Auftraege.AsNoTracking()
            .Include(a => a.Gruppe).Include(a => a.BevorzugterBearbeiter).Include(a => a.Verlauf)
            .FirstAsync(a => a.Id == id));
        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.True(auftrag.Gruppe.IstEingangsgruppe);
        Assert.Null(auftrag.BearbeiterId);
        // Wer ihn zuletzt bearbeitet hat, ist als bevorzugter Bearbeiter genannt.
        Assert.Equal("Anna Beispiel", auftrag.BevorzugterBearbeiter!.Name);
        var eintrag = auftrag.Verlauf.OrderBy(v => v.Id).Last();
        Assert.Equal(VerlaufArt.VomKundenWiederGeoeffnet, eintrag.Art);
        Assert.Equal("Vom Kunden wieder geöffnet in Gruppe IT-Hotline, bevorzugt an Anna Beispiel", eintrag.Beschreibung);

        // Die IT sieht ihn wieder in der Gruppe und Anna auf ihrer Startseite.
        Assert.Contains("Maus springt", await anna.GetStringAsync("/Gruppen"));
        Assert.Contains("Für mich vorgemerkt", await anna.GetStringAsync("/"));
        Assert.Contains("Die neue Maus springt auch.", await anna.GetStringAsync($"/Auftraege/{id}"));
    }

    [Fact]
    public async Task Auch_Bearbeiter_stellen_eigene_Auftraege_und_sehen_dort_nur_ihre()
    {
        var bernd = await app.AnmeldenAlsAsync("bernd");

        var id = await StellenAsync(bernd, "Mein eigener Monitor flackert");

        var liste = await bernd.GetStringAsync("/Meine");
        Assert.Contains("Mein eigener Monitor flackert", liste);
        Assert.DoesNotContain("Drucker im Flur druckt nicht", liste);
        Assert.Equal("Bernd Muster", (await LadenAsync(id)).Kunde.Name);
        Assert.Equal(1, await app.DatenbankAsync(db => db.Kunden.CountAsync(k => k.Anmeldename == "bernd")));
    }

    [Fact]
    public async Task Ein_neues_Konto_bekommt_Name_und_Kontaktdaten_aus_dem_Verzeichnis()
    {
        app.Verzeichnis.Eintraege[@"amt\nneu"] = new("Nina Neu", "Nina.Neu@Example.org", "0221 9-87", "Bauamt");

        var nina = await app.AnmeldenAlsNeuAsync(@"AMT\nneu");
        Assert.Contains("Nina Neu", await nina.GetStringAsync("/Meine"));

        var benutzer = await app.DatenbankAsync(db => db.Benutzer.FirstAsync(b => b.Anmeldename == @"amt\nneu"));
        Assert.Equal(("Nina Neu", "nina.neu@example.org", false), (benutzer.Name, benutzer.Email, benutzer.IstBearbeiter));
        var kunde = await app.DatenbankAsync(db => db.Kunden.SingleAsync(k => k.Anmeldename == @"amt\nneu"));
        Assert.Equal(("Nina Neu", "nina.neu@example.org", "0221 9-87", "Bauamt"), (kunde.Name, kunde.Email, kunde.Telefon, kunde.Organisationseinheit));

        // Die Hotline findet sie danach über die Kundensuche.
        var anna = await app.AnmeldenAlsAsync("anna");
        Assert.Contains("Nina Neu", await anna.GetStringAsync("/Kunden?q=bauamt"));
    }

    [Fact]
    public async Task Ohne_Verzeichniseintrag_heisst_das_Konto_wie_der_Anmeldename()
    {
        var unbekannt = await app.AnmeldenAlsNeuAsync(@"amt\xy123");

        Assert.Contains("xy123", await unbekannt.GetStringAsync("/Meine"));
        var kunde = await app.DatenbankAsync(db => db.Kunden.SingleAsync(k => k.Anmeldename == @"amt\xy123"));
        Assert.Equal(("xy123", null), (kunde.Name, kunde.Email));
    }

    [Fact]
    public async Task Ein_von_der_Hotline_angelegter_Kunde_wird_ueber_die_Adresse_aus_dem_Verzeichnis_wiedererkannt()
    {
        app.Verzeichnis.Eintraege["eva"] = new("Eva Test", "eva.test@example.org", null, null);
        var kunden = await app.DatenbankAsync(db => db.Kunden.CountAsync());

        var eva = await app.AnmeldenAlsNeuAsync("eva");

        Assert.Contains("WLAN im Besprechungsraum", await eva.GetStringAsync("/Meine"));
        Assert.Equal(kunden, await app.DatenbankAsync(db => db.Kunden.CountAsync()));
        Assert.Equal("eva", await app.DatenbankAsync(db => db.Kunden.Where(k => k.Email == "eva.test@example.org").Select(k => k.Anmeldename).SingleAsync()));
    }

    [Fact]
    public async Task Die_Adresse_eines_Kunden_mit_anderem_Konto_verschafft_keinen_Zugang_zu_dessen_Auftraegen()
    {
        // Dieter Kunde hat bereits das Konto "dieter".
        app.Verzeichnis.Eintraege["doppelgaenger"] = new("Dieter Doppelt", "dieter.kunde@example.org", null, null);

        var anderer = await app.AnmeldenAlsNeuAsync("doppelgaenger");

        Assert.DoesNotContain("Drucker im Flur", await anderer.GetStringAsync("/Meine"));
        var kunde = await app.DatenbankAsync(db => db.Kunden.SingleAsync(k => k.Anmeldename == "doppelgaenger"));
        Assert.Null(kunde.Email);
        Assert.Equal("dieter", await app.DatenbankAsync(db => db.Kunden.Where(k => k.Email == "dieter.kunde@example.org").Select(k => k.Anmeldename).SingleAsync()));
    }

    [Fact]
    public async Task Ein_deaktiviertes_Konto_kommt_auch_nicht_auf_die_Kundenseiten()
    {
        await app.AnmeldenAlsNeuAsync("gesperrt");
        await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "gesperrt").ExecuteUpdateAsync(b => b.SetProperty(x => x.Aktiv, false)));

        var gesperrt = await app.AnmeldenAlsNeuAsync("gesperrt");

        Assert.Contains("Kein Zugriff", await gesperrt.GetStringAsync("/Meine"));
        Assert.Contains("Kein Zugriff", await gesperrt.GetStringAsync("/Meine/Neu"));
    }

    private static async Task<int> StellenAsync(HttpClient client, string betreff, Dictionary<string, string>? weitere = null)
    {
        var felder = new Dictionary<string, string>(weitere ?? [])
        {
            ["Eingabe.Betreff"] = betreff,
            ["Eingabe.Beschreibung"] = "Für einen Test.",
        };
        var antwort = await client.PostFormularAsync("/Meine/Neu", "/Meine/Neu", felder);
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private async Task<HttpResponseMessage> AktionAsync(HttpClient client, string seite, string aktion, Dictionary<string, string>? felder = null)
    {
        felder ??= [];
        felder["version"] = (await LadenAsync(int.Parse(seite.Split('/')[^1]))).Version.ToString();
        return await client.PostFormularAsync(seite, $"{seite}?handler={aktion}", felder);
    }

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Kunde)
        .Include(a => a.Gruppe)
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
