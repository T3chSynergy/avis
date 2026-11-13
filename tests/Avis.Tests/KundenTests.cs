using System.Net;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

public class KundenTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Theory]
    [InlineData("müller", new[] { "Frank Müller", "Gabi Müller" })]
    [InlineData("MÜLLER", new[] { "Frank Müller", "Gabi Müller" })]
    [InlineData("müller personal", new[] { "Gabi Müller" })]
    [InlineData("özlem", new[] { "Özlem Übung" })]
    [InlineData("ÜBUNG", new[] { "Özlem Übung" })]
    [InlineData("gabi.mueller@", new[] { "Gabi Müller" })]
    [InlineData("1234", new[] { "Dieter Kunde", "Özlem Übung" })]
    [InlineData("0221", new[] { "Özlem Übung" })]
    [InlineData("gibtesnicht", new string[0])]
    public async Task Die_Suche_findet_Kunden_ueber_Name_EMail_Telefon_und_Organisationseinheit(string suche, string[] erwartet)
    {
        var gefunden = await app.DatenbankAsync(db =>
            db.Kunden.Suchen(suche).OrderBy(k => k.Name).Select(k => k.Name).ToListAsync());

        Assert.Equal(erwartet.Order(), gefunden.Order());
    }

    [Theory]
    [InlineData("/Kunden")]
    [InlineData("/Kunden?q=müller")]
    [InlineData("/Kunden/1")]
    [InlineData("/Kunden/Treffer?q=müller&leer=neu")]
    [InlineData("/Auftraege/1/Kunde")]
    public async Task Seiten_antworten(string pfad)
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        Assert.Equal(HttpStatusCode.OK, (await anna.GetAsync(pfad)).StatusCode);
    }

    [Fact]
    public async Task Die_Trefferliste_kommt_ohne_Seitenrahmen_und_haelt_den_gewaehlten_Kunden_sichtbar()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var dieter = await KundeIdAsync("Dieter Kunde");

        var treffer = await anna.GetStringAsync($"/Kunden/Treffer?q=müller&gewaehlt={dieter}&leer=neu");

        Assert.DoesNotContain("<html", treffer);
        Assert.Contains("Frank Müller", treffer);
        Assert.Contains("Gabi Müller", treffer);
        Assert.Contains($"id=\"kunde-{dieter}\" value=\"{dieter}\" checked", treffer);
        Assert.Contains("Neuer Kunde", treffer);
        Assert.DoesNotContain("Neuer Kunde", await anna.GetStringAsync("/Kunden/Treffer?q=müller"));
    }

    [Fact]
    public async Task Beim_Erfassen_zeigt_die_Suche_Treffer_und_behaelt_die_Eingaben()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var antwort = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu?handler=Suchen", new()
        {
            ["suche"] = "müller",
            ["Eingabe.Felder.Betreff"] = "Schon getippter Betreff",
        });

        var text = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Frank Müller", text);
        Assert.Contains("Schon getippter Betreff", text);
        Assert.DoesNotContain("field-validation-error", text);
    }

    [Fact]
    public async Task Die_Eingabetaste_im_Suchfeld_sucht_statt_zu_erfassen()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var vorher = await app.DatenbankAsync(db => db.Auftraege.CountAsync());

        var antwort = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new() { ["suche"] = "özlem" });

        var text = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Özlem Übung", text);
        Assert.DoesNotContain("field-validation-error", text);
        Assert.Equal(vorher, await app.DatenbankAsync(db => db.Auftraege.CountAsync()));
    }

    [Fact]
    public async Task Ein_Auftrag_wird_fuer_einen_gefundenen_Kunden_erfasst()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var frank = await KundeIdAsync("Frank Müller");
        var kunden = await app.DatenbankAsync(db => db.Kunden.CountAsync());

        var id = await ErfassenAsync(anna, "Bildschirm bleibt schwarz", new()
        {
            ["kundeId"] = frank.ToString(),
            // Reste in den Feldern für einen neuen Kunden zählen dann nicht.
            ["Eingabe.KundeName"] = "Jemand anderes",
            ["Eingabe.KundeEmail"] = "keine adresse",
        });

        Assert.Equal(frank, (await LadenAsync(id)).KundeId);
        Assert.Equal(kunden, await app.DatenbankAsync(db => db.Kunden.CountAsync()));
    }

    [Fact]
    public async Task Ohne_Kunde_wird_kein_Auftrag_erfasst()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var antwort = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.Felder.Betreff"] = "Ohne Kunde",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await HotlineIdAsync()).ToString(),
        });

        Assert.Contains("Bitte einen Kunden auswählen oder den Namen eines neuen Kunden angeben.", await antwort.Content.ReadAsStringAsync());
        Assert.False(await app.DatenbankAsync(db => db.Auftraege.AnyAsync(a => a.Betreff == "Ohne Kunde")));
    }

    [Fact]
    public async Task Ein_neuer_Kunde_ist_sofort_ueber_die_Suche_zu_finden()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        await ErfassenAsync(anna, "Neuer Kollege braucht Zugang", new()
        {
            ["Eingabe.KundeName"] = "Jörg Ähnlich",
            ["Eingabe.KundeTelefon"] = "(030) 55 66-77",
        });

        Assert.Contains("Jörg Ähnlich", await anna.GetStringAsync("/Kunden?q=ÄHNLICH"));
        Assert.Contains("Jörg Ähnlich", await anna.GetStringAsync("/Kunden?q=0305566"));
    }

    [Fact]
    public async Task Der_Kunde_eines_Auftrags_laesst_sich_aendern_und_steht_im_Verlauf()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Falsch zugeordnet", new() { ["kundeId"] = (await KundeIdAsync("Frank Müller")).ToString() });
        await anna.UebernehmenAsync(app, id);
        var gabi = await KundeIdAsync("Gabi Müller");
        var seite = $"/Auftraege/{id}/Kunde";
        var version = (await LadenAsync(id)).Version;

        // Erst suchen, dann auswählen.
        var suche = await anna.PostFormularAsync(seite, $"{seite}?handler=Suchen", new() { ["version"] = version.ToString(), ["suche"] = "gabi" });
        Assert.Contains("Gabi Müller", await suche.Content.ReadAsStringAsync());

        var antwort = await anna.PostFormularAsync(seite, seite, new() { ["version"] = version.ToString(), ["kundeId"] = gabi.ToString() });

        var text = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Der Auftrag gehört jetzt zu Gabi Müller.", text);
        Assert.Contains("Kunde: Frank Müller → Gabi Müller", text);
        var auftrag = await LadenAsync(id);
        Assert.Equal(gabi, auftrag.KundeId);
        Assert.False(auftrag.Verlauf.OrderBy(v => v.Id).Last().FuerKundenSichtbar);

        // Derselbe Kunde noch einmal und ein veralteter Stand werden abgelehnt.
        var nochmal = await anna.PostFormularAsync(seite, seite, new() { ["version"] = auftrag.Version.ToString(), ["kundeId"] = gabi.ToString() });
        Assert.Contains("ist bereits der Kunde", await nochmal.Content.ReadAsStringAsync());
        var veraltet = await anna.PostFormularAsync(seite, seite, new() { ["version"] = version.ToString(), ["kundeId"] = (await KundeIdAsync("Eva Test")).ToString() });
        Assert.Contains("inzwischen geändert", await veraltet.Content.ReadAsStringAsync());
        Assert.Equal(gabi, (await LadenAsync(id)).KundeId);
    }

    [Fact]
    public async Task Die_Kundenseite_zeigt_die_Auftraege_des_Kunden()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var eva = await KundeIdAsync("Eva Test");
        await ErfassenAsync(anna, "Nur für Eva sichtbar hier", new() { ["kundeId"] = eva.ToString() });

        Assert.Contains("Nur für Eva sichtbar hier", await anna.GetStringAsync($"/Kunden/{eva}"));
        Assert.DoesNotContain("Nur für Eva sichtbar hier", await anna.GetStringAsync($"/Kunden/{await KundeIdAsync("Gabi Müller")}"));
    }

    [Fact]
    public async Task Ein_Kunde_wird_angelegt_und_seine_Angaben_geaendert()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        await anna.PostFormularAsync("/Kunden", "/Kunden", new() { ["name"] = "Karl Neu", ["email"] = "Karl.Neu@Example.org" });
        var id = await KundeIdAsync("Karl Neu");
        var standort = await app.DatenbankAsync(db => db.Standorte.Where(s => s.Name == "Nebenstelle").Select(s => s.Id).FirstAsync());

        var antwort = await anna.PostFormularAsync($"/Kunden/{id}", $"/Kunden/{id}", new()
        {
            ["name"] = "Karl Neumann",
            ["email"] = "karl.neumann@example.org",
            ["telefon"] = "4711",
            ["organisationseinheit"] = "Bauamt",
            ["standortId"] = standort.ToString(),
        });

        Assert.Contains("Karl Neumann wurde gespeichert.", await antwort.Content.ReadAsStringAsync());
        var kunde = await app.DatenbankAsync(db => db.Kunden.FirstAsync(k => k.Id == id));
        Assert.Equal(("Karl Neumann", "karl.neumann@example.org", "4711", "Bauamt", standort),
            (kunde.Name, kunde.Email, kunde.Telefon, kunde.Organisationseinheit, kunde.StandortId!.Value));

        // Die Suche kennt die neuen Angaben und vergisst die alten.
        Assert.Contains("Karl Neumann", await anna.GetStringAsync("/Kunden?q=bauamt"));
        Assert.DoesNotContain("Karl Neumann", await anna.GetStringAsync("/Kunden?q=karl.neu@"));
    }

    [Fact]
    public async Task Eine_EMail_Adresse_gehoert_nur_einem_Kunden()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var gabi = await KundeIdAsync("Gabi Müller");

        var anlegen = await anna.PostFormularAsync("/Kunden", "/Kunden", new() { ["name"] = "Doppelt", ["email"] = "FRANK.MUELLER@example.org" });
        var aendern = await anna.PostFormularAsync($"/Kunden/{gabi}", $"/Kunden/{gabi}", new() { ["name"] = "Gabi Müller", ["email"] = "frank.mueller@example.org" });

        Assert.Contains("gehört schon zum Kunden Frank Müller", await anlegen.Content.ReadAsStringAsync());
        Assert.Contains("gehört schon zum Kunden Frank Müller", await aendern.Content.ReadAsStringAsync());
        Assert.False(await app.DatenbankAsync(db => db.Kunden.AnyAsync(k => k.Name == "Doppelt")));
        Assert.Equal("gabi.mueller@example.org", await app.DatenbankAsync(db => db.Kunden.Where(k => k.Id == gabi).Select(k => k.Email).FirstAsync()));
    }

    private async Task<int> ErfassenAsync(HttpClient client, string betreff, Dictionary<string, string> kunde)
    {
        var felder = new Dictionary<string, string>(kunde)
        {
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await HotlineIdAsync()).ToString(),
        };
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", felder);
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private Task<int> HotlineIdAsync() =>
        app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync());

    private Task<int> KundeIdAsync(string name) =>
        app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == name).Select(k => k.Id).FirstAsync());

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
