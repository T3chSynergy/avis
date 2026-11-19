using System.Globalization;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Abgleich;

/// <summary>
/// Wann ein Abgleich läuft: einmal am Tag zu einer festen Uhrzeit, in der
/// Zeitzone der Anwendung. Eine externe Quelle soll nicht öfter als nötig
/// ganz gelesen werden, und nicht bei jedem Start der Anwendung.
/// </summary>
public static class Abgleichplan
{
    /// <summary>Nach einem gescheiterten Lauf wird frühestens nach dieser Zeit noch einmal versucht.</summary>
    public static readonly TimeSpan Wiederholung = TimeSpan.FromHours(1);

    /// <summary>
    /// Ob ein Lauf abgebrochen wurde, weil es jemand verlangt hat, etwa beim
    /// Beenden der Anwendung. Eine Zeitüberschreitung der Quelle sieht für
    /// .NET genauso aus, ist aber ein Fehler wie jeder andere: Sie wird
    /// festgehalten, und der nächste Versuch folgt.
    /// </summary>
    public static bool Abgebrochen(Exception ausnahme, CancellationToken abbruch) =>
        ausnahme is OperationCanceledException && abbruch.IsCancellationRequested;

    /// <summary>Liest eine Uhrzeit in der Form "02:30". Der Name der Einstellung steht in der Fehlermeldung.</summary>
    public static TimeOnly Uhrzeit(string? text, string einstellung) =>
        TimeOnly.TryParseExact(text?.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var uhrzeit)
            ? uhrzeit
            : throw new InvalidOperationException($"{einstellung} muss eine Uhrzeit in der Form 02:30 sein, nicht \"{text}\".");

    /// <summary>
    /// Der Zeitpunkt in UTC, zu dem der Lauf zuletzt fällig wurde: heute zur
    /// Uhrzeit, oder gestern, solange sie heute noch nicht erreicht ist.
    /// </summary>
    public static DateTime FaelligSeit(DateTime jetztUtc, TimeOnly uhrzeit, TimeZoneInfo zone)
    {
        var ort = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(jetztUtc, DateTimeKind.Utc), zone);
        var tag = DateOnly.FromDateTime(ort);
        if (TimeOnly.FromDateTime(ort) < uhrzeit)
            tag = tag.AddDays(-1);

        // In der Nacht der Umstellung auf Sommerzeit gibt es eine Stunde nicht. Dann gilt die Stunde danach.
        var lokal = tag.ToDateTime(uhrzeit);
        if (zone.IsInvalidTime(lokal))
            lokal = lokal.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(lokal, zone);
    }

    /// <summary>
    /// Ob der Lauf jetzt ansteht: Seit der fälligen Uhrzeit gab es keinen
    /// gelungenen Lauf, und der letzte Versuch ist nicht gerade erst gescheitert.
    /// Wurde noch nie abgeglichen, steht der Lauf sofort an.
    /// </summary>
    public static bool Faellig(DateTime jetztUtc, TimeOnly uhrzeit, TimeZoneInfo zone, DateTime? letzterErfolg, DateTime? letzterVersuch)
    {
        var seit = FaelligSeit(jetztUtc, uhrzeit, zone);
        if (letzterErfolg >= seit)
            return false;
        return letzterVersuch is null || letzterVersuch < seit || jetztUtc - letzterVersuch >= Wiederholung;
    }
}

/// <summary>
/// Sorgt dafür, dass derselbe Abgleich nicht zweimal gleichzeitig läuft, etwa von Hand aus der Verwaltung, während
/// der geplante Lauf noch arbeitet. Zwei Läufe sähen beide denselben Stand und legten dieselben Einträge doppelt an.
/// </summary>
public sealed class Abgleichsperre
{
    public const string LaeuftBereits = "Dieser Abgleich läuft gerade schon. Sein Ergebnis steht hier, sobald er fertig ist.";

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _sperren = new();

    /// <summary>Belegt den Abgleich dieser Art bis zum Dispose. Null, wenn er gerade läuft.</summary>
    public IDisposable? Belegen(string art)
    {
        var sperre = _sperren.GetOrAdd(art, _ => new SemaphoreSlim(1, 1));
        return sperre.Wait(0) ? new Freigabe(sperre) : null;
    }

    private sealed class Freigabe(SemaphoreSlim sperre) : IDisposable
    {
        private bool _frei;

        public void Dispose()
        {
            if (_frei)
                return;
            _frei = true;
            sperre.Release();
        }
    }
}

/// <summary>Hält den letzten Lauf jedes Abgleichs in der Datenbank fest und sagt, ob der tägliche Lauf ansteht.</summary>
public class Abgleichprotokoll(AvisDb db, TimeProvider uhr, Ortszeit ortszeit)
{
    /// <summary>Mehr Hinweise werden nicht aufbewahrt; die Zahl der übrigen steht in der letzten Zeile.</summary>
    public const int Hoechstzahl = 200;

    public async Task<Abgleichergebnis?> LetztesAsync(string art, CancellationToken abbruch = default)
    {
        var lauf = await db.Abgleichlaeufe.AsNoTracking().FirstOrDefaultAsync(l => l.Art == art, abbruch);
        if (lauf is null)
            return null;

        var hinweise = string.IsNullOrEmpty(lauf.Hinweise) ? [] : lauf.Hinweise.Split('\n');
        return new(lauf.Zeitpunkt, lauf.Gelesen, lauf.Neu, lauf.Geaendert, lauf.Reaktiviert, lauf.Inaktiv, hinweise, lauf.Fehler);
    }

    public async Task FesthaltenAsync(string art, Abgleichergebnis ergebnis, CancellationToken abbruch = default)
    {
        var lauf = await db.Abgleichlaeufe.FirstOrDefaultAsync(l => l.Art == art, abbruch);
        if (lauf is null)
        {
            lauf = new Abgleichlauf { Art = art };
            db.Abgleichlaeufe.Add(lauf);
        }

        var hinweise = (ergebnis.Hinweise ?? []).Select(h => h.ReplaceLineEndings(" ")).ToList();
        if (hinweise.Count > Hoechstzahl)
            hinweise = [.. hinweise.Take(Hoechstzahl), $"… und {hinweise.Count - Hoechstzahl} weitere Hinweise."];

        lauf.Zeitpunkt = ergebnis.Zeitpunkt;
        if (ergebnis.Fehler is null)
            lauf.LetzterErfolg = ergebnis.Zeitpunkt;
        lauf.Gelesen = ergebnis.Gelesen;
        lauf.Neu = ergebnis.Neu;
        lauf.Geaendert = ergebnis.Geaendert;
        lauf.Reaktiviert = ergebnis.Reaktiviert;
        lauf.Inaktiv = ergebnis.Inaktiv;
        lauf.Fehler = ergebnis.Fehler;
        lauf.Hinweise = hinweise.Count == 0 ? null : string.Join('\n', hinweise);
        await db.SaveChangesAsync(abbruch);
    }

    public async Task<bool> FaelligAsync(string art, TimeOnly uhrzeit, CancellationToken abbruch = default)
    {
        var lauf = await db.Abgleichlaeufe.AsNoTracking().FirstOrDefaultAsync(l => l.Art == art, abbruch);
        return Abgleichplan.Faellig(uhr.GetUtcNow().UtcDateTime, uhrzeit, ortszeit.Zone, lauf?.LetzterErfolg, lauf?.Zeitpunkt);
    }
}

/// <summary>
/// Sieht alle paar Minuten nach, ob ein täglicher Abgleich ansteht, und führt
/// ihn aus. Erst die Bearbeiter, dann die Kunden, dann die Geräte: Der Nutzer
/// eines Geräts wird über die Adresse eines Kunden gefunden. Lief AVIS zur eingestellten
/// Uhrzeit nicht, wird der Lauf nachgeholt, sobald es wieder läuft.
/// </summary>
public class Abgleichhintergrund(
    IServiceScopeFactory scopes,
    IOptions<BearbeiterabgleichOptionen> bearbeiter,
    IOptions<KundenabgleichOptionen> kunden,
    IOptions<GeraeteabgleichOptionen> geraete,
    ILogger<Abgleichhintergrund> log) : BackgroundService
{
    public static readonly TimeSpan Takt = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken abbruch)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), abbruch);
            using var takt = new PeriodicTimer(Takt);
            do
            {
                await FaelligeAusfuehrenAsync(abbruch);
            }
            while (await takt.WaitForNextTickAsync(abbruch));
        }
        catch (OperationCanceledException) when (abbruch.IsCancellationRequested)
        {
            // Die Anwendung wird beendet.
        }
    }

    /// <summary>Führt die Abgleiche aus, die anstehen. Ein Fehler in einem hält den anderen nicht auf.</summary>
    public async Task FaelligeAusfuehrenAsync(CancellationToken abbruch = default)
    {
        await VersuchenAsync("Bearbeiterabgleich", async dienste =>
        {
            var abgleich = dienste.GetRequiredService<Bearbeiterabgleich>();
            if (abgleich.Eingerichtet && await dienste.GetRequiredService<Abgleichprotokoll>().FaelligAsync(Abgleichlauf.Bearbeiter, bearbeiter.Value.Laufzeit, abbruch))
                await abgleich.AusfuehrenAsync(abbruch);
        }, abbruch);
        await VersuchenAsync("Kundenabgleich", async dienste =>
        {
            var abgleich = dienste.GetRequiredService<Kundenabgleich>();
            if (abgleich.Eingerichtet && await dienste.GetRequiredService<Abgleichprotokoll>().FaelligAsync(Abgleichlauf.Kunden, kunden.Value.Laufzeit, abbruch))
                await abgleich.AusfuehrenAsync(abbruch);
        }, abbruch);
        await VersuchenAsync("Geräteabgleich", async dienste =>
        {
            var abgleich = dienste.GetRequiredService<Geraeteabgleich>();
            if (abgleich.Eingerichtet && await dienste.GetRequiredService<Abgleichprotokoll>().FaelligAsync(Abgleichlauf.Geraete, geraete.Value.Laufzeit, abbruch))
                await abgleich.AusfuehrenAsync(abbruch);
        }, abbruch);
    }

    private async Task VersuchenAsync(string name, Func<IServiceProvider, Task> ausfuehren, CancellationToken abbruch)
    {
        try
        {
            // Je Abgleich eine eigene Verbindung zur Datenbank: Jeder lädt seine ganze Tabelle.
            using var scope = scopes.CreateScope();
            await ausfuehren(scope.ServiceProvider);
        }
        catch (Exception ex) when (!Abgleichplan.Abgebrochen(ex, abbruch))
        {
            log.LogError(ex, "Der {Abgleich} im Hintergrund ist auf einen Fehler gelaufen.", name);
        }
    }
}
