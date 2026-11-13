using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

public class SucheTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Theory]
    [InlineData("ÜBERHITZT", true)]            // Betreff, ohne Rücksicht auf Groß- und Kleinschreibung
    [InlineData("lüftergeräusch", true)]       // Beschreibung
    [InlineData("srv-käfig-07", true)]         // Gerät
    [InlineData("özlem", true)]                // Kunde
    [InlineData("wärmeleitpaste", true)]       // interner Kommentar im Verlauf
    [InlineData("überhitzt özlem srv-käfig", true)] // jedes Wort in einem anderen Feld
    [InlineData("überhitzt gibtesnicht", false)]
    public async Task Die_Suche_findet_Auftraege_ueber_alle_Texte(string suche, bool erwartet)
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        await BeispielAsync(anna);

        var seite = await anna.GetStringAsync($"/Auftraege?q={Uri.EscapeDataString(suche)}");

        Assert.Equal(erwartet, seite.Contains("Server im Keller überhitzt"));
    }

    [Fact]
    public async Task Eine_Auftragsnummer_fuehrt_direkt_zum_Auftrag()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await BeispielAsync(anna);

        Assert.Equal($"/Auftraege/{id}", (await anna.GetAsync($"/Auftraege?q={id}")).RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal($"/Auftraege/{id}", (await anna.GetAsync($"/Auftraege?q=%23{id}")).RequestMessage!.RequestUri!.AbsolutePath);
        // Eine Nummer, die es nicht gibt, ist eine gewöhnliche Suche.
        Assert.Equal("/Auftraege", (await anna.GetAsync("/Auftraege?q=987654")).RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Eine_Suche_schliesst_abgeschlossene_Auftraege_ein_die_Liste_ohne_Suche_nicht()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Einmaliges Suchwort Quastenflosser", null);
        var seite = $"/Auftraege/{id}";
        await anna.PostFormularAsync(seite, $"{seite}?handler=Uebernehmen", new() { ["version"] = "1" });
        await anna.PostFormularAsync(seite, $"{seite}?handler=Abschliessen", new() { ["version"] = "2", ["text"] = "Erledigt." });

        Assert.DoesNotContain("Quastenflosser", await anna.GetStringAsync("/Auftraege"));
        Assert.Contains("Quastenflosser", await anna.GetStringAsync("/Auftraege?q=quastenflosser"));
        Assert.DoesNotContain("Einmaliges Suchwort", await anna.GetStringAsync("/Auftraege?q=quastenflosser&status=aktiv"));
        Assert.Contains("Quastenflosser", await anna.GetStringAsync("/Auftraege?status=abgeschlossen"));
        Assert.Contains("Quastenflosser", await anna.GetStringAsync("/Auftraege?status=alle"));
    }

    [Fact]
    public async Task Gruppe_und_Bearbeiter_grenzen_die_Liste_ein()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var netzwerk = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Netzwerk").Select(g => g.Id).FirstAsync());
        var berndId = await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "bernd").Select(b => b.Id).FirstAsync());
        var imNetzwerk = await ErfassenAsync(anna, "Filtertest Netzwerk", netzwerk);
        await ErfassenAsync(anna, "Filtertest Hotline", null);
        await bernd.PostFormularAsync($"/Auftraege/{imNetzwerk}", $"/Auftraege/{imNetzwerk}?handler=Uebernehmen", new() { ["version"] = "1" });

        var nachGruppe = await anna.GetStringAsync($"/Auftraege?q=filtertest&gruppe={netzwerk}");
        Assert.Contains("Filtertest Netzwerk", nachGruppe);
        Assert.DoesNotContain("Filtertest Hotline", nachGruppe);

        var nachBearbeiter = await anna.GetStringAsync($"/Auftraege?bearbeiter={berndId}");
        Assert.Contains("Filtertest Netzwerk", nachBearbeiter);
        Assert.DoesNotContain("Filtertest Hotline", nachBearbeiter);

        Assert.DoesNotContain("Filtertest Netzwerk", await anna.GetStringAsync($"/Auftraege?q=filtertest&status=offen"));
    }

    [Fact]
    public async Task Die_Suche_findet_nichts_aus_gesperrten_Gruppen_fuer_Aussenstehende()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        await anna.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Gruppen", new() { ["name"] = "Verschlusssachen" });
        var gruppe = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Verschlusssachen").Select(g => g.Id).FirstAsync());
        await anna.PostFormularAsync($"/Verwaltung/Gruppen/{gruppe}", $"/Verwaltung/Gruppen/{gruppe}?handler=Speichern", new() { ["name"] = "Verschlusssachen", ["aktiv"] = "true", ["gesperrt"] = "true" });
        var id = await ErfassenAsync(anna, "Zobelpelz im Tresor", gruppe);

        Assert.Contains("Zobelpelz", await anna.GetStringAsync("/Auftraege?q=zobelpelz"));
        Assert.DoesNotContain("Zobelpelz im Tresor", await bernd.GetStringAsync("/Auftraege?q=zobelpelz"));
        // Auch die Nummer verrät ihm nichts.
        Assert.Equal("/Auftraege", (await bernd.GetAsync($"/Auftraege?q={id}")).RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Eine_Aenderung_am_Auftrag_ist_sofort_suchbar_und_der_alte_Text_nicht_mehr()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Vorher Zitronenfalter", null);
        await anna.UebernehmenAsync(app, id);
        var seite = $"/Auftraege/{id}/Bearbeiten";

        await anna.PostFormularAsync(seite, seite, new()
        {
            ["Version"] = "2",
            ["Felder.Betreff"] = "Nachher Schwalbenschwanz",
            ["Felder.Beschreibung"] = "Für einen Test.",
        });

        Assert.Contains("Nachher Schwalbenschwanz", await anna.GetStringAsync("/Auftraege?q=schwalbenschwanz"));
        Assert.DoesNotContain("Nachher Schwalbenschwanz", await anna.GetStringAsync("/Auftraege?q=zitronenfalter"));
    }

    /// <summary>Ein Auftrag mit Suchbarem in jedem Feld. Wird je Testlauf nur einmal angelegt.</summary>
    private async Task<int> BeispielAsync(HttpClient anna)
    {
        var vorhanden = await app.DatenbankAsync(db => db.Auftraege.Where(a => a.Betreff == "Server im Keller überhitzt").Select(a => (int?)a.Id).FirstOrDefaultAsync());
        if (vorhanden is not null)
            return vorhanden.Value;

        var antwort = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["kundeId"] = (await app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == "Özlem Übung").Select(k => k.Id).FirstAsync())).ToString(),
            ["Eingabe.Felder.Betreff"] = "Server im Keller überhitzt",
            ["Eingabe.Felder.Beschreibung"] = "Auffälliges Lüftergeräusch seit dem Wochenende.",
            ["Eingabe.Felder.Geraet"] = "SRV-Käfig-07",
            ["Eingabe.GruppeId"] = (await HotlineAsync()).ToString(),
        });
        var id = int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        await anna.UebernehmenAsync(app, id);
        await anna.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=InternerKommentar", new() { ["version"] = "2", ["text"] = "Wärmeleitpaste erneuern." });
        return id;
    }

    private async Task<int> ErfassenAsync(HttpClient client, string betreff, int? gruppe)
    {
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (gruppe ?? await HotlineAsync()).ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private Task<int> HotlineAsync() =>
        app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync());
}
