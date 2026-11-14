using System.Net;
using Avis.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

/// <summary>
/// Die Seite der Testanmeldung leert auf Wunsch die Datenbank. Jeder Test
/// hat deshalb seine eigene Anwendung mit eigener Datenbank.
/// </summary>
public class TestanmeldungTests
{
    [Fact]
    public async Task Die_Testanmeldung_nennt_den_Ordner_in_dem_die_Mails_liegen()
    {
        using var app = new AvisAnwendung();

        var seite = WebUtility.HtmlDecode(await app.CreateClient().GetStringAsync("/Anmeldung"));

        Assert.Contains("Mails werden nicht versendet, sondern als Dateien abgelegt.", seite);
        Assert.Contains(Path.Combine(Path.GetDirectoryName(app.Anhangordner)!, "Mails"), seite);
    }

    [Fact]
    public async Task Die_Datenbank_laesst_sich_leeren_und_mit_Testdaten_neu_anlegen()
    {
        using var app = new AvisAnwendung();
        var anna = await app.AnmeldenAlsAsync("anna");
        var frisch = await StandAsync(app);

        // Etwas Eigenes: ein Auftrag für einen neuen Kunden mit Anhang und ein neues Konto.
        await anna.PostMitDateienAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Wird gelöscht",
            ["Eingabe.Felder.Betreff"] = "Verschwindet gleich",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync())).ToString(),
        }, ("notiz.txt", "Inhalt"u8.ToArray()));
        await app.AnmeldenAlsNeuAsync("gast");
        Assert.Equal(1, app.DateienInDerAblage());
        Assert.NotEqual(frisch, await StandAsync(app));

        // Ohne Bestätigung bleibt alles, wie es ist.
        var ohne = await anna.PostFormularAsync("/Anmeldung", "/Anmeldung?handler=NeuAnlegen", []);
        Assert.Contains("Bitte bestätige, dass alle Daten gelöscht werden sollen.", await ohne.Content.ReadAsStringAsync());
        Assert.True(await app.DatenbankAsync(db => db.Auftraege.AnyAsync(a => a.Betreff == "Verschwindet gleich")));

        var antwort = await anna.PostFormularAsync("/Anmeldung", "/Anmeldung?handler=NeuAnlegen", new() { ["bestaetigt"] = "true" });

        // Wie nach dem ersten Start: nur die Testdaten, die Zählung beginnt von vorn, keine Datei bleibt liegen.
        Assert.Contains("Die Datenbank wurde geleert und mit Testdaten neu angelegt.", await antwort.Content.ReadAsStringAsync());
        Assert.Equal(frisch, await StandAsync(app));
        Assert.Equal(0, app.DateienInDerAblage());
        Assert.False(await app.DatenbankAsync(db => db.Kunden.AnyAsync(k => k.Name == "Wird gelöscht")));
        Assert.False(await app.DatenbankAsync(db => db.Benutzer.AnyAsync(b => b.Anmeldename == "gast")));

        // Anna ist abgemeldet und kann sich wieder anmelden und weiterarbeiten.
        Assert.Contains("Testanmeldung", await anna.GetStringAsync("/Auftraege"));
        anna = await app.AnmeldenAlsAsync("anna");
        Assert.Contains("Drucker im Flur druckt nicht", await anna.GetStringAsync("/Auftraege?status=alle"));
    }

    [Fact]
    public async Task Ohne_Testdaten_gibt_es_das_Neuanlegen_nicht()
    {
        using var basis = new AvisAnwendung();
        using var app = basis.WithWebHostBuilder(b => b.UseSetting("Testdaten:Anlegen", "false"));
        var client = app.CreateClient();

        Assert.DoesNotContain("Datenbank neu anlegen", await client.GetStringAsync("/Anmeldung"));
        var antwort = await client.PostFormularAsync("/Anmeldung", "/Anmeldung?handler=NeuAnlegen", new() { ["bestaetigt"] = "true" });
        Assert.Equal(HttpStatusCode.NotFound, antwort.StatusCode);
    }

    /// <summary>Was in der Datenbank steht, mit den Nummern der Aufträge.</summary>
    private static Task<string> StandAsync(AvisAnwendung app) => app.DatenbankAsync(async db => string.Join(" | ",
        "Aufträge " + string.Join(",", await db.Auftraege.OrderBy(a => a.Id).Select(a => a.Id).ToListAsync()),
        "Verlauf " + await db.Verlauf.CountAsync(),
        "Kunden " + await db.Kunden.CountAsync(),
        "Benutzer " + await db.Benutzer.CountAsync(),
        "Gruppen " + await db.Gruppen.CountAsync(),
        "Geräte " + await db.Geraete.CountAsync(),
        "Anhänge " + await db.Anhaenge.CountAsync(),
        "Benachrichtigungen " + await db.Benachrichtigungen.CountAsync()));
}
