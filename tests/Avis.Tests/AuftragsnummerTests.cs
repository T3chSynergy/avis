using Avis.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avis.Tests;

/// <summary>
/// Die Einstellung Auftraege:ErsteNummer wirkt beim Start. Jeder Abschnitt
/// startet die Anwendung deshalb neu, auf derselben Datenbank.
/// </summary>
public class AuftragsnummerTests
{
    [Fact]
    public async Task Die_Zaehlung_schliesst_an_die_eingestellte_Nummer_an_und_geht_nie_zurueck()
    {
        using var basis = new AvisAnwendung();

        // Leere Datenbank: Schon die Aufträge der Testdaten zählen ab der Nummer.
        using (var app = Mit(basis, "200000"))
        {
            var nummern = await NummernAsync(app);
            Assert.Equal(Enumerable.Range(200000, nummern.Count), nummern);
            Assert.Equal(200000 + nummern.Count, await ErfassenAsync(app));
        }

        // Eine kleinere Nummer und gar keine ändern nichts mehr.
        int letzte;
        using (var app = Mit(basis, "1000"))
            letzte = await ErfassenAsync(app);
        using (var app = Mit(basis, null))
            Assert.Equal(letzte + 1, await ErfassenAsync(app));
        Assert.True(letzte > 200000);

        // Eine größere hebt die Zählung an, auch wenn es schon Aufträge gibt, und nur einmal.
        using (var app = Mit(basis, "300000"))
            Assert.Equal(300000, await ErfassenAsync(app));
        using (var app = Mit(basis, "300000"))
            Assert.Equal(300001, await ErfassenAsync(app));
    }

    private static WebApplicationFactory<Program> Mit(AvisAnwendung basis, string? ersteNummer) =>
        basis.WithWebHostBuilder(b =>
        {
            if (ersteNummer is not null)
                b.UseSetting("Auftraege:ErsteNummer", ersteNummer);
        });

    private static async Task<List<int>> NummernAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AvisDb>().Auftraege.OrderBy(a => a.Id).Select(a => a.Id).ToListAsync();
    }

    private static async Task<int> ErfassenAsync(WebApplicationFactory<Program> app)
    {
        var anna = app.CreateClient();
        await anna.PostFormularAsync("/Anmeldung", "/Anmeldung", new() { ["anmeldename"] = "anna" });
        int hotline;
        using (var scope = app.Services.CreateScope())
            hotline = await scope.ServiceProvider.GetRequiredService<AvisDb>().Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync();

        var antwort = await anna.PostFormularAsync("/Auftraege/Neu", "/Auftraege/Neu", new()
        {
            ["Eingabe.KundeName"] = "Test Kunde",
            ["Eingabe.Felder.Betreff"] = "Nummernprüfung",
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = hotline.ToString(),
        });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }
}
