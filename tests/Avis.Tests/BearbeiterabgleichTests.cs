using System.Net;
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
/// Der Abgleich der Bearbeiter mit einer externen Quelle. Anstelle des Active
/// Directory liefert eine Liste im Test die Bearbeiter. Aus den Testdaten
/// kommen Anna (Administratorin), Bernd und Clara (von Hand Bearbeiter),
/// Dieter (ohne Rolle) und Finn, der schon aus dem Verzeichnis stammt.
/// Jeder Test hat seine eigene Datenbank.
/// </summary>
public class BearbeiterabgleichTests
{
    private const string FinnsKennung = "7c1f0a52-3b7e-4d1a-9c55-0d2a6f1e4c01";

    private static Quellbearbeiter Finn => new(FinnsKennung, @"AVIS\Finn", "Verzeichnis, Finn", "Finn.Verzeichnis@example.org");
    private static Quellbearbeiter Nora => new("guid-nora", @"avis\nora", "Neu, Nora", "nora.neu@example.org");

    [Fact]
    public async Task Wen_die_Quelle_nennt_der_ist_Bearbeiter_und_fehlende_Konten_werden_angelegt()
    {
        using var app = new AvisAnwendung();
        var vorher = await app.DatenbankAsync(db => db.Benutzer.CountAsync());
        var dieter = await KontoAsync(app, "dieter");
        Assert.False(dieter.IstBearbeiter);
        // Finn heißt in der Quelle anders. Dieter hat sich schon einmal angemeldet und steht ohne Rolle in AVIS.
        app.Bearbeiterquelle.Bearbeiter.AddRange([Finn, Nora, new("guid-dieter", "DIETER", "Kunde, Dieter", null)]);

        var ergebnis = await AbgleichenAsync(app);

        Assert.Null(ergebnis.Fehler);
        Assert.Equal((3, 1, 2, 0, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        Assert.Equal(vorher + 1, await app.DatenbankAsync(db => db.Benutzer.CountAsync()));

        // Neu: ein Konto mit Anmeldename in Kleinbuchstaben, Bearbeiter, noch in keiner Gruppe.
        var nora = await KontoAsync(app, @"avis\nora");
        Assert.Equal(("Neu, Nora", "nora.neu@example.org", true, false, true, "ActiveDirectory", "guid-nora"),
            (nora.Name, nora.Email, nora.IstBearbeiter, nora.IstAdministrator, nora.Aktiv, nora.Quelle, nora.ExterneKennung));

        // Wiedererkannt über die Kennung: derselbe Eintrag, Name und Adresse aus der Quelle.
        var finn = await KontoAsync(app, @"avis\finn");
        Assert.Equal(("Verzeichnis, Finn", "finn.verzeichnis@example.org", true), (finn.Name, finn.Email, finn.IstBearbeiter));

        // Wiedererkannt über den Anmeldenamen: Dieters Konto bleibt dasselbe und ist jetzt Bearbeiter.
        var d = await KontoAsync(app, "dieter");
        Assert.Equal((dieter.Id, "Kunde, Dieter", null, true, "ActiveDirectory", "guid-dieter"), (d.Id, d.Name, d.Email, d.IstBearbeiter, d.Quelle, d.ExterneKennung));

        // Wer nur in AVIS zum Bearbeiter gemacht wurde, bleibt unberührt.
        var bernd = await KontoAsync(app, "bernd");
        Assert.Equal((true, null, "Bernd Muster"), (bernd.IstBearbeiter, bernd.Quelle, bernd.Name));

        // Die Neue steht sofort zur Wahl, zum Beispiel als bevorzugte Bearbeiterin.
        var anna = await app.AnmeldenAlsAsync("anna");
        Assert.Contains("Neu, Nora", await anna.GetStringAsync("/Auftraege/Neu"));

        // Ein zweiter Lauf ohne Änderung in der Quelle ändert nichts.
        ergebnis = await AbgleichenAsync(app);
        Assert.Equal((3, 0, 0, 0, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
    }

    [Fact]
    public async Task Wen_die_Quelle_nicht_mehr_nennt_der_verliert_die_Rolle_und_seine_Auftraege_gehen_zurueck()
    {
        using var app = new AvisAnwendung();
        app.Bearbeiterquelle.Bearbeiter.AddRange([Finn, Nora]);
        await AbgleichenAsync(app);

        // Finn tritt einer Gruppe bei und übernimmt einen Auftrag.
        var anna = await app.AnmeldenAlsAsync("anna");
        var finn = await app.AnmeldenAlsAsync(@"avis\finn");
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Netzwerk").Select(g => g.Id).SingleAsync());
        await finn.PostFormularAsync("/Gruppen/Meine", $"/Gruppen/Meine?handler=Beitreten&id={netzwerk}", []);
        var erfasst = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = "Switch im Keller ausgefallen",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = netzwerk.ToString(),
        });
        var id = int.Parse(erfasst.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        await finn.UebernehmenAsync(app, id);
        var konto = await KontoAsync(app, @"avis\finn");
        Assert.Equal(konto.Id, await app.DatenbankAsync(db => db.Auftraege.Where(a => a.Id == id).Select(a => a.BearbeiterId).SingleAsync()));

        // Die Quelle nennt Finn nicht mehr.
        app.Bearbeiterquelle.Bearbeiter.Clear();
        app.Bearbeiterquelle.Bearbeiter.Add(Nora);
        var ergebnis = await AbgleichenAsync(app);

        Assert.Equal((1, 0, 0, 0, 1), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        konto = await app.DatenbankAsync(db => db.Benutzer.AsNoTracking().Include(b => b.Gruppen).SingleAsync(b => b.Id == konto.Id));
        Assert.Equal((false, true, 0), (konto.IstBearbeiter, konto.Aktiv, konto.Gruppen.Count));

        // Der Auftrag liegt wieder offen in seiner Gruppe, und im Verlauf steht, warum.
        var auftrag = await app.DatenbankAsync(db => db.Auftraege.AsNoTracking().Include(a => a.Verlauf).ThenInclude(v => v.Benutzer).SingleAsync(a => a.Id == id));
        Assert.Equal((AuftragStatus.Offen, netzwerk), (auftrag.Status, auftrag.GruppeId));
        var zurueck = auftrag.Verlauf.OrderBy(v => v.Id).Last();
        Assert.Equal(("Zurück an Gruppe Netzwerk", "Verzeichnis, Finn ist kein Bearbeiter mehr.", "Bearbeiterabgleich"), (zurueck.Beschreibung, zurueck.Text, zurueck.Benutzer.Name));

        // Das Konto bleibt: Finn kommt weiter an seine eigenen Aufträge als Kunde, aber nicht mehr an die der anderen.
        finn = await app.AnmeldenAlsAsync(@"avis\finn");
        Assert.Equal(HttpStatusCode.OK, (await finn.GetAsync("/Meine")).StatusCode);
        Assert.Contains("Kein Zugriff", await finn.GetStringAsync("/Auftraege"));
        // Das Konto, in dessen Namen der Abgleich handelt, steht nicht in der Benutzerliste.
        Assert.DoesNotContain(Bearbeiterabgleich.Systemkonto, await anna.GetStringAsync("/Verwaltung/Benutzer"));

        // Nennt die Quelle ihn wieder, ist er wieder Bearbeiter, mit demselben Konto.
        app.Bearbeiterquelle.Bearbeiter.Add(Finn);
        ergebnis = await AbgleichenAsync(app);
        Assert.Equal((2, 0, 0, 1, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        Assert.Equal((konto.Id, true), ((await KontoAsync(app, @"avis\finn")).Id, (await KontoAsync(app, @"avis\finn")).IstBearbeiter));
    }

    [Fact]
    public async Task Wer_zugleich_Kunde_ist_hat_ein_Konto_und_einen_Kundeneintrag_die_zusammengehoeren()
    {
        using var app = new AvisAnwendung();
        // Dieselbe Person steht in beiden Quellen, mit demselben Anmeldenamen in anderer Schreibweise.
        app.Bearbeiterquelle.Bearbeiter.Add(Nora);
        app.Kundenquelle.Kunden.Add(new("guid-nora", @"AVIS\Nora", "Neu, Nora", "Nora.Neu@example.org", "4711", "IT", "A 301"));
        await AbgleichenAsync(app);
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Kundenabgleich>().AusfuehrenAsync();
        var konto = await KontoAsync(app, @"avis\nora");

        // Als Kundin stellt sie einen eigenen Auftrag. Er hängt am Eintrag aus dem Kundenabgleich; ein zweiter entsteht nicht.
        var nora = await app.AnmeldenAlsAsync(@"avis\nora");
        var gestellt = await nora.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new() { ["Eingabe.Betreff"] = "Mein Headset knackt", ["Eingabe.Beschreibung"] = "Für einen Test." });
        var id = int.Parse(gestellt.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        var kunde = await app.DatenbankAsync(db => db.Auftraege.Where(a => a.Id == id).Select(a => a.Kunde).SingleAsync());
        Assert.Equal(("ActiveDirectory", @"avis\nora", "A 301"), (kunde.Quelle, kunde.Anmeldename, kunde.Standort));
        Assert.Equal(1, await app.DatenbankAsync(db => db.Kunden.CountAsync(k => k.Name == "Neu, Nora")));

        // Als Bearbeiterin sieht sie ihn unter allen Aufträgen und kann ihn wie jeden anderen übernehmen.
        Assert.Contains("Mein Headset knackt", await nora.GetStringAsync("/Auftraege"));
        await nora.UebernehmenAsync(app, id);
        Assert.Equal(konto.Id, await app.DatenbankAsync(db => db.Auftraege.Where(a => a.Id == id).Select(a => a.BearbeiterId).SingleAsync()));

        // Verliert sie die Rolle, bleibt sie Kundin: Der Auftrag geht zurück an die Gruppe und bleibt ihr eigener.
        app.Bearbeiterquelle.Bearbeiter.Clear();
        app.Bearbeiterquelle.Bearbeiter.Add(Finn);
        Assert.Equal(1, (await AbgleichenAsync(app)).Inaktiv);
        nora = await app.AnmeldenAlsAsync(@"avis\nora");
        Assert.Contains("Mein Headset knackt", await nora.GetStringAsync("/Meine"));
        Assert.Contains("Kein Zugriff", await nora.GetStringAsync("/Auftraege"));
        Assert.True(await app.DatenbankAsync(db => db.Kunden.Where(k => k.Id == kunde.Id).Select(k => k.Aktiv).SingleAsync()));
    }

    [Fact]
    public async Task Administratoren_bleiben_Bearbeiter_auch_wenn_die_Quelle_sie_nicht_nennt()
    {
        using var app = new AvisAnwendung();
        await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == @"avis\finn").ExecuteUpdateAsync(b => b.SetProperty(x => x.IstAdministrator, true)));
        app.Bearbeiterquelle.Bearbeiter.Add(Nora);

        var ergebnis = await AbgleichenAsync(app);

        Assert.Equal(0, ergebnis.Inaktiv);
        Assert.Contains("ist aber Administrator und bleibt deshalb Bearbeiter", Assert.Single(ergebnis.Hinweise!));
        var finn = await KontoAsync(app, @"avis\finn");
        Assert.True(finn is { IstBearbeiter: true, IstAdministrator: true });
        // Anna ist Administratorin und stammt nicht aus der Quelle: ebenfalls unberührt.
        Assert.True((await KontoAsync(app, "anna")).IstBearbeiter);
    }

    [Fact]
    public async Task Liefert_die_Quelle_nichts_oder_laesst_sie_sich_nicht_lesen_aendert_sich_nichts()
    {
        using var app = new AvisAnwendung();

        // Eine leere Antwort nähme sonst allen aus der Quelle auf einmal die Rolle. Konten ohne Anmeldenamen zählen nicht.
        app.Bearbeiterquelle.Bearbeiter.Add(new("guid-ohne", "  ", "Ohne Anmeldename", null));
        var leer = await AbgleichenAsync(app);
        Assert.Contains("hat keinen einzigen Bearbeiter geliefert", leer.Fehler);

        app.Bearbeiterquelle.Gestoert = true;
        var gestoert = await AbgleichenAsync(app);
        Assert.Contains("konnte nicht gelesen werden: Der Server ist nicht funktionstüchtig.", gestoert.Fehler);

        Assert.True((await KontoAsync(app, @"avis\finn")).IstBearbeiter);
    }

    [Fact]
    public async Task Ein_Anmeldename_der_schon_einem_anderen_Konto_gehoert_wird_nicht_uebernommen()
    {
        using var app = new AvisAnwendung();
        // Finn wurde im Verzeichnis umbenannt, aber auf einen Namen, den in AVIS schon Clara trägt. Ein Konto hat gar keinen.
        app.Bearbeiterquelle.Bearbeiter.AddRange([Finn with { Anmeldename = "clara" }, Nora, new("guid-ohne", null, "Ohne Anmeldename", null)]);

        var ergebnis = await AbgleichenAsync(app);

        Assert.Null(ergebnis.Fehler);
        Assert.Equal(2, ergebnis.Hinweise!.Count);
        Assert.Contains(ergebnis.Hinweise, h => h.Contains("Der Anmeldename clara gehört schon zum Konto Clara Probe."));
        Assert.Contains(ergebnis.Hinweise, h => h.Contains("Ohne Anmeldename: Das Konto hat keinen brauchbaren Anmeldenamen"));
        // Finn bleibt, wie er war, und verliert die Rolle nicht; Clara auch.
        var finn = await KontoAsync(app, @"avis\finn");
        Assert.Equal(("Finn Verzeichnis", true), (finn.Name, finn.IstBearbeiter));
        Assert.Equal(("Clara Probe", null), ((await KontoAsync(app, "clara")).Name, (await KontoAsync(app, "clara")).Quelle));
    }

    [Fact]
    public async Task Ein_Konto_aus_der_Quelle_laesst_sich_in_AVIS_nur_zum_Administrator_machen()
    {
        using var app = new AvisAnwendung();
        var anna = await app.AnmeldenAlsAsync("anna");
        var finn = await KontoAsync(app, @"avis\finn");
        var seite = $"/Verwaltung/Benutzer/{finn.Id}";

        // Name und E-Mail sind gesperrt, und die Rolle "keine" steht nicht zur Wahl.
        var formular = await anna.GetStringAsync(seite);
        Assert.Contains("Stammt aus ActiveDirectory.", formular);
        Assert.Matches("id=\"name\"[^>]*disabled", formular);
        Assert.Contains("Bearbeiter (laut ActiveDirectory)", formular);
        Assert.DoesNotContain(">keine</option>", formular);

        // Wer es trotzdem schickt, ändert nichts: Die Quelle entscheidet, ob jemand Bearbeiter ist und wie er heißt.
        await anna.PostFormularAsync(seite, seite, new() { ["name"] = "Von Hand umbenannt", ["email"] = "anders@example.org", ["rolle"] = "Keine", ["aktiv"] = "true" });
        finn = await KontoAsync(app, @"avis\finn");
        Assert.Equal(("Finn Verzeichnis", "finn.verzeichnis@example.org", true, false), (finn.Name, finn.Email, finn.IstBearbeiter, finn.IstAdministrator));

        // Zum Administrator machen und wieder zurück geht.
        await anna.PostFormularAsync(seite, seite, new() { ["rolle"] = "Administrator", ["aktiv"] = "true" });
        Assert.True((await KontoAsync(app, @"avis\finn")) is { IstBearbeiter: true, IstAdministrator: true });
        await anna.PostFormularAsync(seite, seite, new() { ["rolle"] = "Bearbeiter", ["aktiv"] = "true" });
        Assert.True((await KontoAsync(app, @"avis\finn")) is { IstBearbeiter: true, IstAdministrator: false });

        // Ein Konto, das nur in AVIS steht, lässt sich wie bisher ganz bearbeiten.
        var clara = await KontoAsync(app, "clara");
        Assert.Contains(">keine</option>", await anna.GetStringAsync($"/Verwaltung/Benutzer/{clara.Id}"));
    }

    [Fact]
    public async Task Die_Verwaltung_zeigt_den_letzten_Abgleich_stoesst_einen_an_und_ohne_Quelle_ist_er_aus()
    {
        using var app = new AvisAnwendung();
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        app.Bearbeiterquelle.Bearbeiter.AddRange([Finn, Nora]);

        Assert.Contains("Es wurde noch nicht abgeglichen.", await anna.GetStringAsync("/Verwaltung/Bearbeiterabgleich"));
        Assert.Contains("Kein Zugriff", await bernd.GetStringAsync("/Verwaltung/Bearbeiterabgleich"));

        var seite = await (await anna.PostFormularAsync("/Verwaltung/Bearbeiterabgleich", "/Verwaltung/Bearbeiterabgleich", [])).Content.ReadAsStringAsync();
        Assert.Contains("Die Bearbeiter wurden abgeglichen.", seite);
        Assert.Matches("<dt>Neu angelegt</dt>\\s*<dd>1</dd>", seite);
        Assert.Matches("<dt>Nicht mehr Bearbeiter</dt>\\s*<dd>0</dd>", seite);
        Assert.Contains("täglich um 01:30 Uhr", seite);

        app.Bearbeiterquelle.Gestoert = true;
        seite = await (await anna.PostFormularAsync("/Verwaltung/Bearbeiterabgleich", "/Verwaltung/Bearbeiterabgleich", [])).Content.ReadAsStringAsync();
        Assert.Contains("konnte nicht gelesen werden", seite);

        using var basis = new AvisAnwendung();
        using var ohne = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.RemoveAll<IBearbeiterquelle>()));
        var client = ohne.CreateClient();
        await client.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = "anna" });
        Assert.Contains("Der Abgleich ist ausgeschaltet", await client.GetStringAsync("/Verwaltung/Bearbeiterabgleich"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Bearbeiterabgleich", [])).StatusCode);
    }

    [Fact]
    public void Eine_halbe_Einstellung_laesst_AVIS_nicht_starten()
    {
        // Ohne Filter wäre jedes Konto der Domäne ein Bearbeiter.
        using var halb = new AvisAnwendung();
        using var ohneFilter = halb.WithWebHostBuilder(b => b.UseSetting("Bearbeiterabgleich:Art", "ActiveDirectory"));
        Assert.Contains("muss Bearbeiterabgleich:Filter gesetzt sein", Assert.ThrowsAny<Exception>(() => ohneFilter.CreateClient()).ToString());

        using var schief = new AvisAnwendung();
        using var ohneKlammern = schief.WithWebHostBuilder(b => b
            .UseSetting("Bearbeiterabgleich:Art", "ActiveDirectory")
            .UseSetting("Bearbeiterabgleich:Filter", "memberOf=CN=AVIS-Bearbeiter,DC=example,DC=org"));
        Assert.Contains("Bearbeiterabgleich:Filter muss ein LDAP-Filter in Klammern sein", Assert.ThrowsAny<Exception>(() => ohneKlammern.CreateClient()).ToString());
    }

    [Fact]
    public async Task Die_Testdaten_enthalten_einen_Bearbeiter_aus_dem_Verzeichnis()
    {
        using var app = new AvisAnwendung();

        var finn = await KontoAsync(app, @"avis\finn");

        Assert.Equal(("Finn Verzeichnis", "ActiveDirectory", FinnsKennung, true, false, true),
            (finn.Name, finn.Quelle, finn.ExterneKennung, finn.IstBearbeiter, finn.IstAdministrator, finn.Aktiv));
    }

    private static async Task<Abgleichergebnis> AbgleichenAsync(AvisAnwendung app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Bearbeiterabgleich>().AusfuehrenAsync();
    }

    private static Task<Benutzer> KontoAsync(AvisAnwendung app, string anmeldename) =>
        app.DatenbankAsync(db => db.Benutzer.AsNoTracking().SingleAsync(b => b.Anmeldename == anmeldename));
}
