using System.Net;
using Avis.Web.Data;
using Avis.Web.Domain;
using Avis.Web.Abgleich;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Avis.Tests;

/// <summary>
/// Der Abgleich der Kunden mit einer externen Quelle. Anstelle des Active
/// Directory liefert eine Liste im Test die Kunden. Aus den Testdaten gibt
/// es Dieter Kunde (mit Anmeldename), Eva Test, Özlem Übung, Frank Müller
/// und Gabi Müller (ohne). Jeder Test hat seine eigene Datenbank.
/// </summary>
public class KundenabgleichTests
{
    private static Quellkunde Dieter => new("guid-dieter", @"AVIS\Dieter", "Kunde, Dieter", "Dieter.Kunde@example.org", "9001", "Bauhof");
    private static Quellkunde Eva => new("guid-eva", @"avis\eva", "Test, Eva", "eva.test@example.org", "9002", "Kasse");
    private static Quellkunde Hans => new("guid-hans", @"avis\hans", "Neu, Hans", "hans.neu@example.org", null, null);

    [Fact]
    public async Task Der_erste_Abgleich_uebernimmt_vorhandene_Kunden_legt_fehlende_an_und_die_Quelle_gewinnt()
    {
        using var app = await OhneVerzeichniskundenAsync();
        var vorher = await app.DatenbankAsync(db => db.Kunden.CountAsync());
        var dieter = await KundeAsync(app, "Dieter Kunde");
        // In den Testdaten heißt Dieters Konto nur "dieter". Im Verzeichnis steht die Domäne davor.
        await app.DatenbankAsync(db => db.Kunden.Where(k => k.Id == dieter.Id).ExecuteUpdateAsync(k => k.SetProperty(x => x.Anmeldename, @"avis\dieter")));
        var eva = await KundeAsync(app, "Eva Test");
        app.Kundenquelle.Kunden.AddRange([Dieter, Eva, Hans]);

        var ergebnis = await AbgleichenAsync(app);

        Assert.Null(ergebnis.Fehler);
        Assert.Equal((3, 1, 2, 0, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        Assert.Equal(vorher + 1, await app.DatenbankAsync(db => db.Kunden.CountAsync()));

        // Dieter wird über den Anmeldenamen wiedererkannt, Eva über die Adresse: dieselben Einträge, die Angaben aus der Quelle.
        var d = await app.DatenbankAsync(db => db.Kunden.AsNoTracking().SingleAsync(k => k.Id == dieter.Id));
        Assert.Equal(("ActiveDirectory", "guid-dieter", "Kunde, Dieter", "dieter.kunde@example.org", "9001", "Bauhof", @"avis\dieter", true),
            (d.Quelle, d.ExterneKennung, d.Name, d.Email, d.Telefon, d.Organisationseinheit, d.Anmeldename, d.Aktiv));
        // Die Quelle nennt für Dieter keinen Standort: Der von Hand eingetragene gilt nicht mehr.
        Assert.Equal(("A 204", null), (dieter.Standort, d.Standort));
        var e = await app.DatenbankAsync(db => db.Kunden.AsNoTracking().SingleAsync(k => k.Id == eva.Id));
        Assert.Equal(("guid-eva", "Test, Eva", @"avis\eva", "Kasse"), (e.ExterneKennung, e.Name, e.Anmeldename, e.Organisationseinheit));
        var h = await KundeAsync(app, "Neu, Hans");
        Assert.Equal(("ActiveDirectory", "hans.neu@example.org", true), (h.Quelle, h.Email, h.Aktiv));

        // Wer nur in AVIS steht, bleibt unberührt, und die Suche kennt die neuen Angaben.
        var frank = await KundeAsync(app, "Frank Müller");
        Assert.Equal((null, true), (frank.Quelle, frank.Aktiv));
        Assert.Equal(["Kunde, Dieter"], await app.DatenbankAsync(db => db.Kunden.Suchen("bauhof").Select(k => k.Name).ToListAsync()));

        // Ein zweiter Lauf ohne Änderung in der Quelle ändert nichts.
        ergebnis = await AbgleichenAsync(app);
        Assert.Equal((3, 0, 0, 0, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
    }

    [Fact]
    public async Task Wer_in_der_Quelle_fehlt_wird_inaktiv_und_bleibt_mit_seinen_Auftraegen_erhalten()
    {
        using var app = await OhneVerzeichniskundenAsync();
        var anna = await app.AnmeldenAlsAsync("anna");
        app.Kundenquelle.Kunden.AddRange([Eva, Hans]);
        await AbgleichenAsync(app);
        var eva = await KundeAsync(app, "Test, Eva");
        var auftraege = await app.DatenbankAsync(db => db.Auftraege.CountAsync(a => a.KundeId == eva.Id));
        Assert.True(auftraege > 0);

        // Eva verlässt die Gruppe, bei Hans ändert sich die Nummer.
        app.Kundenquelle.Kunden.Clear();
        app.Kundenquelle.Kunden.Add(Hans with { Telefon = "4711" });
        var ergebnis = await AbgleichenAsync(app);

        Assert.Equal((1, 0, 1, 0, 1), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        eva = await KundeAsync(app, "Test, Eva");
        Assert.False(eva.Aktiv);
        Assert.Equal(auftraege, await app.DatenbankAsync(db => db.Auftraege.CountAsync(a => a.KundeId == eva.Id)));
        Assert.Equal("4711", (await KundeAsync(app, "Neu, Hans")).Telefon);

        // Inaktiv: markiert in der Liste und auf der Seite, nicht mehr wählbar und kein neuer Auftrag.
        Assert.Matches("Test, Eva</a>\\s*<span class=\"hinweis\">inaktiv</span>", await anna.GetStringAsync("/Kunden?q=eva"));
        var seite = await anna.GetStringAsync($"/Kunden/{eva.Id}");
        Assert.Contains("Dieser Kunde ist als inaktiv markiert.", seite);
        Assert.DoesNotContain("Auftrag für Test, Eva erfassen", seite);
        Assert.DoesNotContain("Test, Eva", await anna.GetStringAsync("/Kunden/Treffer?q=eva&leer=neu"));
        var erfassen = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["kundeId"] = eva.Id.ToString(),
            ["Eingabe.Felder.Betreff"] = "Für eine Inaktive",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync())).ToString(),
        });
        Assert.Contains("Test, Eva ist als inaktiv markiert.", await erfassen.Content.ReadAsStringAsync());
        Assert.False(await app.DatenbankAsync(db => db.Auftraege.AnyAsync(a => a.Betreff == "Für eine Inaktive")));

        // Steht Eva wieder in der Gruppe, ist sie wieder aktiv, derselbe Eintrag.
        app.Kundenquelle.Kunden.Add(Eva);
        ergebnis = await AbgleichenAsync(app);
        Assert.Equal((2, 0, 0, 1, 0), (ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv));
        Assert.Equal((eva.Id, true), ((await KundeAsync(app, "Test, Eva")).Id, (await KundeAsync(app, "Test, Eva")).Aktiv));
    }

    [Fact]
    public async Task Liefert_die_Quelle_nichts_oder_laesst_sie_sich_nicht_lesen_aendert_sich_nichts()
    {
        using var app = await OhneVerzeichniskundenAsync();
        app.Kundenquelle.Kunden.AddRange([Eva, Hans]);
        await AbgleichenAsync(app);

        // Eine leere Antwort würde sonst alle Kunden der Quelle auf einmal inaktiv machen.
        app.Kundenquelle.Kunden.Clear();
        var leer = await AbgleichenAsync(app);
        Assert.Contains("keinen einzigen Kunden geliefert", leer.Fehler);

        app.Kundenquelle.Gestoert = true;
        var gestoert = await AbgleichenAsync(app);
        Assert.Contains("konnte nicht gelesen werden: Der Server ist nicht funktionstüchtig.", gestoert.Fehler);

        Assert.True((await KundeAsync(app, "Test, Eva")).Aktiv);
        Assert.True((await KundeAsync(app, "Neu, Hans")).Aktiv);
    }

    [Fact]
    public async Task Eine_Adresse_die_schon_einem_anderen_Kunden_gehoert_wird_nicht_uebernommen()
    {
        using var app = await OhneVerzeichniskundenAsync();
        // Dieter ist in den Testdaten mit dem Konto "dieter" verknüpft. Ein anderes Konto nennt dieselbe Adresse.
        app.Kundenquelle.Kunden.AddRange([Hans, new("guid-doppelt", @"avis\doppelt", "Doppelt, Dora", "dieter.kunde@example.org", null, null)]);

        var ergebnis = await AbgleichenAsync(app);

        Assert.Null(ergebnis.Fehler);
        Assert.Contains("Doppelt, Dora: Die Adresse dieter.kunde@example.org gehört schon zum Kunden Dieter Kunde und wurde nicht übernommen.", Assert.Single(ergebnis.Hinweise!));
        Assert.Null((await KundeAsync(app, "Doppelt, Dora")).Email);
        Assert.Equal("dieter.kunde@example.org", (await KundeAsync(app, "Dieter Kunde")).Email);
    }

    [Fact]
    public async Task Kunden_aus_AVIS_markiert_man_von_Hand_als_inaktiv_Kunden_aus_der_Quelle_nicht()
    {
        using var app = await OhneVerzeichniskundenAsync();
        var anna = await app.AnmeldenAlsAsync("anna");
        app.Kundenquelle.Kunden.Add(Hans);
        await AbgleichenAsync(app);
        var frank = await KundeAsync(app, "Frank Müller");
        var hans = await KundeAsync(app, "Neu, Hans");

        // Frank steht nur in AVIS: von Hand inaktiv und wieder aktiv.
        var inaktiv = await anna.PostFormularAsync($"/Kunden/{frank.Id}", $"/Kunden/{frank.Id}?handler=Aktiv", new() { ["aktiv"] = "false" });
        Assert.Contains("Frank Müller ist jetzt als inaktiv markiert.", await inaktiv.Content.ReadAsStringAsync());
        Assert.False((await KundeAsync(app, "Frank Müller")).Aktiv);
        Assert.DoesNotContain("Frank Müller", await anna.GetStringAsync("/Kunden/Treffer?q=frank&leer=neu"));
        // Der Abgleich fasst ihn nicht an.
        await AbgleichenAsync(app);
        Assert.False((await KundeAsync(app, "Frank Müller")).Aktiv);
        await anna.PostFormularAsync($"/Kunden/{frank.Id}", $"/Kunden/{frank.Id}?handler=Aktiv", new() { ["aktiv"] = "true" });
        Assert.True((await KundeAsync(app, "Frank Müller")).Aktiv);

        // Hans stammt aus der Quelle: Dort entscheidet sich, ob er aktiv ist, wie er heißt und wo er sitzt.
        var seite = await anna.GetStringAsync($"/Kunden/{hans.Id}");
        Assert.Contains("Stammt aus ActiveDirectory.", seite);
        Assert.DoesNotContain("Als inaktiv markieren", seite);
        var abgelehnt = await anna.PostFormularAsync($"/Kunden/{hans.Id}", $"/Kunden/{hans.Id}?handler=Aktiv", new() { ["aktiv"] = "false" });
        Assert.Contains("Ob der Kunde aktiv ist, entscheidet sich dort.", await abgelehnt.Content.ReadAsStringAsync());
        Assert.DoesNotContain(">Speichern</button>", seite);
        var gesperrt = await anna.PostFormularAsync($"/Kunden/{hans.Id}", $"/Kunden/{hans.Id}", new() { ["name"] = "Von Hand umbenannt", ["email"] = "anders@example.org", ["standort"] = "Z 999" });
        Assert.Contains("Die Angaben werden dort gepflegt und lassen sich in AVIS nicht ändern.", await gesperrt.Content.ReadAsStringAsync());
        hans = await app.DatenbankAsync(db => db.Kunden.AsNoTracking().SingleAsync(k => k.Id == hans.Id));
        Assert.Equal(("Neu, Hans", "hans.neu@example.org", true, null), (hans.Name, hans.Email, hans.Aktiv, hans.Standort));
    }

    [Fact]
    public async Task Der_Standort_kommt_aus_der_Quelle_und_wird_sonst_von_Hand_eingetragen()
    {
        using var app = await OhneVerzeichniskundenAsync();
        var anna = await app.AnmeldenAlsAsync("anna");
        app.Kundenquelle.Kunden.Add(Hans with { Standort = " A 105 " });
        await AbgleichenAsync(app);
        var hans = await KundeAsync(app, "Neu, Hans");
        Assert.Equal("A 105", hans.Standort);

        // Er steht in der Liste, in der Auswahl beim Erfassen, und die Suche findet ihn.
        Assert.Contains("Neu, Hans", await anna.GetStringAsync("/Kunden?q=a+105"));
        Assert.Contains("· A 105", await anna.GetStringAsync("/Kunden/Treffer?q=hans&leer=neu"));

        // Von Hand lässt er sich bei einem Kunden aus der Quelle nicht ändern; ein Umzug kommt mit dem nächsten Abgleich.
        await anna.PostFormularAsync($"/Kunden/{hans.Id}", $"/Kunden/{hans.Id}", new() { ["name"] = "Neu, Hans", ["standort"] = "Z 999" });
        Assert.Equal("A 105", (await KundeAsync(app, "Neu, Hans")).Standort);
        app.Kundenquelle.Kunden.Clear();
        app.Kundenquelle.Kunden.Add(Hans with { Standort = "B 0.12" });
        var ergebnis = await AbgleichenAsync(app);
        Assert.Equal(1, ergebnis.Geaendert);
        Assert.Equal("B 0.12", (await KundeAsync(app, "Neu, Hans")).Standort);

        // Bei einem Kunden, der nur in AVIS steht, trägt ihn ein Bearbeiter ein.
        var frank = await KundeAsync(app, "Frank Müller");
        await anna.PostFormularAsync($"/Kunden/{frank.Id}", $"/Kunden/{frank.Id}", new() { ["name"] = "Frank Müller", ["email"] = frank.Email ?? "", ["standort"] = " C 0.04 " });
        Assert.Equal("C 0.04", (await KundeAsync(app, "Frank Müller")).Standort);
        Assert.Contains("Frank Müller", await anna.GetStringAsync("/Kunden?q=c+0.04"));
    }

    [Fact]
    public async Task Die_Verwaltung_zeigt_den_letzten_Abgleich_und_stoesst_einen_an()
    {
        using var app = await OhneVerzeichniskundenAsync();
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        app.Kundenquelle.Kunden.AddRange([Eva, Hans]);

        Assert.Contains("Seit dem Start von AVIS wurde noch nicht abgeglichen.", await anna.GetStringAsync("/Verwaltung/Kundenabgleich"));
        Assert.Contains("Kein Zugriff", await bernd.GetStringAsync("/Verwaltung/Kundenabgleich"));

        var seite = await (await anna.PostFormularAsync("/Verwaltung/Kundenabgleich", "/Verwaltung/Kundenabgleich", [])).Content.ReadAsStringAsync();
        Assert.Contains("Die Kunden wurden abgeglichen.", seite);
        Assert.Matches("<dt>Aus der Quelle gelesen</dt>\\s*<dd>2</dd>", seite);
        Assert.Matches("<dt>Neu angelegt</dt>\\s*<dd>1</dd>", seite);
        Assert.True(await app.DatenbankAsync(db => db.Kunden.AnyAsync(k => k.Name == "Neu, Hans")));

        // Ein gescheiterter Lauf steht als Fehler da.
        app.Kundenquelle.Gestoert = true;
        seite = await (await anna.PostFormularAsync("/Verwaltung/Kundenabgleich", "/Verwaltung/Kundenabgleich", [])).Content.ReadAsStringAsync();
        Assert.Contains("konnte nicht gelesen werden", seite);
    }

    [Fact]
    public async Task Ohne_Quelle_ist_der_Abgleich_aus_und_eine_halbe_Einstellung_laesst_AVIS_nicht_starten()
    {
        using var basis = new AvisAnwendung();
        using var ohne = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.RemoveAll<IKundenquelle>()));
        var client = ohne.CreateClient();
        await client.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = "anna" });

        Assert.Contains("Der Abgleich ist ausgeschaltet", await client.GetStringAsync("/Verwaltung/Kundenabgleich"));
        // Die Seite hat dann kein Formular; das Sicherheitstoken stammt von einer anderen.
        var antwort = await client.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Kundenabgleich", []);
        Assert.Equal(HttpStatusCode.NotFound, antwort.StatusCode);

        // Ohne Filter wäre jedes Konto der Domäne ein Kunde.
        using var halb = new AvisAnwendung();
        using var ohneFilter = halb.WithWebHostBuilder(b => b.UseSetting("Kundenabgleich:Art", "ActiveDirectory"));
        Assert.Contains("muss Kundenabgleich:Filter gesetzt sein", Assert.ThrowsAny<Exception>(() => ohneFilter.CreateClient()).ToString());

        using var schief = new AvisAnwendung();
        using var ohneKlammern = schief.WithWebHostBuilder(b => b.UseSetting("Kundenabgleich:Art", "ActiveDirectory").UseSetting("Kundenabgleich:Filter", "department=*"));
        Assert.Contains("muss ein LDAP-Filter in Klammern sein", Assert.ThrowsAny<Exception>(() => ohneKlammern.CreateClient()).ToString());
    }

    [Fact]
    public void Der_eingestellte_Filter_wird_auf_aktive_Benutzerkonten_eingeschraenkt()
    {
        var optionen = new KundenabgleichOptionen { Filter = " (memberOf=CN=AVIS-Kunden,OU=Gruppen,DC=example,DC=org) " };

        Assert.Equal(
            "(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2))(memberOf=CN=AVIS-Kunden,OU=Gruppen,DC=example,DC=org))",
            optionen.Suchfilter);
    }

    [Fact]
    public async Task Die_Testdaten_enthalten_Kunden_aus_dem_Verzeichnis_und_stillgelegte()
    {
        using var app = new AvisAnwendung();
        var anna = await app.AnmeldenAlsAsync("anna");

        var kunden = await app.DatenbankAsync(db => db.Kunden.AsNoTracking().OrderBy(k => k.Name).ToListAsync());
        Assert.Equal(
            [("Hanna Verzeichnis", @"avis\hanna", true), ("Igor Abgleich", @"avis\igor", true), ("Jana Ehemals", @"avis\jana", false)],
            kunden.Where(k => k.Quelle == "ActiveDirectory").Select(k => (k.Name, k.Anmeldename!, k.Aktiv)));
        Assert.All(kunden.Where(k => k.AusQuelle), k => Assert.True(Guid.TryParse(k.ExterneKennung, out _)));
        Assert.Equal(["Karl Ruhestand"], kunden.Where(k => !k.AusQuelle && !k.Aktiv).Select(k => k.Name));

        // Wie echte: Die Angaben pflegt das Verzeichnis, und wer dort fehlt, ist markiert und nicht wählbar.
        var hanna = kunden.Single(k => k.Name == "Hanna Verzeichnis");
        Assert.Contains("Stammt aus ActiveDirectory.", await anna.GetStringAsync($"/Kunden/{hanna.Id}"));
        Assert.Matches("Jana Ehemals</a>\\s*<span class=\"hinweis\">inaktiv</span>", await anna.GetStringAsync("/Kunden?q=bauamt"));
        var treffer = await anna.GetStringAsync("/Kunden/Treffer?q=bauamt&leer=neu");
        Assert.Contains("Hanna Verzeichnis", treffer);
        Assert.DoesNotContain("Jana Ehemals", treffer);
    }

    /// <summary>
    /// Eine Anwendung ohne die Kunden aus dem Verzeichnis, die die Testdaten
    /// mitbringen: Diese Tests zählen genau, was ein Abgleich tut.
    /// </summary>
    private static async Task<AvisAnwendung> OhneVerzeichniskundenAsync()
    {
        var app = new AvisAnwendung();
        // Zuerst die Geräte aus Snipe-IT: Eines davon nennt einen dieser Kunden als Nutzer.
        await app.DatenbankAsync(db => db.Geraete.Where(g => g.Quelle != null).ExecuteDeleteAsync());
        await app.DatenbankAsync(db => db.Kunden.Where(k => k.Quelle != null).ExecuteDeleteAsync());
        return app;
    }

    private static async Task<Abgleichergebnis> AbgleichenAsync(AvisAnwendung app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Kundenabgleich>().AusfuehrenAsync();
    }

    private static Task<Kunde> KundeAsync(AvisAnwendung app, string name) =>
        app.DatenbankAsync(db => db.Kunden.AsNoTracking().SingleAsync(k => k.Name == name));
}
