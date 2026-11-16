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

    [Theory]
    [InlineData("Der Monitor INV MO-2203 flackert.", "mo-2203")]
    [InlineData("inv 4532436 und INV:4532437, dazu (INV-4532438).", "4532436|4532437|4532438")]
    [InlineData("INV4711 ohne Abstand, Inv. 4712 mit Punkt, INV #4713 mit Raute", "4711|4712|4713")]
    [InlineData("**INV NB-1017** in fetter Schrift\nund am Zeilenende INV NB-1018", "nb-1017|nb-1018")]
    [InlineData("INV A_1.2-3, dann geht der Satz weiter.", "a_1.2-3")]
    [InlineData("Das Inventar, eine Investition und die Inventarnummer 4711 sind nicht gemeint. KINV 5 auch nicht.", "")]
    [InlineData("INV", "")]
    public void Geraete_werden_mit_INV_und_ihrer_Inventarnummer_genannt(string text, string erwartet)
    {
        var nummern = Geraetebezug.Nummern([text, null, ""]);

        // Zu jeder Nennung gehören auch die Schreibweisen, bei denen die Nummer im Inventar selbst mit "INV" beginnt.
        var genannt = nummern.Where(n => !n.StartsWith("inv", StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(erwartet.Split('|', StringSplitOptions.RemoveEmptyEntries).Order(), genannt);
        Assert.All(genannt, n => Assert.Superset(new HashSet<string> { "inv" + n, "inv-" + n }, nummern));
    }

    [Fact]
    public async Task Wer_Geraete_im_Text_nennt_verknuepft_sie_mit_dem_Auftrag()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var id = await ErfassenAsync(anna, "Monitor zeigt Streifen", "Der Monitor INV mo-2203 zeigt Streifen, am Notebook (INV NB-1017) auch. INV ZZ-0000 kennt niemand.");

        // Zwei Geräte sind verknüpft. Die unbekannte Nummer bleibt Text und stört nicht.
        Assert.Equal(["MO-2203", "NB-1017"], await GeraeteAsync(id));
        var monitor = await GeraetIdAsync("MO-2203");
        var seite = await anna.GetStringAsync($"/Auftraege/{id}");
        Assert.Contains($"/Geraete/{monitor}", seite);
        Assert.Contains("Monitor 27 Zoll", seite);
        Assert.Contains("Notebook 14 Zoll", seite);

        // Am Gerät steht der Auftrag, die Liste zählt ihn, und die Suche findet ihn über die Inventarnummer.
        Assert.Contains("Monitor zeigt Streifen", await anna.GetStringAsync($"/Geraete/{monitor}"));
        // Andere Tests dieser Klasse nennen den Monitor auch; die Suche liefert nur seine Zeile.
        Assert.Matches("MO-2203</a>(?s:.*?)<td>[1-9]\\d*</td>\\s*</tr>", await anna.GetStringAsync("/Geraete?q=mo-2203"));
        Assert.Contains("Monitor zeigt Streifen", await anna.GetStringAsync("/Auftraege?q=NB-1017"));

        // Auch der Betreff zählt.
        Assert.Equal(["NB-1018"], await GeraeteAsync(await ErfassenAsync(anna, "INV NB-1018 startet nicht", "Für einen Test.")));
    }

    [Fact]
    public async Task Spaetere_Texte_verknuepfen_weitere_Geraete_und_gestrichene_Nennungen_loesen_sie()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Notebook lädt nicht", "Es geht um INV NB-1017.");
        await anna.UebernehmenAsync(app, id);
        var seite = $"/Auftraege/{id}";

        // Ein interner Kommentar nennt das Ersatzgerät, eine Nachricht an den Kunden den Monitor.
        await AktionAsync(anna, id, "InternerKommentar", "Getauscht gegen INV NB-1018.");
        await AktionAsync(anna, id, "NachrichtAnKunden", "Bitte auch INV MO-2203 mitbringen.");
        Assert.Equal(["MO-2203", "NB-1017", "NB-1018"], await GeraeteAsync(id));

        // Die Beschreibung nennt das Notebook nicht mehr: Es ist gelöst. Ihr früherer Text steht im Verlauf,
        // zählt aber nicht. Was in Kommentar und Nachricht steht, bleibt.
        var auftrag = await LadenAsync(id);
        await anna.PostFormularAsync($"{seite}/Bearbeiten", $"{seite}/Bearbeiten", new()
        {
            ["Version"] = auftrag.Version.ToString(),
            ["Felder.Betreff"] = auftrag.Betreff,
            ["Felder.Beschreibung"] = "Das Gerät lädt nicht.",
        });
        auftrag = await LadenAsync(id);
        Assert.Contains(auftrag.Verlauf, v => v.Art == VerlaufArt.Geaendert && v.Text == "Es geht um INV NB-1017.");
        Assert.Equal(["MO-2203", "NB-1018"], await GeraeteAsync(id));

        // Der Verfasser ändert seinen internen Kommentar: Auch das löst die Verknüpfung.
        var kommentar = auftrag.Verlauf.Single(v => v.Art == VerlaufArt.InternerKommentar).Id;
        await anna.PostFormularAsync($"{seite}/Eintrag/{kommentar}", $"{seite}/Eintrag/{kommentar}", new() { ["Text"] = "Doch nicht getauscht." });
        Assert.Equal(["MO-2203"], await GeraeteAsync(id));

        // Der Kunde antwortet und nennt ein Gerät. Seine Seite zeigt die verknüpften Geräte nicht.
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var eigener = await StellenAsync(dieter, "Mein Notebook ist langsam", "Auf dem Aufkleber steht INV NB-1017.");
        Assert.Equal(["NB-1017"], await GeraeteAsync(eigener));
        await dieter.PostFormularAsync($"/Meine/{eigener}", $"/Meine/{eigener}", new() { ["text"] = "Der Monitor INV MO-2203 hängt daran." });
        Assert.Equal(["MO-2203", "NB-1017"], await GeraeteAsync(eigener));
        Assert.DoesNotContain("Monitor 27 Zoll", await dieter.GetStringAsync($"/Meine/{eigener}"));
    }

    [Fact]
    public async Task Ein_ausgemustertes_Geraet_bleibt_am_Auftrag_wird_aber_nicht_neu_verknuepft()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        await anna.PostFormularAsync("/Geraete/Bearbeiten", "/Geraete/Bearbeiten", new()
        {
            ["Eingabe.Inventarnummer"] = "PC-0001",
            ["Eingabe.Bezeichnung"] = "Alter Arbeitsplatzrechner",
            ["Eingabe.Aktiv"] = "true",
        });
        var id = await ErfassenAsync(anna, "Alter Rechner piept", "INV PC-0001 piept beim Start.");
        await app.DatenbankAsync(db => db.Geraete.Where(g => g.Inventarnummer == "PC-0001").ExecuteUpdateAsync(g => g.SetProperty(x => x.Aktiv, false)));

        // Am bestehenden Auftrag bleibt es, auch wenn weiter geschrieben wird, und ist als ausgemustert markiert.
        await anna.UebernehmenAsync(app, id);
        await AktionAsync(anna, id, "InternerKommentar", "Netzteil geprüft.");
        Assert.Equal(["PC-0001"], await GeraeteAsync(id));
        Assert.Matches("PC-0001</a>(?s:.*?)ausgemustert", await anna.GetStringAsync($"/Auftraege/{id}"));

        // Ein neuer Auftrag, der es nennt, wird erfasst, aber nicht mit ihm verknüpft.
        Assert.Empty(await GeraeteAsync(await ErfassenAsync(anna, "Noch ein alter Rechner", "Wieder INV PC-0001.")));
        Assert.Contains("ausgemustert", await anna.GetStringAsync("/Geraete?q=pc-0001"));
    }

    [Fact]
    public async Task Ein_Geraet_aus_Snipe_IT_fuehrt_zu_seiner_Seite_dort()
    {
        // TB-3001 kommt aus Snipe-IT und hat dort die Kennung 9001, NB-1017 ist in AVIS angelegt.
        var tablet = await GeraetIdAsync("TB-3001");
        var notebook = await GeraetIdAsync("NB-1017");
        using var mitAdresse = app.WithWebHostBuilder(b => b.UseSetting("Geraeteabgleich:Adresse", "https://inventar.example.org/snipe/"));
        var anna = mitAdresse.CreateClient();
        await anna.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = "anna" });

        Assert.Contains("<a href=\"https://inventar.example.org/snipe/hardware/9001\" rel=\"noopener noreferrer\">In Snipe-IT ansehen</a>", await anna.GetStringAsync($"/Geraete/{tablet}"));
        Assert.DoesNotContain("In Snipe-IT ansehen", await anna.GetStringAsync($"/Geraete/{notebook}"));

        // Ohne eingestellte Adresse gibt es keinen Link.
        var ohne = await app.AnmeldenAlsAsync("anna");
        Assert.DoesNotContain("In Snipe-IT ansehen", await ohne.GetStringAsync($"/Geraete/{tablet}"));
    }

    [Theory]
    [InlineData("SnipeIt", "9001", "https://inventar.example.org", "https://inventar.example.org/hardware/9001")]
    [InlineData("SnipeIt", "9001", " https://inventar.example.org/snipe/ ", "https://inventar.example.org/snipe/hardware/9001")]
    [InlineData("SnipeIt", "9001", "inventar.example.org", null)]
    [InlineData("SnipeIt", "9001", "", null)]
    [InlineData("SnipeIt", null, "https://inventar.example.org", null)]
    [InlineData(null, null, "https://inventar.example.org", null)]
    public void Die_Adresse_in_Snipe_IT_entsteht_aus_Einstellung_und_Kennung(string? quelle, string? kennung, string adresse, string? erwartet)
    {
        var geraet = new Inventargeraet { Inventarnummer = "X-1", Bezeichnung = "Testgerät", Quelle = quelle, ExterneKennung = kennung };

        Assert.Equal(erwartet, Avis.Web.Pages.Geraete.DetailsModel.AdresseInSnipeIt(geraet, adresse));
    }

    private async Task<int> ErfassenAsync(HttpClient client, string betreff, string beschreibung)
    {
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = beschreibung,
            ["Eingabe.GruppeId"] = (await HotlineAsync()).ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private static async Task<int> StellenAsync(HttpClient client, string betreff, string beschreibung)
    {
        var antwort = await client.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = betreff,
            ["Eingabe.Beschreibung"] = beschreibung,
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private async Task AktionAsync(HttpClient client, int id, string aktion, string text)
    {
        var version = (await LadenAsync(id)).Version;
        var antwort = await client.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler={aktion}", new() { ["version"] = version.ToString(), ["text"] = text });
        Assert.DoesNotContain("meldung fehler", await antwort.Content.ReadAsStringAsync());
    }

    /// <summary>Die Inventarnummern der Geräte, die mit dem Auftrag verknüpft sind.</summary>
    private Task<List<string>> GeraeteAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .Where(a => a.Id == id)
        .SelectMany(a => a.Geraete)
        .Select(g => g.Inventarnummer)
        .OrderBy(n => n)
        .ToListAsync());

    private Task<int> GeraetIdAsync(string inventarnummer) =>
        app.DatenbankAsync(db => db.Geraete.Where(g => g.Inventarnummer == inventarnummer).Select(g => g.Id).SingleAsync());

    private Task<int> HotlineAsync() =>
        app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync());

    private Task<int> KundeIdAsync(string name) =>
        app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == name).Select(k => k.Id).FirstAsync());

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
