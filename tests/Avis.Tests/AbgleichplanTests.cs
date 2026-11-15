using Avis.Web.Abgleich;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Avis.Tests;

/// <summary>
/// Die Abgleiche laufen einmal am Tag zu einer festen Uhrzeit. Die Zeiten in
/// diesen Tests sind UTC; in Berlin ist es im November eine Stunde später.
/// </summary>
public class AbgleichplanTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly TimeOnly HalbDrei = new(2, 30);

    private static DateTime Utc(int monat, int tag, int stunde, int minute) =>
        new(2026, monat, tag, stunde, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Faellig_wird_der_Lauf_zur_Uhrzeit_in_der_Zeitzone_der_Anwendung()
    {
        // 02:30 in Berlin ist im November 01:30 UTC. Kurz davor gilt noch der Lauf von gestern.
        Assert.Equal(Utc(11, 14, 1, 30), Abgleichplan.FaelligSeit(Utc(11, 15, 1, 29), HalbDrei, Berlin));
        Assert.Equal(Utc(11, 15, 1, 30), Abgleichplan.FaelligSeit(Utc(11, 15, 1, 30), HalbDrei, Berlin));
        Assert.Equal(Utc(11, 15, 1, 30), Abgleichplan.FaelligSeit(Utc(11, 15, 23, 0), HalbDrei, Berlin));

        // Im Sommer ist Berlin zwei Stunden voraus.
        Assert.Equal(Utc(7, 1, 0, 30), Abgleichplan.FaelligSeit(Utc(7, 1, 12, 0), HalbDrei, Berlin));

        // Am 29. März 2026 springt die Uhr von 2 auf 3: 02:30 gibt es nicht, es gilt 03:30 Sommerzeit.
        Assert.Equal(Utc(3, 29, 1, 30), Abgleichplan.FaelligSeit(Utc(3, 29, 12, 0), HalbDrei, Berlin));
    }

    [Fact]
    public void Ein_Lauf_steht_an_bis_er_seit_der_Uhrzeit_einmal_gelungen_ist()
    {
        var jetzt = Utc(11, 15, 9, 0);
        var faellig = Utc(11, 15, 1, 30);
        bool Faellig(DateTime? erfolg, DateTime? versuch) => Abgleichplan.Faellig(jetzt, HalbDrei, Berlin, erfolg, versuch);

        // Noch nie gelaufen: sofort.
        Assert.True(Faellig(null, null));
        // Gestern gelungen, heute noch nicht: steht an. Heute gelungen, auch von Hand: erledigt bis morgen.
        Assert.True(Faellig(faellig.AddHours(-3), faellig.AddHours(-3)));
        Assert.False(Faellig(faellig.AddMinutes(1), faellig.AddMinutes(1)));
        Assert.False(Faellig(jetzt.AddMinutes(-5), jetzt.AddMinutes(-5)));

        // Heute gescheitert: nicht alle paar Minuten wieder, sondern nach einer Stunde.
        Assert.False(Faellig(faellig.AddHours(-24), jetzt.AddMinutes(-59)));
        Assert.True(Faellig(faellig.AddHours(-24), jetzt.AddMinutes(-60)));
        Assert.True(Faellig(null, jetzt.AddMinutes(-61)));
    }

    [Theory]
    [InlineData("02:30", 2, 30)]
    [InlineData(" 2:05 ", 2, 5)]
    [InlineData("23:59", 23, 59)]
    public void Die_Uhrzeit_steht_als_Stunde_und_Minute_in_den_Einstellungen(string text, int stunde, int minute) =>
        Assert.Equal(new TimeOnly(stunde, minute), Abgleichplan.Uhrzeit(text, "Geraeteabgleich:Uhrzeit"));

    [Theory]
    [InlineData("")]
    [InlineData("nachts")]
    [InlineData("24:00")]
    [InlineData("2.30")]
    public void Eine_unlesbare_Uhrzeit_wird_mit_dem_Namen_der_Einstellung_abgelehnt(string text) =>
        Assert.Contains("Geraeteabgleich:Uhrzeit muss eine Uhrzeit in der Form 02:30 sein",
            Assert.Throws<InvalidOperationException>(() => Abgleichplan.Uhrzeit(text, "Geraeteabgleich:Uhrzeit")).Message);

    [Fact]
    public void Mit_einer_unlesbaren_Uhrzeit_startet_AVIS_nicht()
    {
        using var basis = new AvisAnwendung();
        using var app = basis.WithWebHostBuilder(b => b
            .UseSetting("Geraeteabgleich:Art", "SnipeIt")
            .UseSetting("Geraeteabgleich:Adresse", "https://inventar.example.org")
            .UseSetting("Geraeteabgleich:Schluessel", "geheim")
            .UseSetting("Geraeteabgleich:Uhrzeit", "nachts"));

        Assert.Contains("Geraeteabgleich:Uhrzeit muss eine Uhrzeit in der Form 02:30 sein", Assert.ThrowsAny<Exception>(() => app.CreateClient()).ToString());
    }

    [Fact]
    public async Task Antwortet_eine_Quelle_nicht_rechtzeitig_laeuft_der_Hintergrund_weiter_und_versucht_es_spaeter_wieder()
    {
        var uhr = new Testuhr(Utc(11, 15, 9, 0));
        using var basis = new AvisAnwendung();
        using var app = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(uhr)));
        var hintergrund = ActivatorUtilities.CreateInstance<Abgleichhintergrund>(app.Services);
        basis.Bearbeiterquelle.Bearbeiter.Add(new("guid-berta", @"avis\berta", "Bearbeiterin, Berta", null));
        basis.Kundenquelle.Kunden.Add(new("guid-nora", @"avis\nora", "Neu, Nora", "nora.neu@example.org", null, null, "A 301"));
        basis.Geraetequelle.Geraete.Add(new("501", "NB-5001", "Notebook", "Notebook", "A 301", null));

        // .NET meldet eine Zeitüberschreitung wie einen Abbruch. Früher endete der Hintergrund daran, ohne Meldung,
        // und bis zum nächsten Neustart lief kein Abgleich mehr.
        basis.Geraetequelle.ZuLangsam = true;
        await hintergrund.FaelligeAusfuehrenAsync();

        Assert.Equal((1, 1, 1), (basis.Bearbeiterquelle.Abrufe, basis.Kundenquelle.Abrufe, basis.Geraetequelle.Abrufe));
        var lauf = await DatenbankAsync(app, db => db.Abgleichlaeufe.AsNoTracking().SingleAsync(l => l.Art == Abgleichlauf.Geraete));
        Assert.Contains("konnte nicht gelesen werden", lauf.Fehler);
        Assert.Null(lauf.LetzterErfolg);

        // Nicht gleich wieder, aber nach einer Stunde; dann gelingt es.
        uhr.Jetzt = Utc(11, 15, 9, 30);
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal(1, basis.Geraetequelle.Abrufe);
        basis.Geraetequelle.ZuLangsam = false;
        uhr.Jetzt = Utc(11, 15, 10, 1);
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((1, 1, 2), (basis.Bearbeiterquelle.Abrufe, basis.Kundenquelle.Abrufe, basis.Geraetequelle.Abrufe));
        Assert.Equal(1, await DatenbankAsync(app, db => db.Geraete.CountAsync(g => g.Inventarnummer == "NB-5001")));
    }

    [Fact]
    public async Task Der_Hintergrund_gleicht_einmal_am_Tag_ab_erst_Bearbeiter_dann_Kunden_dann_Geraete()
    {
        var uhr = new Testuhr(Utc(11, 15, 9, 0));
        using var basis = new AvisAnwendung();
        using var app = basis.WithWebHostBuilder(b => b
            .UseSetting("Bearbeiterabgleich:Uhrzeit", "01:30")
            .UseSetting("Kundenabgleich:Uhrzeit", "02:00")
            .UseSetting("Geraeteabgleich:Uhrzeit", "02:30")
            .ConfigureTestServices(s => s.AddSingleton<TimeProvider>(uhr)));
        var hintergrund = ActivatorUtilities.CreateInstance<Abgleichhintergrund>(app.Services);
        var kunden = basis.Kundenquelle;
        var geraete = basis.Geraetequelle;
        var bearbeiter = basis.Bearbeiterquelle;
        bearbeiter.Bearbeiter.Add(new("guid-berta", @"avis\berta", "Bearbeiterin, Berta", null));
        kunden.Kunden.Add(new("guid-nora", @"avis\nora", "Neu, Nora", "nora.neu@example.org", null, null, "A 301"));
        geraete.Geraete.Add(new("501", "NB-5001", "Notebook", "Notebook", "A 301", "nora.neu@example.org"));

        // Noch nie abgeglichen: Der erste Blick holt beides nach. Weil die Kunden zuerst kommen, findet das
        // Gerät seine Nutzerin, die es eben noch nicht gab.
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((1, 1, 1), (bearbeiter.Abrufe, kunden.Abrufe, geraete.Abrufe));
        var geraet = await DatenbankAsync(app, db => db.Geraete.AsNoTracking().Include(g => g.Kunde).SingleAsync(g => g.Inventarnummer == "NB-5001"));
        Assert.Equal("Neu, Nora", geraet.Kunde?.Name);

        // Danach ist für heute Ruhe, auch wenn AVIS neu startet: Der Lauf steht in der Datenbank.
        uhr.Jetzt = Utc(11, 15, 22, 0);
        await hintergrund.FaelligeAusfuehrenAsync();
        await ActivatorUtilities.CreateInstance<Abgleichhintergrund>(app.Services).FaelligeAusfuehrenAsync();
        Assert.Equal((1, 1), (kunden.Abrufe, geraete.Abrufe));

        // In der Nacht: um 01:30 Ortszeit die Bearbeiter, um 02:00 die Kunden, um 02:30 die Geräte, je einmal.
        uhr.Jetzt = Utc(11, 16, 0, 29);
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((1, 1, 1), (bearbeiter.Abrufe, kunden.Abrufe, geraete.Abrufe));
        uhr.Jetzt = Utc(11, 16, 0, 59);
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((2, 1, 1), (bearbeiter.Abrufe, kunden.Abrufe, geraete.Abrufe));
        uhr.Jetzt = Utc(11, 16, 1, 4);
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((2, 1), (kunden.Abrufe, geraete.Abrufe));
        uhr.Jetzt = Utc(11, 16, 1, 34);
        await hintergrund.FaelligeAusfuehrenAsync();
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((2, 2), (kunden.Abrufe, geraete.Abrufe));

        // Ist Snipe-IT in der nächsten Nacht nicht zu erreichen, versucht AVIS es nach einer Stunde wieder
        // und nicht alle fünf Minuten. Die Kunden sind davon nicht betroffen.
        geraete.Gestoert = true;
        uhr.Jetzt = Utc(11, 17, 1, 34);
        await hintergrund.FaelligeAusfuehrenAsync();
        uhr.Jetzt = Utc(11, 17, 1, 39);
        await hintergrund.FaelligeAusfuehrenAsync();
        uhr.Jetzt = Utc(11, 17, 2, 29);
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((3, 3), (kunden.Abrufe, geraete.Abrufe));
        var lauf = await DatenbankAsync(app, db => db.Abgleichlaeufe.AsNoTracking().SingleAsync(l => l.Art == Abgleichlauf.Geraete));
        Assert.Contains("Snipe-IT hat den Zugangsschlüssel abgelehnt.", lauf.Fehler);
        Assert.Equal((Utc(11, 17, 1, 34), Utc(11, 16, 1, 34)), (lauf.Zeitpunkt, lauf.LetzterErfolg!.Value));

        geraete.Gestoert = false;
        uhr.Jetzt = Utc(11, 17, 2, 34);
        await hintergrund.FaelligeAusfuehrenAsync();
        await hintergrund.FaelligeAusfuehrenAsync();
        Assert.Equal((3, 4), (kunden.Abrufe, geraete.Abrufe));
        lauf = await DatenbankAsync(app, db => db.Abgleichlaeufe.AsNoTracking().SingleAsync(l => l.Art == Abgleichlauf.Geraete));
        Assert.Equal((null, Utc(11, 17, 2, 34)), (lauf.Fehler, lauf.LetzterErfolg!.Value));
    }

    [Fact]
    public async Task Ein_Lauf_von_Hand_zaehlt_und_die_Verwaltung_zeigt_ihn_auch_nach_einem_Neustart()
    {
        var uhr = new Testuhr(Utc(11, 15, 9, 0));
        using var basis = new AvisAnwendung();
        using var app = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(uhr)));
        basis.Geraetequelle.Geraete.Add(new("501", "NB-5001", "Notebook", "Notebook", "A 301", null));
        basis.Kundenquelle.Kunden.Add(new("guid-nora", @"avis\nora", "Neu, Nora", "nora.neu@example.org", null, null));

        // Von Hand aus der Verwaltung, hier nur die Geräte.
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Geraeteabgleich>().AusfuehrenAsync();

        // Der Hintergrund holt danach nur noch die Kunden nach.
        await ActivatorUtilities.CreateInstance<Abgleichhintergrund>(app.Services).FaelligeAusfuehrenAsync();
        Assert.Equal((1, 1), (basis.Kundenquelle.Abrufe, basis.Geraetequelle.Abrufe));

        // Das Protokoll liest aus der Datenbank, nicht aus dem Gedächtnis der laufenden Anwendung.
        using (var scope = app.Services.CreateScope())
        {
            var letztes = await scope.ServiceProvider.GetRequiredService<Abgleichprotokoll>().LetztesAsync(Abgleichlauf.Geraete);
            Assert.Equal((Utc(11, 15, 9, 0), 1, 1, null), (letztes!.Zeitpunkt, letztes.Gelesen, letztes.Neu, letztes.Fehler));
            Assert.Empty(letztes.Hinweise!);
        }
    }

    [Fact]
    public async Task Von_sehr_vielen_Hinweisen_bleiben_die_ersten_und_ihre_Zahl()
    {
        using var app = new AvisAnwendung();
        using var scope = app.Services.CreateScope();
        var protokoll = scope.ServiceProvider.GetRequiredService<Abgleichprotokoll>();
        var hinweise = Enumerable.Range(1, 250).Select(i => $"Hinweis {i}\nmit Umbruch").ToList();

        await protokoll.FesthaltenAsync(Abgleichlauf.Kunden, new(Utc(11, 15, 9, 0), Gelesen: 300, Hinweise: hinweise));

        var letztes = await protokoll.LetztesAsync(Abgleichlauf.Kunden);
        Assert.Equal(Abgleichprotokoll.Hoechstzahl + 1, letztes!.Hinweise!.Count);
        Assert.Equal(("Hinweis 1 mit Umbruch", "… und 50 weitere Hinweise."), (letztes.Hinweise[0], letztes.Hinweise[^1]));
    }

    private static async Task<T> DatenbankAsync<T>(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app, Func<AvisDb, Task<T>> abfrage)
    {
        using var scope = app.Services.CreateScope();
        return await abfrage(scope.ServiceProvider.GetRequiredService<AvisDb>());
    }

    /// <summary>Eine Uhr, die der Test stellt.</summary>
    private sealed class Testuhr(DateTime jetzt) : TimeProvider
    {
        public DateTime Jetzt { get; set; } = jetzt;

        public override DateTimeOffset GetUtcNow() => new(Jetzt, TimeSpan.Zero);
    }
}
