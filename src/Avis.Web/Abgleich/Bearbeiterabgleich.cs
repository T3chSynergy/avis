using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Avis.Web.Abgleich;

public class BearbeiterabgleichOptionen
{
    public const string Aus = "Aus";
    public const string ActiveDirectory = "ActiveDirectory";

    /// <summary>
    /// "Aus": Wer Bearbeiter ist, legt ein Administrator in AVIS fest.
    /// "ActiveDirectory": Bearbeiter sind die Benutzerkonten des Active
    /// Directory, auf die der Filter passt.
    /// </summary>
    public string Art { get; set; } = Aus;

    /// <summary>
    /// LDAP-Filter, der bestimmt, wer Bearbeiter ist, in der Regel die
    /// Mitglieder einer Gruppe: "(memberOf=CN=AVIS-Bearbeiter,OU=Gruppen,DC=example,DC=org)".
    /// Dass nur aktive Benutzerkonten zählen, ergänzt AVIS selbst.
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>Wo im Verzeichnis gesucht wird. Leer: in der ganzen Domäne des Servers.</summary>
    public string? Suchbasis { get; set; }

    /// <summary>Wann der Abgleich täglich läuft, in der Zeitzone der Anwendung, in der Form "01:30".</summary>
    public string Uhrzeit { get; set; } = "01:30";

    public TimeOnly Laufzeit => Abgleichplan.Uhrzeit(Uhrzeit, "Bearbeiterabgleich:Uhrzeit");
}

/// <summary>Ein Bearbeiter, wie ihn eine externe Quelle kennt.</summary>
/// <param name="Kennung">Bleibt in der Quelle gleich, auch wenn sich Name oder Anmeldename ändern.</param>
/// <param name="Anmeldename">So, wie ihn die Anmeldung liefert, bei Windows "domäne\name". Ohne ihn kann sich niemand anmelden.</param>
public record Quellbearbeiter(string Kennung, string? Anmeldename, string? Name, string? Email);

/// <summary>Liefert alle Bearbeiter einer externen Quelle. Der Abgleich weiß nicht, woher sie stammen.</summary>
public interface IBearbeiterquelle
{
    /// <summary>Steht als Quelle am Benutzer, zum Beispiel "ActiveDirectory".</summary>
    string Name { get; }

    /// <summary>Alle Bearbeiter der Quelle. Wirft eine Ausnahme, wenn sie nicht vollständig gelesen werden konnte.</summary>
    IReadOnlyList<Quellbearbeiter> Lesen();
}

/// <summary>
/// Gleicht ab, wer Bearbeiter ist. Die Quelle gewinnt: Wen sie nennt, der
/// ist Bearbeiter und trägt ihren Namen und ihre Adresse; ein Konto, das es
/// in AVIS noch nicht gibt, wird angelegt. Wen sie nicht mehr nennt, der
/// verliert die Rolle, bleibt aber als Konto erhalten. Unberührt bleiben
/// Bearbeiter, die nur in AVIS dazu gemacht wurden, und Administratoren.
/// </summary>
public class Bearbeiterabgleich(
    AvisDb db,
    IEnumerable<IBearbeiterquelle> quellen,
    Abgleichprotokoll protokoll,
    Abgleichsperre sperre,
    TimeProvider uhr,
    ILogger<Bearbeiterabgleich> log)
{
    /// <summary>Anmeldename des Kontos, in dessen Namen der Abgleich Aufträge an ihre Gruppen zurückgibt.</summary>
    public const string Systemkonto = "avis:abgleich";

    private readonly IBearbeiterquelle? _quelle = quellen.FirstOrDefault();

    public bool Eingerichtet => _quelle is not null;

    public async Task<Abgleichergebnis> AusfuehrenAsync(CancellationToken abbruch = default)
    {
        // Läuft derselbe Abgleich schon, geschieht nichts: Das Ergebnis des laufenden bleibt das letzte.
        using var belegt = sperre.Belegen(Abgleichlauf.Bearbeiter);
        if (belegt is null)
            return new(uhr.GetUtcNow().UtcDateTime, Fehler: Abgleichsperre.LaeuftBereits);

        Abgleichergebnis ergebnis;
        try
        {
            ergebnis = await AbgleichenAsync(abbruch);
        }
        catch (Exception ex) when (!Abgleichplan.Abgebrochen(ex, abbruch))
        {
            // Etwa wenn sich das Ergebnis nicht speichern lässt. Was halb vorbereitet war, wird verworfen.
            log.LogError(ex, "Der Bearbeiterabgleich ist auf einen Fehler gelaufen.");
            db.ChangeTracker.Clear();
            ergebnis = new(uhr.GetUtcNow().UtcDateTime, Fehler: $"Der Abgleich ist auf einen Fehler gelaufen, es wurde nichts geändert: {ex.Message}");
        }

        await protokoll.FesthaltenAsync(Abgleichlauf.Bearbeiter, ergebnis, abbruch);
        if (ergebnis.Fehler is not null)
            log.LogError("Der Bearbeiterabgleich ist gescheitert, es wurde nichts geändert: {Fehler}", ergebnis.Fehler);
        else
            log.LogInformation(
                "Bearbeiterabgleich: {Gelesen} gelesen, {Neu} neu, {Geaendert} geändert, {Reaktiviert} wieder Bearbeiter, {Inaktiv} nicht mehr Bearbeiter, {Hinweise} Hinweise.",
                ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv, ergebnis.Hinweise?.Count ?? 0);
        return ergebnis;
    }

    private async Task<Abgleichergebnis> AbgleichenAsync(CancellationToken abbruch)
    {
        var jetzt = uhr.GetUtcNow().UtcDateTime;
        if (_quelle is null)
            return new(jetzt, Fehler: "Es ist keine Quelle eingerichtet (Bearbeiterabgleich:Art).");

        List<Quellbearbeiter> gelesen;
        try
        {
            // Dieselbe Kennung zweimal: Der erste Eintrag zählt.
            gelesen = _quelle.Lesen()
                .Where(e => !string.IsNullOrWhiteSpace(e.Kennung))
                .GroupBy(e => e.Kennung.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex) when (!Abgleichplan.Abgebrochen(ex, abbruch))
        {
            log.LogWarning(ex, "Die Quelle {Quelle} konnte nicht gelesen werden.", _quelle.Name);
            return new(jetzt, Fehler: $"Die Quelle {_quelle.Name} konnte nicht gelesen werden: {ex.Message}");
        }

        var hinweise = new List<string>();

        // Ohne Anmeldenamen kann sich niemand anmelden. Die Konten, die AVIS sich selbst anlegt, sind nie gemeint.
        var eintraege = new List<(string Kennung, string Anmeldename, Quellbearbeiter Eintrag)>();
        foreach (var eintrag in gelesen)
        {
            var anmeldename = Bereinigt(eintrag.Anmeldename, 200)?.ToLowerInvariant();
            if (anmeldename is null || anmeldename.StartsWith("avis:", StringComparison.Ordinal))
                hinweise.Add($"{eintrag.Name ?? eintrag.Kennung}: Das Konto hat keinen brauchbaren Anmeldenamen und wurde ausgelassen.");
            else
                eintraege.Add((eintrag.Kennung.Trim(), anmeldename, eintrag));
        }

        // Eine leere Antwort ist fast immer ein Fehler der Quelle. Ohne diese
        // Bremse verlören alle auf einen Schlag die Rolle, und ihre Aufträge gingen zurück an die Gruppen.
        if (eintraege.Count == 0)
            return new(jetzt, Fehler: $"Die Quelle {_quelle.Name} hat keinen einzigen Bearbeiter geliefert. Damit nicht alle die Rolle verlieren, wurde nichts geändert.");

        var benutzer = await db.Benutzer.ToListAsync(abbruch);
        var vergeben = new HashSet<Benutzer>();
        int neu = 0, geaendert = 0, reaktiviert = 0, inaktiv = 0;

        foreach (var (kennung, anmeldename, eintrag) in eintraege)
        {
            // Wiedererkannt wird zuerst über die Kennung der Quelle, sonst über den Anmeldenamen: Wer sich schon
            // angemeldet hat oder von Hand angelegt wurde, wird übernommen statt verdoppelt.
            var konto = benutzer.FirstOrDefault(b => b.Quelle == _quelle.Name && string.Equals(b.ExterneKennung, kennung, StringComparison.OrdinalIgnoreCase))
                ?? benutzer.FirstOrDefault(b => b.Anmeldename == anmeldename && !vergeben.Contains(b));

            // Der Anmeldename ist in AVIS eindeutig. Trägt ihn schon ein anderes Konto, bleibt dieses hier aus.
            var besitzer = benutzer.FirstOrDefault(b => b != konto && b.Anmeldename == anmeldename);
            if (besitzer is not null)
            {
                hinweise.Add($"{eintrag.Name ?? anmeldename}: Der Anmeldename {anmeldename} gehört schon zum Konto {besitzer.Name}. Der Eintrag aus {_quelle.Name} wurde ausgelassen.");
                if (konto is not null)
                    vergeben.Add(konto);
                continue;
            }

            var istNeu = konto is null;
            if (konto is null)
            {
                konto = new Benutzer();
                db.Benutzer.Add(konto);
                benutzer.Add(konto);
            }
            vergeben.Add(konto);

            var vorher = (konto.Quelle, konto.ExterneKennung, konto.Anmeldename, konto.Name, konto.Email, konto.IstBearbeiter);
            var kamAusDerQuelle = konto.Quelle == _quelle.Name;

            konto.Quelle = _quelle.Name;
            konto.ExterneKennung = kennung;
            konto.Anmeldename = anmeldename;
            konto.Name = Bereinigt(eintrag.Name, 200) ?? OhneDomaene(anmeldename);
            konto.Email = Bereinigt(eintrag.Email, 320)?.ToLowerInvariant();
            konto.IstBearbeiter = true;

            if (istNeu)
                neu++;
            else if (kamAusDerQuelle && !vorher.IstBearbeiter)
                reaktiviert++;
            else if (vorher != (konto.Quelle, konto.ExterneKennung, konto.Anmeldename, konto.Name, konto.Email, konto.IstBearbeiter))
                geaendert++;
        }

        // Wer aus dieser Quelle stammt und dort nicht mehr steht, ist kein Bearbeiter mehr. Das Konto bleibt:
        // An ihm hängt der Verlauf, und als Kunde kann sich die Person weiter anmelden.
        Benutzer? system = null;
        foreach (var konto in benutzer.Where(b => b.Quelle == _quelle.Name && b.IstBearbeiter && !vergeben.Contains(b)).ToList())
        {
            if (konto.IstAdministrator)
            {
                hinweise.Add($"{konto.Name} steht nicht mehr in {_quelle.Name}, ist aber Administrator und bleibt deshalb Bearbeiter.");
                continue;
            }

            system ??= await SystemkontoAsync(benutzer, abbruch);
            konto.IstBearbeiter = false;
            await db.AusDerBearbeitungNehmenAsync(konto, system, jetzt, $"{konto.Name} ist kein Bearbeiter mehr.", abbruch);
            inaktiv++;
        }

        await db.SaveChangesAsync(abbruch);
        return new(jetzt, eintraege.Count, neu, geaendert, reaktiviert, inaktiv, hinweise);
    }

    /// <summary>Das Konto, das im Verlauf steht, wenn der Abgleich einen Auftrag zurückgibt. Anmelden kann sich damit niemand.</summary>
    private async Task<Benutzer> SystemkontoAsync(List<Benutzer> benutzer, CancellationToken abbruch)
    {
        var system = benutzer.FirstOrDefault(b => b.Anmeldename == Systemkonto);
        if (system is null)
        {
            system = new Benutzer { Anmeldename = Systemkonto, Name = "Bearbeiterabgleich", Aktiv = false };
            db.Benutzer.Add(system);
            await db.SaveChangesAsync(abbruch);
        }
        return system;
    }

    private static string? Bereinigt(string? text, int laenge)
    {
        var bereinigt = text?.Trim();
        return string.IsNullOrEmpty(bereinigt) ? null : bereinigt[..Math.Min(bereinigt.Length, laenge)];
    }

    private static string OhneDomaene(string anmeldename)
    {
        var i = anmeldename.LastIndexOf('\\');
        return i >= 0 && i < anmeldename.Length - 1 ? anmeldename[(i + 1)..] : anmeldename;
    }
}

public static class BearbeiterabgleichExtensions
{
    public static void AddAvisBearbeiterabgleich(this WebApplicationBuilder builder)
    {
        var abschnitt = builder.Configuration.GetSection("Bearbeiterabgleich");
        builder.Services.Configure<BearbeiterabgleichOptionen>(abschnitt);
        builder.Services.TryAddScoped<Abgleichprotokoll>();
        builder.Services.TryAddSingleton<Abgleichsperre>();
        builder.Services.AddScoped<Bearbeiterabgleich>();

        var optionen = abschnitt.Get<BearbeiterabgleichOptionen>() ?? new BearbeiterabgleichOptionen();
        switch (optionen.Art)
        {
            case BearbeiterabgleichOptionen.Aus:
                break;
            case BearbeiterabgleichOptionen.ActiveDirectory:
                ActiveDirectoryKonten.FilterPruefen(optionen.Filter, "Bearbeiterabgleich");
                if (!OperatingSystem.IsWindows())
                    throw new InvalidOperationException("Bearbeiterabgleich:Art = ActiveDirectory läuft nur unter Windows.");
                _ = optionen.Laufzeit;
                builder.Services.AddSingleton<IBearbeiterquelle, ActiveDirectoryBearbeiterquelle>();
                builder.Services.AddHostedService<Abgleichhintergrund>();
                break;
            default:
                throw new InvalidOperationException($"Unbekannte Einstellung Bearbeiterabgleich:Art = {optionen.Art}.");
        }
    }

    /// <summary>
    /// Gibt die Aufträge, die jemand bearbeitet, an ihre Gruppen zurück und
    /// trägt ihn aus allen Gruppen aus. So geht nichts unter, wenn jemand
    /// kein Bearbeiter mehr ist. Gibt zurück, wie viele Aufträge es waren.
    /// </summary>
    public static async Task<int> AusDerBearbeitungNehmenAsync(
        this AvisDb db, Benutzer benutzer, Benutzer durch, DateTime jetzt, string grund, CancellationToken abbruch = default)
    {
        var auftraege = await db.Auftraege
            .InBearbeitung()
            .Where(a => a.BearbeiterId == benutzer.Id)
            .ZumAendern()
            .ToListAsync(abbruch);

        foreach (var auftrag in auftraege)
            auftrag.ZurueckAnGruppe(durch, jetzt, grund);

        await db.Entry(benutzer).Collection(b => b.Gruppen).LoadAsync(abbruch);
        benutzer.Gruppen.Clear();
        return auftraege.Count;
    }
}
