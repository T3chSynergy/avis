using System.Globalization;
using Microsoft.Playwright;
using static Avis.Kurzvideos.Handgriffe;

namespace Avis.Kurzvideos;

/// <summary>
/// Nimmt die Bilder der Kurzanleitung auf: ein kleines Szenario mit den erfundenen Testdaten, an dessen Stationen
/// je ein Bildschirmfoto entsteht. Es beginnt wie die Videos mit frischen Testdaten und verändert sie.
/// </summary>
internal sealed class Bildschirmfotos(Kamera kamera, string ordner)
{
    private const int Breit = 1100, Schmal = 540;

    private async Task FotoAsync(IPage p, string datei) =>
        await p.ScreenshotAsync(new() { Path = Path.Combine(ordner, datei), FullPage = true });

    private async Task FotoAsync(ILocator ort, string datei) =>
        await ort.ScreenshotAsync(new() { Path = Path.Combine(ordner, datei) });

    /// <summary>Ein Foto vom oberen Teil eines Elements, damit zwei Bilder nebeneinander gleich hoch sind.</summary>
    private async Task AusschnittAsync(IPage p, ILocator ort, int hoehe, string datei)
    {
        await ort.ScrollIntoViewIfNeededAsync();
        var r = await ort.BoundingBoxAsync() ?? throw new InvalidOperationException($"{datei}: Das Element ist nicht zu sehen.");
        await p.ScreenshotAsync(new()
        {
            Path = Path.Combine(ordner, datei),
            FullPage = true,
            Clip = new() { X = r.X, Y = r.Y + (float)await p.EvaluateAsync<double>("() => window.scrollY"), Width = r.Width, Height = Math.Min(r.Height, hoehe) },
        });
    }

    private Task FuellenAsync(IPage p, string suche, string kunde, string betreff, string text,
        string? dringlichkeit = null, string? wichtigkeit = null, string? kategorie = null, string? termin = null,
        string? gruppe = null, string? bevorzugt = null) =>
        Handgriffe.FuellenAsync(p, kamera.Adresse, new Neu(suche, kunde, betreff, text)
        {
            Dringlichkeit = dringlichkeit,
            Wichtigkeit = wichtigkeit,
            Kategorie = kategorie,
            Termin = termin,
            Gruppe = gruppe,
            Bevorzugt = bevorzugt,
        });

    /// <summary>Klappt eine Aktion auf, füllt ihr Textfeld und sendet ab, wenn eine Schaltfläche genannt ist.</summary>
    private static async Task AktionAsync(IPage p, string name, string feld, string text, string? knopf = null)
    {
        await Aktion(p, name).ClickAsync();
        await p.FillAsync(feld, text);
        if (knopf is not null)
        {
            await p.GetByRole(AriaRole.Button, new() { Name = knopf, Exact = true }).ClickAsync();
            await p.WaitForLoadStateAsync();
        }
    }

    private static string In(int tagen) => DateTime.Today.AddDays(tagen).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async Task AufnehmenAsync()
    {
        Directory.CreateDirectory(ordner);
        var notebook = 0;

        // Bernd tritt der IT-Hotline bei.
        await kamera.FotosAsync("bernd", Breit, async p =>
        {
            await p.GotoAsync(kamera.Adresse + "/Gruppen/Meine");
            await p.Locator("tr", new() { HasText = "IT-Hotline" }).GetByRole(AriaRole.Button, new() { Name = "Beitreten" }).ClickAsync();
            await p.WaitForLoadStateAsync();
        });

        // Anna erfasst, was Bernd später in seinen Listen sieht.
        await kamera.FotosAsync("anna", Breit, async p =>
        {
            await FuellenAsync(p, "müller", "Frank Müller", "Zugriff auf das Laufwerk Finanzen",
                "Herr Müller hat die Abteilung gewechselt und braucht Lesezugriff auf das Laufwerk Finanzen. Die Abteilungsleitung hat zugestimmt.",
                kategorie: "Berechtigung", gruppe: "Systemverwaltung", bevorzugt: "Bernd Muster");
            if (await AbsendenAsync(p) != 4)
            {
                throw new InvalidOperationException("Die Bilder der Kurzanleitung brauchen frische Testdaten.");
            }

            await FuellenAsync(p, "igor", "Igor Abgleich", "Beamer im Sitzungssaal zeigt kein Bild",
                "Der Beamer (INV BE-3002) bleibt schwarz, die Lampe leuchtet. Um 16 Uhr beginnt im Saal eine Sitzung.",
                dringlichkeit: "hoch", wichtigkeit: "hoch", kategorie: "Störung");
            await AbsendenAsync(p);
            await FuellenAsync(p, "eva", "Eva Test", "Notebook startet nicht mehr",
                "Das Notebook (INV NB-1018) bleibt nach dem Einschalten beim Herstellerlogo stehen. Frau Test braucht es morgen früh für eine Schulung.",
                dringlichkeit: "hoch", kategorie: "Störung");
            notebook = await AbsendenAsync(p);
            await FuellenAsync(p, "hanna", "Hanna Verzeichnis", "Tablet verbindet sich nicht mit dem WLAN",
                "Das Tablet (INV TB-3001) findet das WLAN, meldet aber „Authentifizierung fehlgeschlagen“.",
                wichtigkeit: "niedrig", kategorie: "Störung", gruppe: "Netzwerk");
            await AbsendenAsync(p);
        });

        await kamera.FotosAsync("bernd", Breit, async p =>
        {
            // Bernd am Telefon: das ausgefüllte Formular
            await FuellenAsync(p, "müller", "Gabi Müller", "Headset für Telefonkonferenzen",
                "Frau Müller braucht ein Headset mit USB-Anschluss für ihr Notebook. Die Kostenstelle ist geklärt.",
                dringlichkeit: "niedrig", wichtigkeit: "niedrig", kategorie: "Beschaffung", termin: In(12),
                gruppe: "Beschaffung", bevorzugt: "Clara Probe");
            await p.Locator("h1").ClickAsync();
            await FotoAsync(p, "erfassen.png");
            await AbsendenAsync(p);

            // Zwei eigene Aufträge für Bernd, einer davon gleich erledigt.
            await FuellenAsync(p, "dieter", "Dieter Kunde", "Maus am Notebook defekt",
                "Die Maus am Notebook (INV NB-1017) reagiert nur noch ab und zu.",
                dringlichkeit: "niedrig", kategorie: "Störung");
            await AbsendenAsync(p, selbst: true);
            await AktionAsync(p, "Abschließen", "#abschluss-text", "Die Maus ist getauscht. Die alte war defekt.", "Auftrag abschließen");
            await FuellenAsync(p, "übung", "Özlem Übung", "Postfach für das Projekt „Umzug“ einrichten",
                "Für das Projekt wird ein gemeinsames Postfach gebraucht. Zugriff sollen Frau Übung und Herr Müller aus den Finanzen haben.",
                dringlichkeit: "niedrig", kategorie: "Anfrage", termin: In(6), gruppe: "Systemverwaltung");
            await AbsendenAsync(p, selbst: true);

            // Der Auftrag 1 der Testdaten: erst offen, dann übernommen und bearbeitet.
            await p.GotoAsync(kamera.Adresse + "/Auftraege/1");
            await FotoAsync(p, "auftrag-offen.png");
            await p.GetByRole(AriaRole.Button, new() { Name = "Übernehmen", Exact = true }).ClickAsync();
            await p.WaitForLoadStateAsync();
            await AktionAsync(p, "Interner Kommentar", "#intern-text",
                "Am Druckserver nachgesehen: Die Warteschlange hing an einem beschädigten Druckauftrag.\n\n- Warteschlange geleert\n- Druckdienst neu gestartet\n\nTestseite kommt an.",
                "Intern speichern");
            await AktionAsync(p, "Nachricht an den Kunden", "#kunde-text",
                "Guten Tag Herr Kunde,\n\nder Drucker im Flur sollte wieder drucken. Probieren Sie es bitte einmal aus und geben Sie mir kurz Bescheid?",
                "An den Kunden senden");
        });

        // Der Kunde antwortet.
        await kamera.FotosAsync("dieter", Breit, async p =>
        {
            await p.GotoAsync(kamera.Adresse + "/Meine/1");
            await p.FillAsync("#text", "Er druckt wieder, danke. Allerdings kommt jetzt alles nur noch in Schwarz-Weiß heraus.");
            await p.Locator("h1").ClickAsync();
            await FotoAsync(p, "kunde-auftrag.png");
            await p.GetByRole(AriaRole.Button, new() { Name = "Absenden" }).ClickAsync();
            await p.WaitForLoadStateAsync();
            await p.GotoAsync(kamera.Adresse + "/Meine");
            await FotoAsync(p, "kunde-meine.png");
        });

        // Bernds Sicht: der Auftrag, die Listen, Kunde, Gerät, Einstellungen
        await kamera.FotosAsync("bernd", Breit, async p =>
        {
            async Task SeiteAsync(string pfad, string datei)
            {
                await p.GotoAsync(kamera.Adresse + pfad);
                await FotoAsync(p, datei);
            }

            await SeiteAsync("/Auftraege/1", "auftrag.png");
            await SeiteAsync("/", "meine.png");
            await SeiteAsync("/Gruppen", "offene.png");
            await SeiteAsync("/Auftraege?q=wlan", "alle.png");
            await SeiteAsync("/Gruppen/Meine", "gruppen.png");
            await FotoAsync(p.Locator("header").First, "kopf.png");

            await p.GotoAsync(kamera.Adresse + "/Kunden?q=dieter");
            await p.Locator("table.liste a", new() { HasText = "Dieter Kunde" }).ClickAsync();
            await p.WaitForURLAsync(Adresse(@"Kunden/\d+$"));
            await FotoAsync(p, "kunde.png");

            await p.GotoAsync(kamera.Adresse + "/Geraete?q=TB-3001");
            await p.Locator("table.liste a", new() { HasText = "TB-3001" }).ClickAsync();
            await p.WaitForURLAsync(Adresse(@"Geraete/\d+$"));
            await FotoAsync(p, "geraet.png");

            await p.GotoAsync(kamera.Adresse + "/Einstellungen");
            await p.Locator("details.konto summary").ClickAsync();
            await FotoAsync(p, "einstellungen.png");

            // Geräte und ein anderer Auftrag, im Text genannt: die Angaben rechts am Auftrag
            await p.GotoAsync($"{kamera.Adresse}/Auftraege/{notebook}");
            await p.GetByRole(AriaRole.Button, new() { Name = "Übernehmen", Exact = true }).ClickAsync();
            await p.WaitForLoadStateAsync();
            await AktionAsync(p, "Interner Kommentar", "#intern-text",
                "Ersatzgerät INV NB-1017 ausgegeben. Der Monitor dazu läuft über AVIS 3.", "Intern speichern");
            await FotoAsync(p.Locator("aside[aria-labelledby=h-angaben]"), "bezug.png");
        });

        // Die aufgeklappten Formulare in einem schmalen Fenster: So bleibt die Schrift auch im Druck lesbar.
        await kamera.FotosAsync("bernd", Schmal, async p =>
        {
            async Task AufAsync(string name, string feld, string text, int hoehe)
            {
                await p.GotoAsync(kamera.Adresse + "/Auftraege/1");
                await Aktion(p, name).ClickAsync();
                await p.FillAsync(feld, text);
                await p.Locator(feld).EvaluateAsync("(e, h) => { e.style.height = h + 'px'; }", hoehe);
                await p.Locator("h1").ClickAsync();
            }

            await AufAsync("Interner Kommentar", "#intern-text",
                "Farbdruck ist am Gerät gesperrt, seit der Dienst neu gestartet wurde. Die Einstellung liegt auf dem Druckserver.", 120);
            await FotoAsync(p.Locator("section.aktionen"), "panel-intern.png");
            await AufAsync("Nachricht an den Kunden", "#kunde-text",
                "Danke für die Rückmeldung. Der Farbdruck war nach dem Neustart abgeschaltet. Ich gebe das an die Kollegen weiter, die den Druckserver betreuen.", 120);
            await FotoAsync(p.Locator("section.aktionen"), "panel-nachricht.png");
            await AufAsync("Weiterleiten", "#weiter-text",
                "Drucken geht wieder, aber nur schwarz-weiß. Bitte die Farbfreigabe am Druckserver prüfen.", 96);
            await p.SelectOptionAsync("#ziel", new SelectOptionValue { Label = "Systemverwaltung" });
            await p.SelectOptionAsync("#bevorzugt", new SelectOptionValue { Label = "Clara Probe" });
            await p.Locator("h1").ClickAsync();
            await FotoAsync(p.Locator("section.aktionen"), "panel-weiterleiten.png");
            await AufAsync("Abschließen", "#abschluss-text",
                "Der Drucker druckt wieder, auch in Farbe. Ursache war ein beschädigter Druckauftrag in der Warteschlange.", 96);
            await FotoAsync(p.Locator("section.aktionen"), "panel-abschliessen.png");

            // Die Vorschau an einem Textfeld: vorher und nachher
            var panel = p.Locator(".panel.intern");
            await AufAsync("Interner Kommentar", "#intern-text",
                "**Ursache:** Farbdruck war am Druckserver gesperrt.\n\n1. Freigabe wieder gesetzt\n2. Testseite in Farbe gedruckt\n\nPrüfen mit `Get-PrintConfiguration`", 170);
            await AusschnittAsync(p, panel, 300, "vorschau-1.png");
            await panel.Locator(".vorschauknopf").ClickAsync();
            await panel.Locator(".vorschau:not([hidden])").WaitForAsync();
            await p.Locator("h1").ClickAsync();
            await AusschnittAsync(p, panel, 300, "vorschau-2.png");
        });
    }
}
