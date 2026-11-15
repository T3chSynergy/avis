using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Avis.Web.Data;

public static class Startdaten
{
    /// <summary>Bringt die Datenbank auf den aktuellen Stand und sorgt für eine Eingangsgruppe.</summary>
    public static async Task DatenbankVorbereitenAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvisDb>();

        await db.Database.MigrateAsync();
        await FuellenAsync(db, app.Configuration, app.Environment, app.Logger);
    }

    /// <summary>
    /// Leert alle Tabellen und legt die Testdaten neu an, als wäre die
    /// Anwendung zum ersten Mal gestartet. Nur für die Entwicklungsumgebung
    /// mit eingeschalteten Testdaten; sonst geschieht nichts und es gibt
    /// einen Fehler. Die Dateien der Anhänge räumt der Aufrufer weg.
    ///
    /// Geleert wird in einer Transaktion, die Datei bleibt bestehen: So stört
    /// es nicht, wenn der Mailversand im Hintergrund gerade liest. Die
    /// Anweisungen sind die von SQLite.
    /// </summary>
    public static async Task NeuAnlegenAsync(AvisDb db, IConfiguration konfiguration, IHostEnvironment umgebung, ILogger log)
    {
        if (!umgebung.IsDevelopment() || !konfiguration.GetValue<bool>("Testdaten:Anlegen"))
            throw new InvalidOperationException(
                "Die Datenbank lässt sich nur in der Entwicklungsumgebung mit Testdaten (Testdaten:Anlegen) neu anlegen.");
        if (!db.Database.IsSqlite())
            throw new NotSupportedException("Das Neuanlegen der Testdaten ist nur für SQLite umgesetzt.");

        var sql = db.GetService<ISqlGenerationHelper>();
        var tabellen = db.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .OfType<string>()
            .Distinct()
            .ToList();

        await using (var transaktion = await db.Database.BeginTransactionAsync())
        {
            // Die Reihenfolge der Tabellen spielt so keine Rolle: Geprüft wird erst am Ende, wenn alles leer ist.
            await db.Database.ExecuteSqlRawAsync("PRAGMA defer_foreign_keys = ON");
            foreach (var tabelle in tabellen)
            {
                // Die Namen stammen aus dem Modell, nicht aus einer Eingabe.
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM {sql.DelimitIdentifier(tabelle)}");
#pragma warning restore EF1002
            }
            // Die Zählung beginnt wieder von vorn.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence");
            await transaktion.CommitAsync();
        }
        db.ChangeTracker.Clear();

        await FuellenAsync(db, konfiguration, umgebung, log);
        log.LogWarning("Die Datenbank wurde geleert und mit Testdaten neu angelegt.");
    }

    /// <summary>Was eine Datenbank mit aktuellen Tabellen noch braucht: Zählung, Eingangsgruppe, Suchtexte, Testdaten.</summary>
    private static async Task FuellenAsync(AvisDb db, IConfiguration konfiguration, IHostEnvironment umgebung, ILogger log)
    {
        var ersteNummer = konfiguration.GetValue<int>("Auftraege:ErsteNummer");
        if (await AuftragsnummerAnhebenAsync(db, ersteNummer))
            log.LogInformation("Der nächste Auftrag bekommt die Nummer {Nummer} (Auftraege:ErsteNummer).", ersteNummer);

        if (!await db.Gruppen.AnyAsync(g => g.IstEingangsgruppe))
        {
            db.Gruppen.Add(new Gruppe { Name = "IT-Hotline", IstEingangsgruppe = true });
            await db.SaveChangesAsync();
        }

        // Kunden aus der Zeit vor der Kundensuche bekommen ihren Suchtext.
        var ohneSuchtext = await db.Kunden.Where(k => k.Suchtext == "").ToListAsync();
        if (ohneSuchtext.Count > 0)
        {
            foreach (var kunde in ohneSuchtext)
                kunde.SuchtextAktualisieren();
            await db.SaveChangesAsync();
        }

        // Aufträge aus der Zeit vor der Suche bekommen ihren Suchtext.
        var auftraegeOhneSuchtext = await db.Auftraege.Where(a => a.Suchtext == "").Include(a => a.Verlauf).ToListAsync();
        if (auftraegeOhneSuchtext.Count > 0)
        {
            foreach (var auftrag in auftraegeOhneSuchtext)
                auftrag.SuchtextAktualisieren();
            await db.SaveChangesAsync();
        }

        if (konfiguration.GetValue<bool>("Testdaten:Anlegen"))
        {
            if (!umgebung.IsDevelopment())
                throw new InvalidOperationException(
                    "Testdaten (Testdaten:Anlegen) sind nur in der Entwicklungsumgebung erlaubt.");

            if (!await db.Benutzer.AnyAsync())
                await TestdatenAnlegenAsync(db, TimeProvider.System.GetUtcNow().UtcDateTime);
        }
    }

    /// <summary>
    /// Sorgt dafür, dass der nächste Auftrag mindestens diese Nummer bekommt,
    /// um an eine vorhandene Zählung anzuschließen. Wirkt nur nach oben: Ist
    /// die Zählung schon weiter, bleibt sie, wie sie ist. Gibt zurück, ob sie
    /// angehoben wurde.
    ///
    /// Die Zählung führt SQLite selbst, in der Tabelle sqlite_sequence. Für
    /// eine andere Datenbank ist diese Stelle neu zu schreiben.
    /// </summary>
    public static async Task<bool> AuftragsnummerAnhebenAsync(AvisDb db, int ersteNummer)
    {
        if (ersteNummer <= 1)
            return false;
        if (!db.Database.IsSqlite())
            throw new NotSupportedException("Auftraege:ErsteNummer ist nur für SQLite umgesetzt.");

        var tabelle = db.Model.FindEntityType(typeof(Auftrag))!.GetTableName()!;
        var stand = ersteNummer - 1;

        // Die Zeile für die Tabelle gibt es erst, wenn der erste Auftrag angelegt wurde.
        var geaendert = await db.Database.ExecuteSqlAsync(
            $"UPDATE sqlite_sequence SET seq = {stand} WHERE name = {tabelle} AND seq < {stand}");
        geaendert += await db.Database.ExecuteSqlAsync(
            $"INSERT INTO sqlite_sequence (name, seq) SELECT {tabelle}, {stand} WHERE NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = {tabelle})");
        return geaendert > 0;
    }

    private static async Task TestdatenAnlegenAsync(AvisDb db, DateTime jetzt)
    {
        var hotline = await db.Gruppen.FirstAsync(g => g.IstEingangsgruppe);
        var netzwerk = new Gruppe { Name = "Netzwerk" };
        var systeme = new Gruppe { Name = "Systemverwaltung" };
        var beschaffung = new Gruppe { Name = "Beschaffung" };
        var betreuung = new Gruppe { Name = "Anwenderbetreuung" };
        db.Gruppen.AddRange(netzwerk, systeme, beschaffung, betreuung);

        var anna = new Benutzer { Anmeldename = "anna", Name = "Anna Beispiel", Email = "anna.beispiel@example.org", IstBearbeiter = true, IstAdministrator = true };
        var bernd = new Benutzer { Anmeldename = "bernd", Name = "Bernd Muster", Email = "bernd.muster@example.org", IstBearbeiter = true };
        var clara = new Benutzer { Anmeldename = "clara", Name = "Clara Probe", Email = "clara.probe@example.org", IstBearbeiter = true };
        var dieter = new Benutzer { Anmeldename = "dieter", Name = "Dieter Kunde", Email = "dieter.kunde@example.org" };
        // Ein Bearbeiter, wie ihn der Abgleich mit dem Active Directory anlegt: mit der Kennung des Verzeichnisses
        // und dem Anmeldenamen samt Domäne. Name, Adresse und die Rolle Bearbeiter pflegt das Verzeichnis.
        var finn = new Benutzer
        {
            Anmeldename = @"avis\finn",
            Name = "Finn Verzeichnis",
            Email = "finn.verzeichnis@example.org",
            IstBearbeiter = true,
            Quelle = Avis.Web.Abgleich.BearbeiterabgleichOptionen.ActiveDirectory,
            ExterneKennung = "7c1f0a52-3b7e-4d1a-9c55-0d2a6f1e4c01",
        };
        db.Benutzer.AddRange(anna, bernd, clara, dieter, finn);

        hotline.Mitglieder.Add(anna);
        betreuung.Mitglieder.Add(anna);
        netzwerk.Mitglieder.Add(bernd);
        systeme.Mitglieder.Add(bernd);
        systeme.Mitglieder.Add(clara);
        beschaffung.Mitglieder.Add(clara);

        db.Kategorien.AddRange(
            new Kategorie { Name = "Störung" },
            new Kategorie { Name = "Anfrage" },
            new Kategorie { Name = "Beschaffung" },
            new Kategorie { Name = "Berechtigung" });

        // Der Standort ist freier Text, hier die Raumnummer: der Buchstabe steht für das Gebäude.
        var kunde1 = new Kunde { Name = "Dieter Kunde", Email = "dieter.kunde@example.org", Telefon = "1234", Anmeldename = "dieter", Standort = "A 204" };
        var kunde2 = new Kunde { Name = "Eva Test", Email = "eva.test@example.org", Telefon = "5678", Standort = "C 011" };
        db.Kunden.AddRange(
            kunde1,
            kunde2,
            new Kunde { Name = "Özlem Übung", Email = "oezlem.uebung@example.org", Telefon = "0221 12-34", Organisationseinheit = "Personal", Standort = "A 112" },
            new Kunde { Name = "Frank Müller", Email = "frank.mueller@example.org", Telefon = "2201", Organisationseinheit = "Finanzen", Standort = "A 210" },
            new Kunde { Name = "Gabi Müller", Email = "gabi.mueller@example.org", Telefon = "2202", Organisationseinheit = "Personal", Standort = "C 014" },
            // Von Hand angelegt und stillgelegt.
            new Kunde { Name = "Karl Ruhestand", Telefon = "2290", Organisationseinheit = "Ordnungsamt", Aktiv = false });

        // Kunden, wie sie der Abgleich mit dem Active Directory anlegt: mit der Kennung des Verzeichnisses und dem
        // Anmeldenamen samt Domäne. Ihre Angaben pflegt das Verzeichnis. Jana steht dort nicht mehr und ist inaktiv.
        const string verzeichnis = Avis.Web.Abgleich.KundenabgleichOptionen.ActiveDirectory;
        var hanna = new Kunde
        {
            Name = "Hanna Verzeichnis",
            Email = "hanna.verzeichnis@example.org",
            Telefon = "3101",
            Organisationseinheit = "Bauamt",
            Standort = "A 105",
            Anmeldename = @"avis\hanna",
            Quelle = verzeichnis,
            ExterneKennung = "7c1f0a52-3b7e-4d1a-9c55-0d2a6f1e4b01",
        };
        db.Kunden.AddRange(
            hanna,
            new Kunde
            {
                Name = "Igor Abgleich",
                Email = "igor.abgleich@example.org",
                Telefon = "3102",
                Organisationseinheit = "Kämmerei",
                Standort = "B 012",
                Anmeldename = @"avis\igor",
                Quelle = verzeichnis,
                ExterneKennung = "7c1f0a52-3b7e-4d1a-9c55-0d2a6f1e4b02",
            },
            new Kunde
            {
                Name = "Jana Ehemals",
                Email = "jana.ehemals@example.org",
                Telefon = "3103",
                Organisationseinheit = "Bauamt",
                Standort = "A 107",
                Anmeldename = @"avis\jana",
                Quelle = verzeichnis,
                ExterneKennung = "7c1f0a52-3b7e-4d1a-9c55-0d2a6f1e4b03",
                Aktiv = false,
            });

        var flurdrucker = new Inventargeraet { Inventarnummer = "DR-0042", Bezeichnung = "Laserdrucker Flur 2. OG", Typ = "Drucker", Standort = "A Flur 2. OG" };
        db.Geraete.AddRange(
            flurdrucker,
            new Inventargeraet { Inventarnummer = "NB-1017", Bezeichnung = "Notebook 14 Zoll", Typ = "Notebook", Standort = "A 204", Kunde = kunde1 },
            new Inventargeraet { Inventarnummer = "NB-1018", Bezeichnung = "Notebook 14 Zoll", Typ = "Notebook", Standort = "C 011", Kunde = kunde2 },
            new Inventargeraet { Inventarnummer = "MO-2203", Bezeichnung = "Monitor 27 Zoll", Typ = "Monitor", Standort = "A 204", Kunde = kunde1 });

        await db.SaveChangesAsync();

        // Geräte, wie sie der Abgleich mit Snipe-IT anlegt: mit der Nummer des Assets dort und dem Standort von dort.
        // Der Nutzer ist der Kunde mit der Adresse, an die das Asset ausgegeben ist. Das dritte steht dort nicht mehr.
        const string inventar = Avis.Web.Abgleich.GeraeteabgleichOptionen.SnipeIt;
        db.Geraete.AddRange(
            new Inventargeraet { Inventarnummer = "TB-3001", Bezeichnung = "Tablet 11 Zoll", Typ = "Tablet", Standort = "A 105", Kunde = hanna, Quelle = inventar, ExterneKennung = "9001" },
            new Inventargeraet { Inventarnummer = "BE-3002", Bezeichnung = "Beamer Sitzungssaal", Typ = "Beamer", Standort = "B 001", Quelle = inventar, ExterneKennung = "9002" },
            new Inventargeraet { Inventarnummer = "TB-2990", Bezeichnung = "Tablet 10 Zoll", Typ = "Tablet", Standort = "A 107", Quelle = inventar, ExterneKennung = "9003", Aktiv = false });
        await db.SaveChangesAsync();

        var drucker = Auftrag.Erfassen(
            // Ein Gerät kommt an den Auftrag, indem es mit "INV" und seiner Inventarnummer genannt wird.
            new("Drucker im Flur druckt nicht", "Seit heute Morgen bleibt jeder Druckauftrag am Drucker im 2. OG (INV DR-0042) in der Warteschlange hängen."),
            kunde1, anna, hotline, Eingang.Telefon, jetzt.AddHours(-5));

        var wlan = Auftrag.Erfassen(
            new("WLAN im Besprechungsraum fällt aus", "Die Verbindung bricht alle paar Minuten ab.")
            {
                Dringlichkeit = Stufe.Hoch,
                Wichtigkeit = Stufe.Hoch,
            },
            kunde2, anna, hotline, Eingang.Telefon, jetzt.AddHours(-3));
        wlan.Uebernehmen(anna, jetzt.AddHours(-3).AddMinutes(5));
        wlan.Weiterleiten(netzwerk, anna, jetzt.AddHours(-3).AddMinutes(10), bernd, "Access Point in C 112 prüfen.");

        var monitor = Auftrag.Erfassen(
            new("Zweiter Monitor für neuen Arbeitsplatz", "Für den Arbeitsplatz in Raum 3.04 wird ein zweiter Monitor benötigt.")
            {
                Dringlichkeit = Stufe.Niedrig,
                Termin = DateOnly.FromDateTime(jetzt.AddDays(14)),
            },
            kunde1, anna, beschaffung, Eingang.Web, jetzt.AddDays(-1));
        monitor.Uebernehmen(clara, jetzt.AddHours(-20));

        // Zu den Testdaten gehen keine Mails hinaus.
        db.BenachrichtigungenAnlegen = false;
        db.Auftraege.AddRange(drucker, wlan, monitor);
        await db.SaveChangesAsync();
        db.BenachrichtigungenAnlegen = true;
    }
}
