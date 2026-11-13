using System.Net;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

public class GeraeteTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Theory]
    [InlineData("/Geraete")]
    [InlineData("/Geraete?q=drucker")]
    [InlineData("/Geraete/1")]
    [InlineData("/Geraete/Vorschlaege?q=nb")]
    public async Task Seiten_antworten_fuer_Bearbeiter(string pfad)
    {
        var bernd = await app.AnmeldenAlsAsync("bernd");

        Assert.Equal(HttpStatusCode.OK, (await bernd.GetAsync(pfad)).StatusCode);
    }

    [Theory]
    [InlineData("dr-0042", new[] { "DR-0042" })]
    [InlineData("LASERDRUCKER", new[] { "DR-0042" })]
    [InlineData("notebook", new[] { "NB-1017", "NB-1018" })]
    [InlineData("notebook dieter", new[] { "NB-1017" })]
    [InlineData("gibtesnicht", new string[0])]
    public async Task Die_Suche_findet_Geraete_ueber_Nummer_Bezeichnung_Typ_und_Nutzer(string suche, string[] erwartet)
    {
        var gefunden = await app.DatenbankAsync(db =>
            db.Geraete.Suchen(suche).OrderBy(g => g.Inventarnummer).Select(g => g.Inventarnummer).ToListAsync());

        Assert.Equal(erwartet, gefunden);
    }

    [Fact]
    public async Task Nur_Administratoren_legen_Geraete_an_und_aendern_sie()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");

        Assert.Contains("Kein Zugriff", await bernd.GetStringAsync("/Geraete/Bearbeiten"));
        var heimlich = await bernd.PostFormularAsync("/Geraete", "/Geraete/Bearbeiten", new() { ["Eingabe.Inventarnummer"] = "XX-1", ["Eingabe.Bezeichnung"] = "Heimlich" });
        Assert.Contains("Kein Zugriff", await heimlich.Content.ReadAsStringAsync());
        Assert.False(await app.DatenbankAsync(db => db.Geraete.AnyAsync(g => g.Inventarnummer == "XX-1")));

        var angelegt = await anna.PostFormularAsync("/Geraete/Bearbeiten", "/Geraete/Bearbeiten", new()
        {
            ["Eingabe.Inventarnummer"] = " SC-3001 ",
            ["Eingabe.Bezeichnung"] = "Dokumentenscanner",
            ["Eingabe.Typ"] = "Scanner",
            ["Eingabe.Aktiv"] = "true",
            ["kundeId"] = (await KundeIdAsync("Frank Müller")).ToString(),
        });
        Assert.Contains("Gerät SC-3001 wurde gespeichert.", await angelegt.Content.ReadAsStringAsync());
        var geraet = await app.DatenbankAsync(db => db.Geraete.Include(g => g.Kunde).SingleAsync(g => g.Inventarnummer == "SC-3001"));
        Assert.Equal(("Dokumentenscanner", "Scanner", "Frank Müller", true), (geraet.Bezeichnung, geraet.Typ, geraet.Kunde!.Name, geraet.Aktiv));

        // Der Nutzer sieht man am Kunden, und das Gerät lässt sich wieder niemandem zuordnen.
        Assert.Contains("SC-3001", await anna.GetStringAsync($"/Kunden/{geraet.KundeId}"));
        var seite = $"/Geraete/Bearbeiten/{geraet.Id}";
        Assert.Contains("Kein Kunde", await anna.GetStringAsync(seite));
        await anna.PostFormularAsync(seite, seite, new()
        {
            ["Eingabe.Inventarnummer"] = "SC-3001",
            ["Eingabe.Bezeichnung"] = "Dokumentenscanner A3",
            ["Eingabe.Aktiv"] = "true",
            ["kundeId"] = "",
        });
        geraet = await app.DatenbankAsync(db => db.Geraete.SingleAsync(g => g.Inventarnummer == "SC-3001"));
        Assert.Equal(("Dokumentenscanner A3", null, null), (geraet.Bezeichnung, geraet.Typ, geraet.KundeId));
    }

    [Fact]
    public async Task Eine_Inventarnummer_gibt_es_nur_einmal()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var antwort = await anna.PostFormularAsync("/Geraete/Bearbeiten", "/Geraete/Bearbeiten", new()
        {
            ["Eingabe.Inventarnummer"] = "dr-0042",
            ["Eingabe.Bezeichnung"] = "Doppelt",
        });

        Assert.Contains("Diese Inventarnummer gibt es schon.", await antwort.Content.ReadAsStringAsync());
        Assert.Equal(1, await app.DatenbankAsync(db => db.Geraete.CountAsync(g => g.Nummernschluessel == "dr-0042")));
    }

    [Fact]
    public async Task Die_Kundensuche_im_Geraeteformular_behaelt_die_Eingaben()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var antwort = await anna.PostFormularAsync("/Geraete/Bearbeiten", "/Geraete/Bearbeiten?handler=Suchen", new()
        {
            ["Eingabe.Inventarnummer"] = "TB-9",
            ["suche"] = "özlem",
        });

        var text = await antwort.Content.ReadAsStringAsync();
        Assert.Contains("Özlem Übung", text);
        Assert.Contains("value=\"TB-9\"", text);
        Assert.DoesNotContain("field-validation-error", text);
    }

    [Fact]
    public async Task Ein_Auftrag_wird_ueber_die_Inventarnummer_mit_einem_Geraet_verknuepft()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var id = await ErfassenAsync(anna, "Monitor zeigt Streifen", "mo-2203");

        var auftrag = await LadenAsync(id);
        Assert.Equal("MO-2203", auftrag.Inventargeraet!.Inventarnummer);
        var seite = await anna.GetStringAsync($"/Auftraege/{id}");
        Assert.Contains($"/Geraete/{auftrag.InventargeraetId}", seite);
        Assert.Contains("Monitor 27 Zoll", seite);
        Assert.Contains("Monitor zeigt Streifen", await anna.GetStringAsync($"/Geraete/{auftrag.InventargeraetId}"));
        // Die Suche findet den Auftrag über die Inventarnummer.
        Assert.Contains("Monitor zeigt Streifen", await anna.GetStringAsync("/Auftraege?q=MO-2203"));
    }

    [Fact]
    public async Task Eine_unbekannte_Inventarnummer_wird_abgelehnt()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var vorher = await app.DatenbankAsync(db => db.Auftraege.CountAsync());

        var antwort = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = "Unbekanntes Gerät",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.Felder.Inventarnummer"] = "ZZ-0000",
            ["Eingabe.GruppeId"] = (await HotlineAsync()).ToString(),
        });

        Assert.Contains("Im Inventar gibt es kein Gerät mit dieser Nummer.", await antwort.Content.ReadAsStringAsync());
        Assert.Equal(vorher, await app.DatenbankAsync(db => db.Auftraege.CountAsync()));
    }

    [Fact]
    public async Task Die_Verknuepfung_laesst_sich_aendern_und_loesen_und_steht_im_Verlauf()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Notebook lädt nicht", "NB-1017");
        var seite = $"/Auftraege/{id}/Bearbeiten";
        Assert.Contains("value=\"NB-1017\"", await anna.GetStringAsync(seite));

        await BearbeitenAsync(anna, id, "NB-1018");
        var geaendert = await LadenAsync(id);
        Assert.Equal("NB-1018", geaendert.Inventargeraet!.Inventarnummer);
        Assert.Equal("Gerät aus dem Inventar: NB-1017 → NB-1018", geaendert.Verlauf.OrderBy(v => v.Id).Last().Beschreibung);

        await BearbeitenAsync(anna, id, "");
        var geloest = await LadenAsync(id);
        Assert.Null(geloest.InventargeraetId);
        Assert.Equal("Gerät aus dem Inventar: NB-1018 → –", geloest.Verlauf.OrderBy(v => v.Id).Last().Beschreibung);
    }

    [Fact]
    public async Task Ein_ausgemustertes_Geraet_bleibt_am_Auftrag_laesst_sich_aber_nicht_neu_verknuepfen()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        await anna.PostFormularAsync("/Geraete/Bearbeiten", "/Geraete/Bearbeiten", new()
        {
            ["Eingabe.Inventarnummer"] = "PC-0001",
            ["Eingabe.Bezeichnung"] = "Alter Arbeitsplatzrechner",
            ["Eingabe.Aktiv"] = "true",
        });
        var id = await ErfassenAsync(anna, "Alter Rechner piept", "PC-0001");
        await app.DatenbankAsync(db => db.Geraete.Where(g => g.Inventarnummer == "PC-0001").ExecuteUpdateAsync(g => g.SetProperty(x => x.Aktiv, false)));

        // Am bestehenden Auftrag bleibt es beim Bearbeiten erhalten.
        await BearbeitenAsync(anna, id, "PC-0001", betreff: "Alter Rechner piept dreimal");
        var auftrag = await LadenAsync(id);
        Assert.Equal(("Alter Rechner piept dreimal", "PC-0001"), (auftrag.Betreff, auftrag.Inventargeraet!.Inventarnummer));

        // Neu verknüpfen lässt es sich nicht, und es wird nicht mehr vorgeschlagen.
        var antwort = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = "Noch ein alter Rechner",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.Felder.Inventarnummer"] = "PC-0001",
            ["Eingabe.GruppeId"] = (await HotlineAsync()).ToString(),
        });
        Assert.Contains("Im Inventar gibt es kein Gerät mit dieser Nummer.", await antwort.Content.ReadAsStringAsync());
        Assert.DoesNotContain("PC-0001", await anna.GetStringAsync("/Geraete/Vorschlaege?q=pc-00"));
        Assert.Contains("ausgemustert", await anna.GetStringAsync("/Geraete?q=pc-0001"));
    }

    [Fact]
    public async Task Vorschlaege_kommen_ohne_Seitenrahmen()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var vorschlaege = await anna.GetStringAsync("/Geraete/Vorschlaege?q=nb-10");

        Assert.DoesNotContain("<html", vorschlaege);
        Assert.Contains("<option value=\"NB-1017\">Notebook 14 Zoll · Haupthaus · Dieter Kunde</option>", vorschlaege);
        Assert.Contains("NB-1018", vorschlaege);
        Assert.Equal("", (await anna.GetStringAsync("/Geraete/Vorschlaege?q=n")).Trim());
    }

    private async Task<int> ErfassenAsync(HttpClient client, string betreff, string inventarnummer)
    {
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.Felder.Inventarnummer"] = inventarnummer,
            ["Eingabe.GruppeId"] = (await HotlineAsync()).ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private async Task BearbeitenAsync(HttpClient client, int id, string inventarnummer, string? betreff = null)
    {
        var auftrag = await LadenAsync(id);
        var seite = $"/Auftraege/{id}/Bearbeiten";
        var antwort = await client.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = auftrag.Version.ToString(),
            ["Felder.Betreff"] = betreff ?? auftrag.Betreff,
            ["Felder.Beschreibung"] = auftrag.Beschreibung,
            ["Felder.Inventarnummer"] = inventarnummer,
        });
        Assert.Equal($"/Auftraege/{id}", antwort.RequestMessage!.RequestUri!.AbsolutePath);
    }

    private Task<int> HotlineAsync() =>
        app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync());

    private Task<int> KundeIdAsync(string name) =>
        app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == name).Select(k => k.Id).FirstAsync());

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Inventargeraet)
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
