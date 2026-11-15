using Avis.Web.Abgleich;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avis.Tests;

/// <summary>
/// Tests, die Zeit messen, laufen nicht neben anderen: Sonst messen sie, wie
/// beschäftigt der Rechner gerade ist, und scheitern ohne Grund.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class Zeitmessung
{
    public const string Name = "Zeitmessung";
}

[Collection(Zeitmessung.Name)]
public class ZeitmessungTests
{
    [Fact]
    public async Task Auch_sehr_viele_Geraete_gleicht_ein_Lauf_zuegig_ab()
    {
        using var app = new AvisAnwendung();
        await app.DatenbankAsync(db => db.Geraete.Where(g => g.Quelle != null).ExecuteDeleteAsync());
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

    private static async Task<Abgleichergebnis> AbgleichenAsync(AvisAnwendung app)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Geraeteabgleich>().AusfuehrenAsync();
    }
}
