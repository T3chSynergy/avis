using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Data;

public static class Startdaten
{
    /// <summary>Bringt die Datenbank auf den aktuellen Stand und sorgt für eine Eingangsgruppe.</summary>
    public static async Task DatenbankVorbereitenAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AvisDb>();

        await db.Database.MigrateAsync();

        if (!await db.Gruppen.AnyAsync(g => g.IstEingangsgruppe))
        {
            db.Gruppen.Add(new Gruppe { Name = "IT-Hotline", IstEingangsgruppe = true });
            await db.SaveChangesAsync();
        }

        if (app.Configuration.GetValue<bool>("Testdaten:Anlegen"))
        {
            if (!app.Environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Testdaten (Testdaten:Anlegen) sind nur in der Entwicklungsumgebung erlaubt.");

            if (!await db.Benutzer.AnyAsync())
                await TestdatenAnlegenAsync(db, TimeProvider.System.GetUtcNow().UtcDateTime);
        }
    }

    private static async Task TestdatenAnlegenAsync(AvisDb db, DateTime jetzt)
    {
        var hotline = await db.Gruppen.FirstAsync(g => g.IstEingangsgruppe);
        var netzwerk = new Gruppe { Name = "Netzwerk" };
        var systeme = new Gruppe { Name = "Systemverwaltung" };
        var beschaffung = new Gruppe { Name = "Beschaffung" };
        var betreuung = new Gruppe { Name = "Anwenderbetreuung" };
        db.Gruppen.AddRange(netzwerk, systeme, beschaffung, betreuung);

        var anna = new Benutzer { Anmeldename = "anna", Name = "Anna Beispiel", IstBearbeiter = true, IstAdministrator = true };
        var bernd = new Benutzer { Anmeldename = "bernd", Name = "Bernd Muster", IstBearbeiter = true };
        var clara = new Benutzer { Anmeldename = "clara", Name = "Clara Probe", IstBearbeiter = true };
        var dieter = new Benutzer { Anmeldename = "dieter", Name = "Dieter Kunde" };
        db.Benutzer.AddRange(anna, bernd, clara, dieter);

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

        var haupthaus = new Standort { Name = "Haupthaus" };
        var nebenstelle = new Standort { Name = "Nebenstelle" };
        db.Standorte.AddRange(haupthaus, nebenstelle);

        var kunde1 = new Kunde { Name = "Dieter Kunde", Email = "dieter.kunde@example.org", Telefon = "1234", Anmeldename = "dieter", Standort = haupthaus };
        var kunde2 = new Kunde { Name = "Eva Test", Email = "eva.test@example.org", Telefon = "5678", Standort = nebenstelle };
        db.Kunden.AddRange(kunde1, kunde2);

        await db.SaveChangesAsync();

        var drucker = Auftrag.Erfassen(
            new("Drucker im Flur druckt nicht", "Seit heute Morgen bleibt jeder Druckauftrag in der Warteschlange hängen.")
            {
                Geraet = "Drucker 2. OG Flur",
                Standort = haupthaus,
            },
            kunde1, anna, hotline, Eingang.Telefon, jetzt.AddHours(-5));

        var wlan = Auftrag.Erfassen(
            new("WLAN im Besprechungsraum fällt aus", "Die Verbindung bricht alle paar Minuten ab.")
            {
                Dringlichkeit = Stufe.Hoch,
                Wichtigkeit = Stufe.Hoch,
                Standort = nebenstelle,
            },
            kunde2, anna, hotline, Eingang.Telefon, jetzt.AddHours(-3));
        wlan.Uebernehmen(anna, jetzt.AddHours(-3).AddMinutes(5));
        wlan.Weiterleiten(netzwerk, anna, jetzt.AddHours(-3).AddMinutes(10), bernd, "Access Point im Raum 1.12 prüfen.");

        var monitor = Auftrag.Erfassen(
            new("Zweiter Monitor für neuen Arbeitsplatz", "Für den Arbeitsplatz in Raum 3.04 wird ein zweiter Monitor benötigt.")
            {
                Dringlichkeit = Stufe.Niedrig,
                Termin = DateOnly.FromDateTime(jetzt.AddDays(14)),
            },
            kunde1, anna, beschaffung, Eingang.Web, jetzt.AddDays(-1));
        monitor.Uebernehmen(clara, jetzt.AddHours(-20));

        db.Auftraege.AddRange(drucker, wlan, monitor);
        await db.SaveChangesAsync();
    }
}
