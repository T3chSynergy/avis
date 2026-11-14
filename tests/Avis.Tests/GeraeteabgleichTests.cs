using System.Net;
using System.Text;
using Avis.Web.Abgleich;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Avis.Tests;

/// <summary>
/// Der Abgleich der Geräte mit einer externen Quelle. Anstelle von Snipe-IT
/// liefert eine Liste im Test die Geräte. Aus den Testdaten gibt es von Hand
/// angelegte Geräte wie DR-0042 und NB-1017. Jeder Test hat seine eigene
/// Datenbank.
/// </summary>
public class GeraeteabgleichTests
{
    private static Quellgeraet Lesegeraet => new("101", "SC-7001", "Dokumentenscanner", "Scanner", "A 105", "Dieter.Kunde@example.org");
    private static Quellgeraet Plotter => new("102", "PL-7002", "Plotter A0", "Plotter", "B 0.12", null);

    [Fact]
    public async Task Der_Abgleich_legt_Geraete_an_uebernimmt_vorhandene_und_die_Quelle_gewinnt()
    {
        using var app = await OhneQuellgeraeteAsync();
        var vorher = await app.DatenbankAsync(db => db.Geraete.CountAsync());
        var drucker = await GeraetAsync(app, "DR-0042");
        var dieter = await app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == "Dieter Kunde").Select(k => k.Id).SingleAsync());
        // Den Flurdrucker gibt es schon von Hand; in der Quelle heißt er anders und ist an eine unbekannte Adresse ausgegeben.
        app.Geraetequelle.Geraete.AddRange([Lesegeraet, Plotter, new("103", "dr-0042", "Farblaser 2. OG", "Drucker", "A 2.01", "niemand@example.org")]);

        var ergebnis = await AbgleichenAsync(app);

        Assert.Null(ergebnis.Fehler);
        Assert.Equal((3, 2, 1, 0, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        Assert.Equal(vorher + 2, await app.DatenbankAsync(db => db.Geraete.CountAsync()));

        // Neu: mit Standort, und der Nutzer ist der Kunde mit dieser Adresse.
        var scanner = await GeraetAsync(app, "SC-7001");
        Assert.Equal(("SnipeIt", "101", "Dokumentenscanner", "Scanner", "A 105", dieter, true),
            (scanner.Quelle, scanner.ExterneKennung, scanner.Bezeichnung, scanner.Typ, scanner.Standort, scanner.KundeId!.Value, scanner.Aktiv));
        Assert.Null((await GeraetAsync(app, "PL-7002")).KundeId);

        // Übernommen: derselbe Eintrag, die Angaben aus der Quelle, auch der Standort.
        var uebernommen = await app.DatenbankAsync(db => db.Geraete.AsNoTracking().SingleAsync(g => g.Id == drucker.Id));
        Assert.Equal(("SnipeIt", "103", "dr-0042", "Farblaser 2. OG", "A 2.01", null),
            (uebernommen.Quelle, uebernommen.ExterneKennung, uebernommen.Inventarnummer, uebernommen.Bezeichnung, uebernommen.Standort, uebernommen.KundeId));
        Assert.Equal("A Flur 2. OG", drucker.Standort);

        // Was nur in AVIS steht, bleibt unberührt, und die Suche findet auch den Standort.
        Assert.Null((await GeraetAsync(app, "NB-1017")).Quelle);
        Assert.Equal(["SC-7001"], await app.DatenbankAsync(db => db.Geraete.Suchen("a 105").Select(g => g.Inventarnummer).ToListAsync()));

        // Ein zweiter Lauf ohne Änderung in der Quelle ändert nichts.
        ergebnis = await AbgleichenAsync(app);
        Assert.Equal((3, 0, 0, 0, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
    }

    [Fact]
    public async Task Was_in_der_Quelle_fehlt_wird_ausgemustert_und_bleibt_mit_seinen_Auftraegen_erhalten()
    {
        using var app = await OhneQuellgeraeteAsync();
        var anna = await app.AnmeldenAlsAsync("anna");
        app.Geraetequelle.Geraete.AddRange([Lesegeraet, Plotter]);
        await AbgleichenAsync(app);
        var erfasst = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = "Scanner zieht schief ein",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.Felder.Inventarnummer"] = "SC-7001",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync())).ToString(),
        });
        Assert.Contains("Scanner zieht schief ein", await erfasst.Content.ReadAsStringAsync());

        // Der Scanner verschwindet aus der Quelle, der Plotter zieht um und bekommt einen Nutzer.
        app.Geraetequelle.Geraete.Clear();
        app.Geraetequelle.Geraete.Add(Plotter with { Standort = "A 3.07", NutzerEmail = "eva.test@example.org" });
        var ergebnis = await AbgleichenAsync(app);

        Assert.Equal((1, 0, 1, 0, 1), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        var scanner = await GeraetAsync(app, "SC-7001");
        Assert.False(scanner.Aktiv);
        Assert.True(await app.DatenbankAsync(db => db.Auftraege.AnyAsync(a => a.InventargeraetId == scanner.Id)));
        var plotter = await app.DatenbankAsync(db => db.Geraete.AsNoTracking().Include(g => g.Kunde).SingleAsync(g => g.Inventarnummer == "PL-7002"));
        Assert.Equal(("A 3.07", "Eva Test"), (plotter.Standort, plotter.Kunde!.Name));

        // Ausgemustert: markiert in der Liste und nicht mehr neu verknüpfbar.
        Assert.Matches("SC-7001</a>\\s*<span class=\"hinweis\">ausgemustert</span>", await anna.GetStringAsync("/Geraete?q=sc-7001"));
        var verknuepfen = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = "Für ein ausgemustertes Gerät",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.Felder.Inventarnummer"] = "SC-7001",
        });
        Assert.Contains("Im Inventar gibt es kein Gerät mit dieser Nummer.", await verknuepfen.Content.ReadAsStringAsync());

        // Steht der Scanner wieder in der Quelle, ist er wieder in Betrieb, derselbe Eintrag.
        app.Geraetequelle.Geraete.Add(Lesegeraet);
        ergebnis = await AbgleichenAsync(app);
        Assert.Equal((2, 0, 0, 1, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        Assert.Equal((scanner.Id, true), ((await GeraetAsync(app, "SC-7001")).Id, (await GeraetAsync(app, "SC-7001")).Aktiv));
    }

    [Fact]
    public async Task Liefert_die_Quelle_nichts_oder_laesst_sie_sich_nicht_lesen_aendert_sich_nichts()
    {
        using var app = await OhneQuellgeraeteAsync();
        app.Geraetequelle.Geraete.AddRange([Lesegeraet, Plotter]);
        await AbgleichenAsync(app);

        // Eine leere Antwort würde sonst alle Geräte der Quelle auf einmal ausmustern. Geräte ohne Inventarnummer zählen nicht.
        app.Geraetequelle.Geraete.Clear();
        app.Geraetequelle.Geraete.Add(new("104", "  ", "Ohne Nummer", null, null, null));
        var leer = await AbgleichenAsync(app);
        Assert.Contains("kein einziges Gerät mit Inventarnummer geliefert", leer.Fehler);

        app.Geraetequelle.Gestoert = true;
        var gestoert = await AbgleichenAsync(app);
        Assert.Contains("konnte nicht gelesen werden: Snipe-IT hat den Zugangsschlüssel abgelehnt.", gestoert.Fehler);

        Assert.True((await GeraetAsync(app, "SC-7001")).Aktiv);
        Assert.True((await GeraetAsync(app, "PL-7002")).Aktiv);
    }

    [Fact]
    public async Task Doppelte_und_schon_vergebene_Inventarnummern_werden_ausgelassen_und_gemeldet()
    {
        using var app = await OhneQuellgeraeteAsync();
        app.Geraetequelle.Geraete.AddRange([Lesegeraet, Plotter]);
        await AbgleichenAsync(app);

        // Der Plotter bekommt in der Quelle die Nummer des Scanners, und eine Nummer steht dort zweimal.
        app.Geraetequelle.Geraete.Clear();
        app.Geraetequelle.Geraete.AddRange([
            Lesegeraet,
            Plotter with { Inventarnummer = "SC-7001" },
            new("105", "KA-7005", "Kamera", null, null, null),
            new("106", "ka-7005", "Zweite Kamera", null, null, null),
        ]);
        // Die Quelle selbst nennt SC-7001 hier doppelt: Das zweite Gerät, der Plotter, fällt schon beim Lesen heraus.
        var ergebnis = await AbgleichenAsync(app);

        Assert.Null(ergebnis.Fehler);
        Assert.Equal(2, ergebnis.Hinweise!.Count);
        Assert.All(ergebnis.Hinweise, h => Assert.Contains("kommt in der Quelle mehrfach vor", h));
        Assert.Equal("Kamera", (await GeraetAsync(app, "KA-7005")).Bezeichnung);
        // Der Plotter steht damit nicht mehr in der Liste und wird ausgemustert, behält aber seine Nummer.
        Assert.False((await GeraetAsync(app, "PL-7002")).Aktiv);
    }

    [Fact]
    public async Task Bekommt_ein_Geraet_eine_neue_Nummer_ist_die_alte_im_selben_Lauf_wieder_frei()
    {
        using var app = await OhneQuellgeraeteAsync();
        app.Geraetequelle.Geraete.AddRange([Lesegeraet, Plotter]);
        await AbgleichenAsync(app);
        var scanner = await GeraetAsync(app, "SC-7001");

        // Der Scanner wird in der Quelle umnummeriert, und ein neues Gerät bekommt seine bisherige Nummer.
        app.Geraetequelle.Geraete.Clear();
        app.Geraetequelle.Geraete.AddRange([
            Lesegeraet with { Inventarnummer = "SC-7009" },
            Plotter,
            new("107", "SC-7001", "Nachfolger", "Scanner", null, null),
        ]);
        var ergebnis = await AbgleichenAsync(app);

        Assert.Null(ergebnis.Fehler);
        Assert.Empty(ergebnis.Hinweise!);
        Assert.Equal((3, 1, 1, 0, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        Assert.Equal(scanner.Id, (await GeraetAsync(app, "SC-7009")).Id);
        Assert.Equal(("107", "Nachfolger"), ((await GeraetAsync(app, "SC-7001")).ExterneKennung, (await GeraetAsync(app, "SC-7001")).Bezeichnung));
    }

    [Fact]
    public async Task Auch_sehr_viele_Geraete_gleicht_ein_Lauf_zuegig_ab()
    {
        using var app = await OhneQuellgeraeteAsync();
        app.Geraetequelle.Geraete.AddRange(Enumerable.Range(0, 20000).Select(i =>
            new Quellgeraet($"{100000 + i}", $"INV-{i:D6}", $"Gerät {i}", "Notebook", $"A {i % 400}", null)));

        var erster = await AbgleichenAsync(app);
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var zweiter = await AbgleichenAsync(app);
        uhr.Stop();

        Assert.Equal((20000, 20000), (erster.Gelesen, erster.Neu));
        Assert.Equal((20000, 0, 0, 0), (zweiter.Gelesen, zweiter.Neu, zweiter.Geaendert, zweiter.Inaktiv));
        // Mit einer Suche je Gerät durch die ganze Liste dauerte der zweite Lauf über zehn Sekunden. Die Grenze
        // lässt langsamen Rechnern viel Luft und fängt trotzdem den Rückfall dahin.
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(8), $"Der Lauf dauerte {uhr.Elapsed.TotalSeconds:F1} s.");
    }

    [Fact]
    public async Task Geraete_aus_der_Quelle_lassen_sich_in_AVIS_nicht_aendern()
    {
        using var app = await OhneQuellgeraeteAsync();
        var anna = await app.AnmeldenAlsAsync("anna");
        app.Geraetequelle.Geraete.Add(Lesegeraet);
        await AbgleichenAsync(app);
        var scanner = await GeraetAsync(app, "SC-7001");
        var seite = $"/Geraete/Bearbeiten/{scanner.Id}";

        // Die Seite des Geräts nennt die Quelle und bietet kein Bearbeiten an; das Formular führt dorthin zurück.
        var details = await anna.GetStringAsync($"/Geraete/{scanner.Id}");
        Assert.Contains("Wird in SnipeIt gepflegt", details);
        Assert.DoesNotContain(">Bearbeiten</a>", details);
        var formular = await anna.GetStringAsync(seite);
        Assert.Contains("Gerät SC-7001 stammt aus SnipeIt und wird dort gepflegt.", formular);
        Assert.DoesNotContain("Eingabe.Inventarnummer", formular);

        var abgelehnt = await anna.PostFormularAsync($"/Geraete/Bearbeiten/{(await GeraetAsync(app, "NB-1017")).Id}", seite, new()
        {
            ["Eingabe.Inventarnummer"] = "VON-HAND",
            ["Eingabe.Bezeichnung"] = "Von Hand umbenannt",
            ["Eingabe.Standort"] = "Z 999",
            ["Eingabe.Aktiv"] = "false",
            ["kundeId"] = "",
        });
        Assert.Contains("In AVIS lässt es sich nicht ändern.", await abgelehnt.Content.ReadAsStringAsync());

        var danach = await app.DatenbankAsync(db => db.Geraete.AsNoTracking().SingleAsync(g => g.Id == scanner.Id));
        Assert.Equal(("SC-7001", "Dokumentenscanner", "A 105", true, scanner.KundeId),
            (danach.Inventarnummer, danach.Bezeichnung, danach.Standort, danach.Aktiv, danach.KundeId));

        // Ein Gerät, das nur in AVIS steht, bekommt seinen Standort von Hand.
        var notebook = await GeraetAsync(app, "NB-1017");
        var eigene = $"/Geraete/Bearbeiten/{notebook.Id}";
        Assert.Contains(">Bearbeiten</a>", await anna.GetStringAsync($"/Geraete/{notebook.Id}"));
        await anna.PostFormularAsync(eigene, eigene, new()
        {
            ["Eingabe.Inventarnummer"] = "NB-1017",
            ["Eingabe.Bezeichnung"] = "Notebook 14 Zoll",
            ["Eingabe.Standort"] = " C 0.04 ",
            ["Eingabe.Aktiv"] = "true",
            ["kundeId"] = "",
        });
        Assert.Equal("C 0.04", (await GeraetAsync(app, "NB-1017")).Standort);
        Assert.Contains("NB-1017", await anna.GetStringAsync("/Geraete?q=c+0.04"));
    }

    [Fact]
    public async Task Die_Verwaltung_zeigt_den_letzten_Abgleich_stoesst_einen_an_und_ohne_Quelle_ist_er_aus()
    {
        using var app = await OhneQuellgeraeteAsync();
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        app.Geraetequelle.Geraete.AddRange([Lesegeraet, Plotter]);

        Assert.Contains("Seit dem Start von AVIS wurde noch nicht abgeglichen.", await anna.GetStringAsync("/Verwaltung/Geraeteabgleich"));
        Assert.Contains("Kein Zugriff", await bernd.GetStringAsync("/Verwaltung/Geraeteabgleich"));

        var seite = await (await anna.PostFormularAsync("/Verwaltung/Geraeteabgleich", "/Verwaltung/Geraeteabgleich", [])).Content.ReadAsStringAsync();
        Assert.Contains("Die Geräte wurden abgeglichen.", seite);
        Assert.Matches("<dt>Neu angelegt</dt>\\s*<dd>2</dd>", seite);
        Assert.Matches("<dt>Ausgemustert</dt>\\s*<dd>0</dd>", seite);

        app.Geraetequelle.Gestoert = true;
        seite = await (await anna.PostFormularAsync("/Verwaltung/Geraeteabgleich", "/Verwaltung/Geraeteabgleich", [])).Content.ReadAsStringAsync();
        Assert.Contains("Snipe-IT hat den Zugangsschlüssel abgelehnt.", seite);

        using var basis = new AvisAnwendung();
        using var ohne = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.RemoveAll<IGeraetequelle>()));
        var client = ohne.CreateClient();
        await client.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = "anna" });
        Assert.Contains("Der Abgleich ist ausgeschaltet", await client.GetStringAsync("/Verwaltung/Geraeteabgleich"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Geraeteabgleich", [])).StatusCode);
    }

    [Fact]
    public void Eine_halbe_Einstellung_laesst_AVIS_nicht_starten()
    {
        using var ohneAdresse = new AvisAnwendung();
        using var a = ohneAdresse.WithWebHostBuilder(b => b.UseSetting("Geraeteabgleich:Art", "SnipeIt").UseSetting("Geraeteabgleich:Schluessel", "geheim"));
        Assert.Contains("muss Geraeteabgleich:Adresse die Adresse der Installation sein", Assert.ThrowsAny<Exception>(() => a.CreateClient()).ToString());

        using var ohneSchluessel = new AvisAnwendung();
        using var s = ohneSchluessel.WithWebHostBuilder(b => b.UseSetting("Geraeteabgleich:Art", "SnipeIt").UseSetting("Geraeteabgleich:Adresse", "https://inventar.example.org"));
        Assert.Contains("muss Geraeteabgleich:Schluessel gesetzt sein", Assert.ThrowsAny<Exception>(() => s.CreateClient()).ToString());
    }

    [Fact]
    public async Task Die_Testdaten_enthalten_Geraete_aus_dem_Inventar()
    {
        using var app = new AvisAnwendung();

        var geraete = await app.DatenbankAsync(db => db.Geraete.AsNoTracking().Include(g => g.Kunde).Where(g => g.Quelle == "SnipeIt").OrderBy(g => g.ExterneKennung).ToListAsync());

        Assert.Equal(
            [("TB-3001", "A 105", "Hanna Verzeichnis", true), ("BE-3002", "B 001", null, true), ("TB-2990", "A 107", null, false)],
            geraete.Select(g => (g.Inventarnummer, g.Standort!, g.Kunde?.Name, g.Aktiv)));
        // Die von Hand angelegten Geräte behalten ihre Nummern.
        Assert.Equal("DR-0042", await app.DatenbankAsync(db => db.Geraete.Where(g => g.Id == 1).Select(g => g.Inventarnummer).SingleAsync()));
    }

    // ---------- Die Abfrage von Snipe-IT, gegen nachgestellte Antworten in der Form seines Quelltexts

    [Fact]
    public async Task Snipe_IT_wird_seitenweise_gelesen_und_seine_Angaben_werden_uebersetzt()
    {
        var anfragen = new List<HttpRequestMessage>();
        var seiten = new Queue<string>([
            """
            {"total": 3, "rows": [
              {"id": 11, "name": "Notebook M&amp;V", "asset_tag": "NB-11", "model": {"id": 4, "name": "Modell &quot;X&quot;"},
               "category": {"id": 2, "name": "Notebook"}, "location": {"id": 9, "name": "A 105"}, "rtd_location": {"id": 8, "name": "A 001"},
               "status_label": {"id": 2, "name": "Im Einsatz", "status_type": "deployable", "status_meta": "deployed"},
               "assigned_to": {"id": 5, "username": "dieter", "name": "Dieter Kunde", "email": "Dieter.Kunde@example.org", "type": "user"}},
              {"id": 12, "name": "", "asset_tag": "MO-12", "model": {"id": 5, "name": "Monitor 27"},
               "category": {"id": 3, "name": "Monitor"}, "location": null, "rtd_location": {"id": 8, "name": "A 001"},
               "assigned_to": {"id": 9, "name": "A 105", "type": "location"}}
            ], "current_page": 1, "per_page": 500, "total_pages": 2, "prev_page_url": null, "next_page_url": "…"}
            """,
            """
            {"total": 3, "rows": [
              {"id": 13, "name": null, "asset_tag": "", "model": null, "category": null, "location": null, "rtd_location": null, "assigned_to": null}
            ]}
            """,
        ]);
        var quelle = Quelle(anfrage =>
        {
            anfragen.Add(anfrage);
            return Json(seiten.Dequeue());
        });

        var geraete = await quelle.LesenAsync(CancellationToken.None);

        // HTML-Maskierung aufgelöst, der Name des Modells als Ersatz, der Stammplatz als Ersatz, nur ein Benutzer ist ein Nutzer.
        Assert.Equal(
            [
                new Quellgeraet("11", "NB-11", "Notebook M&V", "Notebook", "A 105", "Dieter.Kunde@example.org"),
                new Quellgeraet("12", "MO-12", "Monitor 27", "Monitor", "A 001", null),
                new Quellgeraet("13", "", null, null, null, null),
            ],
            geraete);

        // Zwei Abrufe, der zweite hinter den ersten beiden Assets, mit Schlüssel und der Bitte um JSON.
        Assert.Equal(
            ["https://inventar.example.org/snipe/api/v1/hardware?limit=500&offset=0&sort=id&order=asc", "https://inventar.example.org/snipe/api/v1/hardware?limit=500&offset=2&sort=id&order=asc"],
            anfragen.Select(a => a.RequestUri!.ToString()));
        Assert.All(anfragen, a => Assert.Equal(("Bearer", "geheim", "application/json"), (a.Headers.Authorization!.Scheme, a.Headers.Authorization.Parameter, a.Headers.Accept.Single().MediaType)));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error": "Unauthorized."}""", "Snipe-IT hat den Zugangsschlüssel abgelehnt.")]
    [InlineData(HttpStatusCode.InternalServerError, "", "Snipe-IT antwortet mit dem Fehler 500.")]
    [InlineData(HttpStatusCode.OK, """{"status": "error", "messages": "Insufficient permissions.", "payload": null}""", "Snipe-IT meldet: Insufficient permissions.")]
    [InlineData(HttpStatusCode.OK, "<html><body>Anmeldung</body></html>", "Die Antwort von Snipe-IT ist kein JSON. Stimmt die Adresse?")]
    [InlineData(HttpStatusCode.OK, """{"total": 0}""", "Snipe-IT hat keine Liste von Assets geliefert.")]
    public async Task Fehler_von_Snipe_IT_werden_gemeldet_statt_als_leere_Liste_gelesen(HttpStatusCode status, string antwort, string erwartet)
    {
        var quelle = Quelle(_ => new HttpResponseMessage(status) { Content = new StringContent(antwort, Encoding.UTF8, "application/json") });

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(() => quelle.LesenAsync(CancellationToken.None));

        Assert.Equal(erwartet, fehler.Message);
    }

    private static SnipeItGeraetequelle Quelle(Func<HttpRequestMessage, HttpResponseMessage> antworten)
    {
        var http = new HttpClient(new Gegenstelle(antworten));
        SnipeItGeraetequelle.Einrichten(http, new GeraeteabgleichOptionen { Adresse = " https://inventar.example.org/snipe/ ", Schluessel = " geheim " });
        return new SnipeItGeraetequelle(http);
    }

    private static HttpResponseMessage Json(string inhalt) =>
        new(HttpStatusCode.OK) { Content = new StringContent(inhalt, Encoding.UTF8, "application/json") };

    private sealed class Gegenstelle(Func<HttpRequestMessage, HttpResponseMessage> antworten) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(antworten(request));
    }

    /// <summary>
    /// Eine Anwendung ohne die Geräte aus dem Inventar, die die Testdaten
    /// mitbringen: Diese Tests zählen genau, was ein Abgleich tut.
    /// </summary>
    private static async Task<AvisAnwendung> OhneQuellgeraeteAsync()
    {
        var app = new AvisAnwendung();
        await app.DatenbankAsync(db => db.Geraete.Where(g => g.Quelle != null).ExecuteDeleteAsync());
        return app;
    }

    private static async Task<Abgleichergebnis> AbgleichenAsync(AvisAnwendung app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Geraeteabgleich>().AusfuehrenAsync();
    }

    private static Task<Inventargeraet> GeraetAsync(AvisAnwendung app, string inventarnummer) =>
        app.DatenbankAsync(db => db.Geraete.AsNoTracking().SingleAsync(g => g.Inventarnummer == inventarnummer));
}
