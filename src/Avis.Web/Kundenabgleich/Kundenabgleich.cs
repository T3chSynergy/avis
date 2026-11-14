using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Kundenabgleich;

public class KundenabgleichOptionen
{
    public const string Aus = "Aus";
    public const string ActiveDirectory = "ActiveDirectory";

    /// <summary>
    /// "Aus": Kunden werden nur in AVIS gepflegt. "ActiveDirectory": Kunden
    /// sind die Benutzerkonten des Active Directory, auf die der Filter passt.
    /// </summary>
    public string Art { get; set; } = Aus;

    /// <summary>
    /// LDAP-Filter, der bestimmt, wer Kunde ist, zum Beispiel
    /// "(department=*)" oder "(memberOf=CN=AVIS-Kunden,OU=Gruppen,DC=example,DC=org)".
    /// Dass nur aktive Benutzerkonten zählen, ergänzt AVIS selbst.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Wo im Verzeichnis gesucht wird, zum Beispiel "OU=Mitarbeiter,DC=example,DC=org".
    /// Leer: in der ganzen Domäne des Servers.
    /// </summary>
    public string? Suchbasis { get; set; }

    /// <summary>
    /// Der Filter, mit dem im Active Directory gesucht wird: der eingestellte,
    /// eingeschränkt auf Benutzerkonten von Personen, die nicht deaktiviert
    /// sind. Die lange Zahl steht für "Bit gesetzt", die 2 für "deaktiviert".
    /// </summary>
    public string Suchfilter =>
        $"(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2)){Filter?.Trim()})";

    public int AbstandMinuten { get; set; } = 60;
}

/// <summary>Ein Kunde, wie ihn eine externe Quelle kennt.</summary>
/// <param name="Kennung">Bleibt in der Quelle gleich, auch wenn sich Name oder Anmeldename ändern.</param>
public record Quellkunde(string Kennung, string? Anmeldename, string? Name, string? Email, string? Telefon, string? Organisationseinheit);

/// <summary>
/// Liefert alle Kunden einer externen Quelle. Der Abgleich weiß nicht, woher
/// sie stammen: Ein Verzeichnis, eine Datei oder eine Schnittstelle können
/// dieselbe Liste liefern.
/// </summary>
public interface IKundenquelle
{
    /// <summary>Steht als Quelle am Kunden, zum Beispiel "ActiveDirectory".</summary>
    string Name { get; }

    /// <summary>Alle Kunden der Quelle. Wirft eine Ausnahme, wenn sie nicht vollständig gelesen werden konnte.</summary>
    IReadOnlyList<Quellkunde> Lesen();
}

/// <summary>Was ein Abgleich getan hat. Mit einem Fehler wurde nichts geändert.</summary>
public record Abgleichergebnis(
    DateTime Zeitpunkt,
    int Gelesen = 0,
    int Neu = 0,
    int Geaendert = 0,
    int Reaktiviert = 0,
    int Inaktiv = 0,
    IReadOnlyList<string>? Hinweise = null,
    string? Fehler = null);

/// <summary>Merkt sich das Ergebnis des letzten Abgleichs seit dem Start, für die Verwaltung.</summary>
public class Abgleichstand
{
    public Abgleichergebnis? Letztes { get; set; }
}

/// <summary>
/// Gleicht die Kundentabelle mit einer externen Quelle ab. Die Quelle gewinnt:
/// Ihre Angaben überschreiben, was in AVIS steht. Wer in der Quelle fehlt,
/// wird nicht gelöscht, sondern inaktiv. Kunden, die nur in AVIS angelegt
/// wurden, bleiben unberührt.
/// </summary>
public class Kundenabgleich(
    AvisDb db,
    IEnumerable<IKundenquelle> quellen,
    Abgleichstand stand,
    TimeProvider uhr,
    ILogger<Kundenabgleich> log)
{
    private readonly IKundenquelle? _quelle = quellen.FirstOrDefault();

    public bool Eingerichtet => _quelle is not null;

    public async Task<Abgleichergebnis> AusfuehrenAsync(CancellationToken abbruch = default)
    {
        var ergebnis = await AbgleichenAsync(abbruch);
        stand.Letztes = ergebnis;
        if (ergebnis.Fehler is not null)
            log.LogError("Der Kundenabgleich ist gescheitert, es wurde nichts geändert: {Fehler}", ergebnis.Fehler);
        else
            log.LogInformation(
                "Kundenabgleich: {Gelesen} gelesen, {Neu} neu, {Geaendert} geändert, {Reaktiviert} wieder aktiv, {Inaktiv} inaktiv, {Hinweise} Hinweise.",
                ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv, ergebnis.Hinweise?.Count ?? 0);
        return ergebnis;
    }

    private async Task<Abgleichergebnis> AbgleichenAsync(CancellationToken abbruch)
    {
        var jetzt = uhr.GetUtcNow().UtcDateTime;
        if (_quelle is null)
            return new(jetzt, Fehler: "Es ist keine Quelle eingerichtet (Kundenabgleich:Art).");

        List<Quellkunde> eintraege;
        try
        {
            // Dieselbe Kennung zweimal: Der erste Eintrag zählt.
            eintraege = _quelle.Lesen()
                .Where(e => !string.IsNullOrWhiteSpace(e.Kennung))
                .GroupBy(e => e.Kennung.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Die Quelle {Quelle} konnte nicht gelesen werden.", _quelle.Name);
            return new(jetzt, Fehler: $"Die Quelle {_quelle.Name} konnte nicht gelesen werden: {ex.Message}");
        }

        // Eine leere Antwort ist fast immer ein Fehler der Quelle. Ohne diese
        // Bremse wären danach alle Kunden auf einen Schlag inaktiv.
        if (eintraege.Count == 0)
            return new(jetzt, Fehler: $"Die Quelle {_quelle.Name} hat keinen einzigen Kunden geliefert. Damit nicht alle Kunden inaktiv werden, wurde nichts geändert.");

        var kunden = await db.Kunden.ToListAsync(abbruch);
        var hinweise = new List<string>();
        var vergeben = new HashSet<Kunde>();
        int neu = 0, geaendert = 0, reaktiviert = 0, inaktiv = 0;

        foreach (var eintrag in eintraege)
        {
            var kennung = eintrag.Kennung.Trim();
            var anmeldename = Bereinigt(eintrag.Anmeldename, 200)?.ToLowerInvariant();
            var email = Bereinigt(eintrag.Email, 320)?.ToLowerInvariant();

            // Wiedererkannt wird zuerst über die Kennung der Quelle. Wer noch keine hat, aber schon in AVIS
            // steht, weil er sich angemeldet hat oder von Hand angelegt wurde, wird übernommen statt verdoppelt.
            var kunde = kunden.FirstOrDefault(k => k.Quelle == _quelle.Name && string.Equals(k.ExterneKennung, kennung, StringComparison.OrdinalIgnoreCase))
                ?? (anmeldename is null ? null : kunden.FirstOrDefault(k => k.Quelle is null && k.Anmeldename == anmeldename && !vergeben.Contains(k)))
                ?? (email is null ? null : kunden.FirstOrDefault(k => k.Quelle is null && k.Anmeldename is null && k.Email == email && !vergeben.Contains(k)));

            var istNeu = kunde is null;
            if (kunde is null)
            {
                kunde = new Kunde();
                db.Kunden.Add(kunde);
                kunden.Add(kunde);
            }
            vergeben.Add(kunde);

            // Eine Adresse gehört nur einem Kunden, über sie werden Mails zugeordnet.
            var besitzer = email is null ? null : kunden.FirstOrDefault(k => k != kunde && k.Email == email);
            if (besitzer is not null)
            {
                hinweise.Add($"{eintrag.Name ?? anmeldename ?? kennung}: Die Adresse {email} gehört schon zum Kunden {besitzer.Name} und wurde nicht übernommen.");
                email = kunde.Email == email ? null : kunde.Email;
            }

            var vorher = (kunde.Quelle, kunde.ExterneKennung, kunde.Name, kunde.Email, kunde.Telefon, kunde.Organisationseinheit, kunde.Anmeldename);
            var warAktiv = kunde.Aktiv;

            kunde.Quelle = _quelle.Name;
            kunde.ExterneKennung = kennung;
            kunde.Name = Bereinigt(eintrag.Name, 200) ?? anmeldename ?? kennung;
            kunde.Email = email;
            kunde.Telefon = Bereinigt(eintrag.Telefon, 50);
            kunde.Organisationseinheit = Bereinigt(eintrag.Organisationseinheit, 200);
            kunde.Anmeldename = anmeldename;
            kunde.Aktiv = true;

            if (istNeu)
                neu++;
            else if (!warAktiv)
                reaktiviert++;
            else if (vorher != (kunde.Quelle, kunde.ExterneKennung, kunde.Name, kunde.Email, kunde.Telefon, kunde.Organisationseinheit, kunde.Anmeldename))
                geaendert++;
        }

        // Wer aus dieser Quelle stammt und dort nicht mehr steht, bleibt mit seinen Aufträgen erhalten.
        foreach (var kunde in kunden.Where(k => k.Quelle == _quelle.Name && k.Aktiv && !vergeben.Contains(k)))
        {
            kunde.Aktiv = false;
            inaktiv++;
        }

        await db.SaveChangesAsync(abbruch);
        return new(jetzt, eintraege.Count, neu, geaendert, reaktiviert, inaktiv, hinweise);
    }

    private static string? Bereinigt(string? text, int laenge)
    {
        var bereinigt = text?.Trim();
        return string.IsNullOrEmpty(bereinigt) ? null : bereinigt[..Math.Min(bereinigt.Length, laenge)];
    }
}

/// <summary>Stößt den Abgleich regelmäßig an: kurz nach dem Start und dann im eingestellten Abstand.</summary>
public class Abgleichhintergrund(IServiceScopeFactory scopes, IOptions<KundenabgleichOptionen> optionen, ILogger<Abgleichhintergrund> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken abbruch)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), abbruch);
            using var takt = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(5, optionen.Value.AbstandMinuten)));
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<Kundenabgleich>().AusfuehrenAsync(abbruch);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Der Kundenabgleich im Hintergrund ist auf einen Fehler gelaufen.");
                }
            }
            while (await takt.WaitForNextTickAsync(abbruch));
        }
        catch (OperationCanceledException)
        {
            // Die Anwendung wird beendet.
        }
    }
}

public static class KundenabgleichExtensions
{
    public static void AddAvisKundenabgleich(this WebApplicationBuilder builder)
    {
        var abschnitt = builder.Configuration.GetSection("Kundenabgleich");
        builder.Services.Configure<KundenabgleichOptionen>(abschnitt);
        builder.Services.AddSingleton<Abgleichstand>();
        builder.Services.AddScoped<Kundenabgleich>();

        var optionen = abschnitt.Get<KundenabgleichOptionen>() ?? new KundenabgleichOptionen();
        switch (optionen.Art)
        {
            case KundenabgleichOptionen.Aus:
                break;
            case KundenabgleichOptionen.ActiveDirectory:
                // Ohne Filter wäre jedes Konto der Domäne ein Kunde. Das soll niemand versehentlich einstellen.
                var filter = optionen.Filter?.Trim() ?? "";
                if (filter.Length == 0)
                    throw new InvalidOperationException("Für Kundenabgleich:Art = ActiveDirectory muss Kundenabgleich:Filter gesetzt sein.");
                if (!filter.StartsWith('(') || !filter.EndsWith(')'))
                    throw new InvalidOperationException("Kundenabgleich:Filter muss ein LDAP-Filter in Klammern sein, zum Beispiel (department=*).");
                if (!OperatingSystem.IsWindows())
                    throw new InvalidOperationException("Kundenabgleich:Art = ActiveDirectory läuft nur unter Windows.");
                builder.Services.AddSingleton<IKundenquelle, ActiveDirectoryKundenquelle>();
                builder.Services.AddHostedService<Abgleichhintergrund>();
                break;
            default:
                throw new InvalidOperationException($"Unbekannte Einstellung Kundenabgleich:Art = {optionen.Art}.");
        }
    }
}
