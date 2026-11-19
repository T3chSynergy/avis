using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Avis.Web.Abgleich;

public class GeraeteabgleichOptionen
{
    public const string Aus = "Aus";
    public const string SnipeIt = "SnipeIt";

    /// <summary>
    /// "Aus": Geräte werden nur in AVIS gepflegt. "SnipeIt": Die Geräte kommen
    /// aus einer Installation von Snipe-IT. AVIS liest dort nur.
    /// </summary>
    public string Art { get; set; } = Aus;

    /// <summary>Adresse der Installation, zum Beispiel https://inventar.example.org</summary>
    public string? Adresse { get; set; }

    /// <summary>
    /// Zugangsschlüssel eines Kontos, das in Snipe-IT Assets und Benutzer
    /// ansehen darf. Gehört als Umgebungsvariable auf den Server, nicht in
    /// die Datei im Repository.
    /// </summary>
    public string? Schluessel { get; set; }

    /// <summary>
    /// Wann der Abgleich täglich läuft, in der Zeitzone der Anwendung, in der
    /// Form "02:30". Nach den Kunden: Der Nutzer eines Geräts wird über die
    /// Adresse eines Kunden gefunden.
    /// </summary>
    public string Uhrzeit { get; set; } = "02:30";

    /// <summary>
    /// Ob neben dem Abgleich noch Geräte von Hand angelegt werden dürfen. Mit "false" kommen neue Geräte nur
    /// noch aus der Quelle. Ohne Abgleich hat die Einstellung keine Wirkung.
    /// </summary>
    public bool VonHandAnlegen { get; set; } = true;

    public TimeOnly Laufzeit => Abgleichplan.Uhrzeit(Uhrzeit, "Geraeteabgleich:Uhrzeit");
}

/// <summary>Ein Gerät, wie es eine externe Quelle kennt.</summary>
/// <param name="Kennung">Bleibt in der Quelle gleich, auch wenn sich die Inventarnummer ändert.</param>
/// <param name="NutzerEmail">Adresse dessen, der das Gerät hat. Darüber wird der Kunde gefunden.</param>
public record Quellgeraet(string Kennung, string Inventarnummer, string? Bezeichnung, string? Typ, string? Standort, string? NutzerEmail);

/// <summary>
/// Liefert alle Geräte einer externen Quelle, die in Betrieb sind. Der
/// Abgleich weiß nicht, woher sie stammen.
/// </summary>
public interface IGeraetequelle
{
    /// <summary>Steht als Quelle am Gerät, zum Beispiel "SnipeIt".</summary>
    string Name { get; }

    /// <summary>Alle Geräte der Quelle. Wirft eine Ausnahme, wenn sie nicht vollständig gelesen werden konnte.</summary>
    Task<IReadOnlyList<Quellgeraet>> LesenAsync(CancellationToken abbruch);
}

/// <summary>
/// Gleicht die Gerätetabelle mit einer externen Quelle ab, nach denselben
/// Regeln wie bei den Kunden: Die Quelle gewinnt. Was dort fehlt, wird nicht
/// gelöscht, sondern ausgemustert. Geräte, die nur in AVIS angelegt wurden,
/// bleiben unberührt. AVIS schreibt nie in die Quelle.
/// </summary>
public class Geraeteabgleich(
    AvisDb db,
    IEnumerable<IGeraetequelle> quellen,
    Abgleichprotokoll protokoll,
    Abgleichsperre sperre,
    IOptions<GeraeteabgleichOptionen> optionen,
    TimeProvider uhr,
    ILogger<Geraeteabgleich> log)
{
    private readonly IGeraetequelle? _quelle = quellen.FirstOrDefault();

    public bool Eingerichtet => _quelle is not null;

    /// <summary>Ob sich Geräte von Hand anlegen lassen: immer ohne Abgleich, mit Abgleich nach der Einstellung.</summary>
    public bool VonHandAnlegen => !Eingerichtet || optionen.Value.VonHandAnlegen;

    public async Task<Abgleichergebnis> AusfuehrenAsync(CancellationToken abbruch = default)
    {
        // Läuft derselbe Abgleich schon, geschieht nichts: Das Ergebnis des laufenden bleibt das letzte.
        using var belegt = sperre.Belegen(Abgleichlauf.Geraete);
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
            log.LogError(ex, "Der Geräteabgleich ist auf einen Fehler gelaufen.");
            db.ChangeTracker.Clear();
            ergebnis = new(uhr.GetUtcNow().UtcDateTime, Fehler: $"Der Abgleich ist auf einen Fehler gelaufen, es wurde nichts geändert: {ex.Message}");
        }

        await protokoll.FesthaltenAsync(Abgleichlauf.Geraete, ergebnis, abbruch);
        if (ergebnis.Fehler is not null)
            log.LogError("Der Geräteabgleich ist gescheitert, es wurde nichts geändert: {Fehler}", ergebnis.Fehler);
        else
            log.LogInformation(
                "Geräteabgleich: {Gelesen} gelesen, {Neu} neu, {Geaendert} geändert, {Reaktiviert} wieder in Betrieb, {Inaktiv} ausgemustert, {Hinweise} Hinweise.",
                ergebnis.Gelesen, ergebnis.Neu, ergebnis.Geaendert, ergebnis.Reaktiviert, ergebnis.Inaktiv, ergebnis.Hinweise?.Count ?? 0);
        return ergebnis;
    }

    private async Task<Abgleichergebnis> AbgleichenAsync(CancellationToken abbruch)
    {
        var jetzt = uhr.GetUtcNow().UtcDateTime;
        if (_quelle is null)
            return new(jetzt, Fehler: "Es ist keine Quelle eingerichtet (Geraeteabgleich:Art).");

        IReadOnlyList<Quellgeraet> gelesen;
        try
        {
            gelesen = await _quelle.LesenAsync(abbruch);
        }
        catch (Exception ex) when (!Abgleichplan.Abgebrochen(ex, abbruch))
        {
            log.LogWarning(ex, "Die Quelle {Quelle} konnte nicht gelesen werden.", _quelle.Name);
            return new(jetzt, Fehler: $"Die Quelle {_quelle.Name} konnte nicht gelesen werden: {ex.Message}");
        }

        var hinweise = new List<string>();

        // Ohne Inventarnummer lässt sich ein Gerät in AVIS nicht ansprechen. Dieselbe Nummer zweimal: Das erste zählt.
        var eintraege = new List<Quellgeraet>();
        var nummern = new HashSet<string>();
        foreach (var eintrag in gelesen.Where(e => !string.IsNullOrWhiteSpace(e.Kennung)))
        {
            if (string.IsNullOrWhiteSpace(eintrag.Inventarnummer))
                continue;
            if (eintrag.Inventarnummer.Trim().Length > 50)
                hinweise.Add($"{eintrag.Inventarnummer.Trim()[..50]}…: Die Inventarnummer ist länger als 50 Zeichen, das Gerät wurde ausgelassen.");
            else if (!nummern.Add(Inventargeraet.Schluessel(eintrag.Inventarnummer)))
                hinweise.Add($"{eintrag.Inventarnummer.Trim()}: Die Inventarnummer kommt in der Quelle mehrfach vor. Nur das erste Gerät wurde übernommen.");
            else
                eintraege.Add(eintrag);
        }

        // Eine leere Antwort ist fast immer ein Fehler der Quelle. Ohne diese
        // Bremse wären danach alle Geräte auf einen Schlag ausgemustert.
        if (eintraege.Count == 0)
            return new(jetzt, Fehler: $"Die Quelle {_quelle.Name} hat kein einziges Gerät mit Inventarnummer geliefert. Damit nicht alle Geräte ausgemustert werden, wurde nichts geändert.");

        var geraete = await db.Geraete.ToListAsync(abbruch);
        var kunden = (await db.Kunden.Where(k => k.Email != null).Select(k => new { k.Id, k.Email }).ToListAsync(abbruch))
            .GroupBy(k => k.Email!)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var vergeben = new HashSet<Inventargeraet>();
        int neu = 0, geaendert = 0, reaktiviert = 0, inaktiv = 0;

        // Nachschlagen statt Durchsuchen: Bei zwanzigtausend Geräten dauerte jeder Lauf sonst viele Sekunden.
        // Die Inventarnummer ist in AVIS eindeutig, die Kennung innerhalb einer Quelle auch.
        var nachKennung = geraete
            .Where(g => g.Quelle == _quelle.Name && g.ExterneKennung is not null)
            .GroupBy(g => g.ExterneKennung!)
            .ToDictionary(g => g.Key, g => g.First());
        var nachNummer = geraete.ToDictionary(g => g.Nummernschluessel);
        var kennungenDerQuelle = eintraege.Select(e => e.Kennung.Trim()).ToHashSet();

        foreach (var eintrag in eintraege)
        {
            var kennung = eintrag.Kennung.Trim();
            var nummer = eintrag.Inventarnummer.Trim();
            var schluessel = Inventargeraet.Schluessel(nummer);

            // Wiedererkannt wird über die Kennung der Quelle. Ein Gerät, das mit derselben Inventarnummer schon
            // von Hand in AVIS angelegt wurde, wird übernommen statt verdoppelt. Dasselbe gilt für ein Gerät aus
            // dieser Quelle, dessen Kennung es dort nicht mehr gibt: Es wurde in der Quelle gelöscht und mit
            // derselben Inventarnummer neu angelegt. Sonst bliebe das neue für immer ausgelassen.
            nachNummer.TryGetValue(schluessel, out var mitNummer);
            var uebernehmbar = mitNummer is not null && !vergeben.Contains(mitNummer)
                && (mitNummer.Quelle is null
                    || (mitNummer.Quelle == _quelle.Name && !kennungenDerQuelle.Contains(mitNummer.ExterneKennung ?? "")));
            var geraet = nachKennung.GetValueOrDefault(kennung) ?? (uebernehmbar ? mitNummer : null);

            // Die Inventarnummer ist in AVIS eindeutig. Trägt sie schon ein anderes Gerät, bleibt dieses hier aus.
            if (mitNummer is not null && mitNummer != geraet)
            {
                var besitzer = mitNummer;
                hinweise.Add($"{nummer}: Die Inventarnummer gehört in AVIS schon zu einem anderen Gerät ({besitzer.Bezeichnung}). Das Gerät aus {_quelle.Name} wurde ausgelassen.");
                if (geraet is not null)
                    vergeben.Add(geraet);
                continue;
            }

            var istNeu = geraet is null;
            if (geraet is null)
            {
                geraet = new Inventargeraet();
                db.Geraete.Add(geraet);
                geraete.Add(geraet);
            }
            else if (nachNummer.GetValueOrDefault(geraet.Nummernschluessel) == geraet)
            {
                // Die bisherige Nummer wird frei, falls das Gerät gleich eine andere bekommt.
                nachNummer.Remove(geraet.Nummernschluessel);
            }
            vergeben.Add(geraet);

            var email = Bereinigt(eintrag.NutzerEmail, 320)?.ToLowerInvariant();
            int? kunde = email is not null && kunden.TryGetValue(email, out var id) ? id : null;

            var vorher = (geraet.Quelle, geraet.ExterneKennung, geraet.Inventarnummer, geraet.Bezeichnung, geraet.Typ, geraet.Standort, geraet.KundeId);
            var warAktiv = geraet.Aktiv;

            geraet.Quelle = _quelle.Name;
            geraet.ExterneKennung = kennung;
            geraet.Inventarnummer = nummer;
            geraet.Bezeichnung = Bereinigt(eintrag.Bezeichnung, 200) ?? nummer;
            geraet.Typ = Bereinigt(eintrag.Typ, 100);
            geraet.Standort = Bereinigt(eintrag.Standort, 100);
            geraet.KundeId = kunde;
            geraet.Aktiv = true;
            geraet.SuchtextAktualisieren();
            nachKennung[kennung] = geraet;
            nachNummer[geraet.Nummernschluessel] = geraet;

            if (istNeu)
                neu++;
            else if (!warAktiv)
                reaktiviert++;
            else if (vorher != (geraet.Quelle, geraet.ExterneKennung, geraet.Inventarnummer, geraet.Bezeichnung, geraet.Typ, geraet.Standort, geraet.KundeId))
                geaendert++;
        }

        // Was aus dieser Quelle stammt und dort nicht mehr steht, bleibt mit seinen Aufträgen erhalten.
        foreach (var geraet in geraete.Where(g => g.Quelle == _quelle.Name && g.Aktiv && !vergeben.Contains(g)))
        {
            geraet.Aktiv = false;
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

public static class GeraeteabgleichExtensions
{
    public static void AddAvisGeraeteabgleich(this WebApplicationBuilder builder)
    {
        var abschnitt = builder.Configuration.GetSection("Geraeteabgleich");
        builder.Services.Configure<GeraeteabgleichOptionen>(abschnitt);
        builder.Services.TryAddScoped<Abgleichprotokoll>();
        builder.Services.TryAddSingleton<Abgleichsperre>();
        builder.Services.AddScoped<Geraeteabgleich>();

        var optionen = abschnitt.Get<GeraeteabgleichOptionen>() ?? new GeraeteabgleichOptionen();
        switch (optionen.Art)
        {
            case GeraeteabgleichOptionen.Aus:
                break;
            case GeraeteabgleichOptionen.SnipeIt:
                if (!Uri.TryCreate(optionen.Adresse?.Trim(), UriKind.Absolute, out var adresse) || adresse.Scheme is not ("https" or "http"))
                    throw new InvalidOperationException("Für Geraeteabgleich:Art = SnipeIt muss Geraeteabgleich:Adresse die Adresse der Installation sein, zum Beispiel https://inventar.example.org.");
                if (string.IsNullOrWhiteSpace(optionen.Schluessel))
                    throw new InvalidOperationException("Für Geraeteabgleich:Art = SnipeIt muss Geraeteabgleich:Schluessel gesetzt sein.");
                _ = optionen.Laufzeit;
                builder.Services.AddHttpClient<IGeraetequelle, SnipeItGeraetequelle>(http => SnipeItGeraetequelle.Einrichten(http, optionen));
                builder.Services.AddHostedService<Abgleichhintergrund>();
                break;
            default:
                throw new InvalidOperationException($"Unbekannte Einstellung Geraeteabgleich:Art = {optionen.Art}.");
        }
    }
}
