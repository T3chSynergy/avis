using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

/// <summary>
/// Ein Auftrag nennt einen anderen mit "AVIS" und dessen Nummer. Daraus
/// entsteht ein Verweis in beide Richtungen, mehr nicht. Clara ist Mitglied
/// der gesperrten Gruppe, Bernd nicht.
/// </summary>
public class AuftragsbezugTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    [Theory]
    [InlineData("Folgeauftrag zu AVIS 123.", "123")]
    [InlineData("AVIS-124, AVIS#125 und avis 126; AVIS:127, AVIS128 ohne Abstand", "124|125|126|127|128")]
    [InlineData("AW: [AVIS-129] Drucker druckt nicht", "129")]
    [InlineData("**AVIS 130** in fetter Schrift\nund am Zeilenende AVIS 131", "130|131")]
    [InlineData("AVIS 0.1.0 und AVIS 2,5 sind keine Aufträge, AVIS 0 und AVIS 12a auch nicht.", "")]
    [InlineData("MAVIS 5, AVISO 6 und das Wort AVIS allein sind nicht gemeint.", "")]
    [InlineData("AVIS 1234567890 ist zu lang.", "")]
    public void Auftraege_werden_mit_AVIS_und_ihrer_Nummer_genannt(string text, string erwartet)
    {
        var nummern = Auftragsbezug.Nummern([text, null, ""]);

        Assert.Equal(erwartet.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Order(), nummern.Order());
    }

    [Fact]
    public async Task Wer_einen_Auftrag_im_Text_nennt_verbindet_beide()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var erster = await ErfassenAsync(anna, "Arbeitsplatz für neue Kollegin", "Bitte alles vorbereiten.");

        // Die unbekannte Nummer bleibt Text und stört nicht. Sich selbst nennt ein Auftrag nicht.
        var zweiter = await ErfassenAsync(anna, "Monitor bestellen", $"Gehört zu AVIS {erster}. AVIS 999999 kennt niemand.");
        await anna.UebernehmenAsync(app, zweiter);
        await AktionAsync(anna, zweiter, "InternerKommentar", $"Das hier ist AVIS {zweiter}.");

        Assert.Equal([erster], await BezuegeAsync(zweiter));
        Assert.Empty(await BezuegeAsync(erster));

        // Am nennenden Auftrag steht der genannte, am genannten steht, wer ihn nennt.
        var seiteZweiter = await anna.GetStringAsync($"/Auftraege/{zweiter}");
        Assert.Matches($"<a href=\"/Auftraege/{erster}\">Auftrag {erster}</a> · Arbeitsplatz für neue Kollegin · <span class=\"status\">offen</span>", seiteZweiter);
        Assert.DoesNotContain("nennt diesen", seiteZweiter);
        var seiteErster = await anna.GetStringAsync($"/Auftraege/{erster}");
        Assert.Matches($"<a href=\"/Auftraege/{zweiter}\">Auftrag {zweiter}</a> · Monitor bestellen · <span class=\"status\">in Bearbeitung</span>\\s*<span class=\"hinweis\">nennt diesen</span>", seiteErster);

        // Auch der Betreff zählt, zum Beispiel aus einer weitergeleiteten Mail.
        var dritter = await ErfassenAsync(anna, $"WG: [AVIS-{erster}] Arbeitsplatz für neue Kollegin", "Für einen Test.");
        Assert.Equal([erster], await BezuegeAsync(dritter));
    }

    [Fact]
    public async Task Spaetere_Texte_verbinden_weitere_Auftraege_und_gestrichene_Nennungen_loesen_sie()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var netz = await ErfassenAsync(anna, "Netzwerkdose schalten", "Raum A 105.");
        var kauf = await ErfassenAsync(anna, "Dockingstation beschaffen", "Für Raum A 105.");
        var id = await ErfassenAsync(anna, "Umzug in Raum A 105", $"Die Dose läuft über AVIS {netz}.");
        await anna.UebernehmenAsync(app, id);
        var seite = $"/Auftraege/{id}";

        await AktionAsync(anna, id, "InternerKommentar", $"Bestellt mit AVIS-{kauf}.");
        Assert.Equal([netz, kauf], await BezuegeAsync(id));

        // Die Beschreibung nennt den ersten Auftrag nicht mehr: Der Bezug ist gelöst.
        var auftrag = await LadenAsync(id);
        await anna.PostFormularAsync($"{seite}/Bearbeiten", $"{seite}/Bearbeiten", new()
        {
            ["Version"] = auftrag.Version.ToString(),
            ["Felder.Betreff"] = auftrag.Betreff,
            ["Felder.Beschreibung"] = "Die Dose ist schon geschaltet.",
        });
        Assert.Equal([kauf], await BezuegeAsync(id));
        Assert.DoesNotContain("Umzug in Raum A 105", await anna.GetStringAsync($"/Auftraege/{netz}"));

        // Der Verfasser ändert seinen internen Kommentar: Auch das löst den Bezug.
        var kommentar = (await LadenAsync(id)).Verlauf.Single(v => v.Art == VerlaufArt.InternerKommentar).Id;
        await anna.PostFormularAsync($"{seite}/Eintrag/{kommentar}", $"{seite}/Eintrag/{kommentar}", new() { ["Text"] = "Doch selbst gekauft." });
        Assert.Empty(await BezuegeAsync(id));
    }

    [Fact]
    public async Task Der_Bezug_aendert_nichts_am_Stand_der_Auftraege()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var erster = await ErfassenAsync(anna, "Hauptsache", "Für einen Test.");
        var zweiter = await ErfassenAsync(anna, "Nebensache", $"Siehe AVIS {erster}.");

        // Der genannte Auftrag lässt sich abschließen, während der andere offen bleibt, und umgekehrt.
        await anna.UebernehmenAsync(app, erster);
        await AktionAsync(anna, erster, "Abschliessen", "Erledigt.");

        Assert.Equal(AuftragStatus.Abgeschlossen, (await LadenAsync(erster)).Status);
        Assert.Equal(AuftragStatus.Offen, (await LadenAsync(zweiter)).Status);
        Assert.Contains("<span class=\"status\">abgeschlossen</span>", await anna.GetStringAsync($"/Auftraege/{zweiter}"));
    }

    [Fact]
    public async Task Ein_Auftrag_einer_gesperrten_Gruppe_erscheint_nur_bei_dem_der_ihn_sehen_darf()
    {
        var gesperrt = await GesperrteGruppeAsync("Vertrauliches");
        var clara = await app.AnmeldenAlsAsync("clara");
        var bernd = await app.AnmeldenAlsAsync("bernd");
        var offen = await ErfassenAsync(bernd, "Notebook für die Leitung", "Für einen Test.");
        var geheim = await ErfassenAsync(clara, "GEHEIME-PERSONALSACHE", $"Betrifft auch AVIS {offen}.", gesperrt);
        var nennend = await ErfassenAsync(bernd, "Rückfrage zum Notebook", $"Angeblich gibt es dazu AVIS {geheim}.");

        // Clara sieht den vertraulichen Auftrag an beiden anderen, Bernd an keinem.
        Assert.Contains("GEHEIME-PERSONALSACHE", await clara.GetStringAsync($"/Auftraege/{offen}"));
        Assert.Contains("GEHEIME-PERSONALSACHE", await clara.GetStringAsync($"/Auftraege/{nennend}"));
        Assert.DoesNotContain("GEHEIME-PERSONALSACHE", await bernd.GetStringAsync($"/Auftraege/{offen}"));
        var seite = await bernd.GetStringAsync($"/Auftraege/{nennend}");
        Assert.DoesNotContain("GEHEIME-PERSONALSACHE", seite);
        Assert.DoesNotContain($"/Auftraege/{geheim}\"", seite);
    }

    [Fact]
    public async Task Der_Kunde_sieht_die_verbundenen_Auftraege_nicht()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var intern = await ErfassenAsync(anna, "INTERNE-VORARBEIT", "Für einen Test.");
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var antwort = await dieter.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = "Wie weit ist es?",
            ["Eingabe.Beschreibung"] = $"Am Telefon hieß es AVIS {intern}.",
        });
        var eigener = int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);

        // Auch die Nennung eines Kunden verbindet, aber nur die Bearbeiter sehen es.
        Assert.Equal([intern], await BezuegeAsync(eigener));
        Assert.DoesNotContain("INTERNE-VORARBEIT", await dieter.GetStringAsync($"/Meine/{eigener}"));
        Assert.Contains("INTERNE-VORARBEIT", await anna.GetStringAsync($"/Auftraege/{eigener}"));
    }

    private async Task<int> ErfassenAsync(HttpClient client, string betreff, string beschreibung, int? gruppe = null)
    {
        var antwort = await client.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = beschreibung,
            ["Eingabe.GruppeId"] = (gruppe ?? await HotlineAsync()).ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private async Task AktionAsync(HttpClient client, int id, string aktion, string text)
    {
        var version = (await LadenAsync(id)).Version;
        var antwort = await client.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler={aktion}", new() { ["version"] = version.ToString(), ["text"] = text });
        Assert.DoesNotContain("meldung fehler", await antwort.Content.ReadAsStringAsync());
    }

    private async Task<int> GesperrteGruppeAsync(string name)
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        await anna.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Gruppen", new() { ["name"] = name });
        var id = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == name).Select(g => g.Id).FirstAsync());
        var seite = $"/Verwaltung/Gruppen/{id}";
        await anna.PostFormularAsync(seite, $"{seite}?handler=Speichern", new() { ["name"] = name, ["aktiv"] = "true", ["gesperrt"] = "true" });
        var clara = await app.DatenbankAsync(db => db.Benutzer.Where(b => b.Anmeldename == "clara").Select(b => b.Id).FirstAsync());
        await anna.PostFormularAsync(seite, $"{seite}?handler=MitgliedHinzufuegen", new() { ["benutzerId"] = clara.ToString() });
        Assert.True(await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Id == id).Select(g => g.Gesperrt).FirstAsync()));
        return id;
    }

    /// <summary>Die Nummern der Aufträge, die dieser Auftrag nennt.</summary>
    private Task<List<int>> BezuegeAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .Where(a => a.Id == id)
        .SelectMany(a => a.Bezuege)
        .Select(b => b.Id)
        .OrderBy(n => n)
        .ToListAsync());

    private Task<int> HotlineAsync() =>
        app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync());

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
