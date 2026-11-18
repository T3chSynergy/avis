using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Avis.Kurzvideos;

/// <summary>
/// Ein Kurzvideo: Titel und Beschreibung für die Seite, die gesprochenen Sätze und der Ablauf im Browser.
/// Der Ablauf nennt je Schritt die Nummer seines Satzes, gezählt ab 1.
/// </summary>
internal sealed record Drehbuch(
    string Datei, string Titel, string Worum, string Anmeldename, IReadOnlyList<string> Saetze, Func<Film, Task> Ablauf)
{
    /// <summary>Was vor der Aufnahme ohne Bild geschieht, etwa die Antwort eines Kunden.</summary>
    public Func<Task>? Vorher { get; init; }
}

/// <summary>
/// Die Kurzvideos, in einem durchgehenden Szenario mit den erfundenen Testdaten der Entwicklungsumgebung. Die
/// Videos bauen aufeinander auf: Was eines anlegt oder ändert, findet das nächste vor. Deshalb braucht ein Lauf
/// frische Testdaten, und die Reihenfolge steht fest.
///
/// Die gesprochenen Sätze meiden Abkürzungen und Zeichen, über die eine künstliche Stimme stolpert
/// („Steuerung und V“ statt „Strg+V“).
/// </summary>
internal sealed class Drehbuecher(Kamera kamera)
{
    private int _notebook;
    private string _bildschirmfoto = "";

    /// <summary>Angaben für einen Auftrag, der ohne Aufnahme angelegt wird.</summary>
    private sealed record Neu(string Suche, string Kunde, string Betreff, string Text)
    {
        public string? Dringlichkeit { get; init; }
        public string? Wichtigkeit { get; init; }
        public string? Kategorie { get; init; }
        public string? Termin { get; init; }
        public string? Gruppe { get; init; }
        public string? Bevorzugt { get; init; }
        public bool Selbst { get; init; }
    }

    private static Regex Adresse(string muster) => new(muster, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static ILocator Aktion(IPage s, string name) => s.Locator($".leiste label.knopf:has-text(\"{name}\")");

    private static ILocator Stufe(IPage s, string feld, string wert) =>
        s.GetByRole(AriaRole.Radiogroup, new() { Name = feld }).GetByText(wert, new() { Exact = true });

    private async Task<int> ErfassenAsync(IPage p, Neu a)
    {
        await p.GotoAsync(kamera.Adresse + "/Auftraege/Neu");
        await p.FillAsync("#kundensuche", a.Suche);
        await p.Locator("#kundentreffer label", new() { HasText = a.Kunde }).First.ClickAsync();
        await p.FillAsync("#Eingabe_Felder_Betreff", a.Betreff);
        await p.FillAsync("#Eingabe_Felder_Beschreibung", a.Text);
        if (a.Dringlichkeit is not null) await Stufe(p, "Dringlichkeit", a.Dringlichkeit).ClickAsync();
        if (a.Wichtigkeit is not null) await Stufe(p, "Wichtigkeit", a.Wichtigkeit).ClickAsync();
        if (a.Kategorie is not null) await p.SelectOptionAsync("#Eingabe_Felder_KategorieId", new SelectOptionValue { Label = a.Kategorie });
        if (a.Termin is not null) await p.FillAsync("#Eingabe_Felder_Termin", a.Termin);
        if (a.Gruppe is not null) await p.SelectOptionAsync("#Eingabe_GruppeId", new SelectOptionValue { Label = a.Gruppe });
        if (a.Bevorzugt is not null) await p.SelectOptionAsync("#Eingabe_BevorzugtId", new SelectOptionValue { Label = a.Bevorzugt });
        if (a.Selbst)
        {
            await p.GetByRole(AriaRole.Button, new() { Name = "Erfassen und selbst übernehmen" }).ClickAsync();
        }
        else
        {
            await p.Locator(".knoepfe button.haupt").ClickAsync();
        }

        await p.WaitForURLAsync(Adresse(@"Auftraege/\d+$"));
        return int.Parse(p.Url[(p.Url.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture);
    }

    /// <summary>Ergänzt die Testdaten um die Aufträge, die in den Videos vorkommen. Ohne Aufnahme.</summary>
    public async Task TestdatenAsync()
    {
        await kamera.StillAsync("anna", async p =>
        {
            var erster = await ErfassenAsync(p, new("müller", "Frank Müller", "Zugriff auf das Laufwerk Finanzen",
                "Herr Müller hat die Abteilung gewechselt und braucht Lesezugriff auf das Laufwerk Finanzen. Die Abteilungsleitung hat zugestimmt.")
            { Kategorie = "Berechtigung", Gruppe = "Systemverwaltung", Bevorzugt = "Bernd Muster" });
            if (erster != 4)
            {
                throw new InvalidOperationException(
                    $"Die Videos brauchen frische Testdaten: Der erste neue Auftrag muss die Nummer 4 bekommen, nicht {erster}.");
            }

            await ErfassenAsync(p, new("igor", "Igor Abgleich", "Beamer im Sitzungssaal zeigt kein Bild",
                "Der Beamer (INV BE-3002) bleibt schwarz, die Lampe leuchtet. Um 16 Uhr beginnt im Saal eine Sitzung.")
            { Dringlichkeit = "hoch", Wichtigkeit = "hoch", Kategorie = "Störung" });
            _notebook = await ErfassenAsync(p, new("eva", "Eva Test", "Notebook startet nicht mehr",
                "Das Notebook (INV NB-1018) bleibt nach dem Einschalten beim Herstellerlogo stehen. Frau Test braucht es morgen früh für eine Schulung.")
            { Dringlichkeit = "hoch", Kategorie = "Störung" });
            await ErfassenAsync(p, new("hanna", "Hanna Verzeichnis", "Tablet verbindet sich nicht mit dem WLAN",
                "Das Tablet (INV TB-3001) findet das WLAN, meldet aber „Authentifizierung fehlgeschlagen“.")
            { Wichtigkeit = "niedrig", Kategorie = "Störung", Gruppe = "Netzwerk" });
            await ErfassenAsync(p, new("dieter", "Dieter Kunde", "Maus am Notebook defekt", "Die Maus reagiert nur noch ab und zu.")
            { Dringlichkeit = "niedrig", Kategorie = "Störung", Selbst = true });
            await Aktion(p, "Abschließen").ClickAsync();
            await p.FillAsync("#abschluss-text", "Die Maus ist getauscht. Die alte war defekt.");
            await p.Locator(".panel[data-aktion=abschliessen] button.haupt").ClickAsync();
            await p.WaitForLoadStateAsync();
        });
        await kamera.StillAsync("bernd", p => ErfassenAsync(p, new("übung", "Özlem Übung", "Postfach für das Projekt „Umzug“ einrichten",
            "Für das Projekt wird ein gemeinsames Postfach gebraucht. Zugriff sollen Frau Übung und Herr Müller aus den Finanzen haben.")
        {
            Dringlichkeit = "niedrig",
            Kategorie = "Anfrage",
            Termin = DateTime.Today.AddDays(9).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Gruppe = "Systemverwaltung",
            Selbst = true,
        }));
    }

    public IReadOnlyList<Drehbuch> Alle() =>
    [
        Ueberblick(), MeineGruppen(), Erfassen(), Uebernehmen(), Weiterleiten(), Abschliessen(),
        Nennen(), Suchen(), Kundensicht(), KundenUndGeraete(), Texte(),
    ];

    private static Drehbuch Ueberblick() => new("01-ueberblick", "AVIS im Überblick",
        "Kopf und Navigation: wo was zu finden ist.", "bernd",
        [
            "Das ist AVIS, das Auftragssystem der IT. Oben stehen auf jeder Seite dieselben zwei Zeilen.",
            "Meine Aufträge zeigt, was du gerade selbst bearbeitest. Die Zahl daneben sagt, wie viel dort liegt.",
            "Offene Aufträge zeigt, was in deinen Gruppen liegt und noch niemand übernommen hat. Ganz oben steht, was dich als bevorzugten Bearbeiter nennt.",
            "Die Ansicht lässt sich umschalten: nach Gruppe, nach Priorität oder nach Liegezeit.",
            "Alle Aufträge ist die Suche über alles, was du sehen darfst, auch über abgeschlossene Aufträge.",
            "Daneben findest du die Kunden und die Geräte, jeweils mit ihren bisherigen Aufträgen.",
            "Ganz oben stehen das Suchfeld, Auftrag erfassen und die Hilfe. Unter deinem Namen findest du deine Gruppen und die Einstellungen.",
        ],
        async f =>
        {
            var s = f.Seite;
            ILocator Bereich(string name) => s.Locator("nav.bereiche a", new() { HasText = name });
            ILocator Ansicht(string name) => s.Locator("nav.filter a", new() { HasText = name });
            await f.Gehe("/");
            await f.Pause(400);
            await f.Schritt(1, "AVIS im Überblick", "Kopf und Navigation sind auf jeder Seite gleich", async () =>
            {
                await f.Pause(900);
                await f.Zu(s.Locator("header a.marke"));
            });
            await f.Schritt(2, "Meine Aufträge", "was du gerade bearbeitest", async () =>
            {
                await f.Zu(Bereich("Meine Aufträge"));
                await f.Pause(1200);
                await f.Zu(s.Locator("table.liste tbody tr"));
            });
            await f.Schritt(3, "Offene Aufträge", "was in deinen Gruppen liegt und noch niemand übernommen hat", async () =>
            {
                await f.Senden(Bereich("Offene Aufträge"));
                await f.Pause(900);
                await f.Zu(s.Locator("h2", new() { HasText = "Für mich vorgemerkt" }));
            });
            await f.Schritt(4, "Ansicht umschalten", "nach Gruppe, nach Priorität oder nach Liegezeit", async () =>
            {
                await f.Senden(Ansicht("Nach Priorität"));
                await f.Pause(1300);
                await f.Senden(Ansicht("Nach Liegezeit"));
            });
            await f.Schritt(5, "Alle Aufträge", "Suche und Filter über alles, was du sehen darfst", async () =>
            {
                await f.Senden(Bereich("Alle Aufträge"));
                await f.Pause(700);
                await f.Zu(s.Locator("#q"));
            });
            await f.Schritt(6, "Kunden und Geräte", "jeweils mit ihren bisherigen Aufträgen", async () =>
            {
                await f.Zu(Bereich("Kunden"));
                await f.Pause(1400);
                await f.Zu(Bereich("Geräte"));
            });
            await f.Schritt(7, "Suchfeld, Auftrag erfassen, Hilfe und dein Menü", "", async () =>
            {
                await f.Zu(s.Locator("#schnellsuche"));
                await f.Pause(900);
                await f.Zu(s.Locator("header a.neu"));
                await f.Pause(900);
                await f.Zu(s.Locator("header a.hilfe"));
                await f.Pause(900);
                await f.Klick(s.Locator("details.konto summary"));
            });
        });

    private static Drehbuch MeineGruppen() => new("02-meine-gruppen", "Meine Gruppen",
        "Gruppen bestimmen, welche offenen Aufträge man sieht.", "bernd",
        [
            "Welche Aufträge du unter Offene Aufträge siehst, bestimmst du selbst: über deine Gruppen.",
            "Du findest sie im Menü unter deinem Namen.",
            "Trag dich in jede Gruppe ein, deren Aufträge du bearbeitest oder vertrittst. Hier ist es die Hotline.",
            "Wenn du möchtest, schickt dir AVIS eine Mail, sobald in einer Gruppe ein neuer Auftrag liegt. Voreingestellt ist das aus.",
            "Austreten geht genauso. In eine gesperrte Gruppe trägt dich nur ein Administrator ein.",
        ],
        async f =>
        {
            var s = f.Seite;
            ILocator Zeile(string name) => s.Locator("table.liste tbody tr", new() { HasText = name });
            await f.Gehe("/Gruppen");
            await f.Pause(400);
            await f.Schritt(1, "Meine Gruppen", "Sie bestimmen, was du unter „Offene Aufträge“ siehst", () => f.Zu(s.Locator("h1")));
            await f.Schritt(2, "Im Menü unter deinem Namen", "", async () =>
            {
                await f.Klick(s.Locator("details.konto summary"));
                await f.Pause(500);
                await f.Senden(s.Locator("details.konto .menue a", new() { HasText = "Meine Gruppen" }));
            });
            await f.Schritt(3, "In eine Gruppe eintragen", "jede, deren Aufträge du bearbeitest oder vertrittst", async () =>
            {
                await f.Pause(1200);
                await f.Senden(Zeile("IT-Hotline").GetByRole(AriaRole.Button, new() { Name = "Beitreten" }));
            });
            await f.Schritt(4, "Mail bei neuen Aufträgen", "je Gruppe ein- und ausschaltbar, voreingestellt aus", async () =>
            {
                await f.Pause(1500);
                await f.Senden(Zeile("IT-Hotline").GetByRole(AriaRole.Button, new() { NameRegex = Adresse("(?i)einschalten") }));
            });
            await f.Schritt(5, "Austreten geht genauso", "In gesperrte Gruppen trägt dich ein Administrator ein",
                () => f.Zu(Zeile("Netzwerk").GetByRole(AriaRole.Button, new() { Name = "Austreten" })));
        });

    private static Drehbuch Erfassen() => new("03-auftrag-erfassen", "Einen Auftrag erfassen",
        "Vom Anruf zum Auftrag: Kunde, Betreff, Priorität und Gruppe.", "bernd",
        [
            "So erfasst du in AVIS einen Auftrag, zum Beispiel während eines Anrufs.",
            "Auftrag erfassen steht auf jeder Seite oben rechts. Noch schneller geht es mit Alt und N.",
            "Zuerst suchst du den Kunden: nach Name, Telefon, E-Mail oder Standort. Die Treffer erscheinen schon beim Tippen.",
            "Dann kommen Betreff und Beschreibung. Ein Gerät nennst du einfach im Text: mit dem Kürzel INV und der Inventarnummer.",
            "Dringlichkeit und Wichtigkeit ergeben zusammen die Priorität. Hier ist es die niedrigste.",
            "Vorbelegt ist die Eingangsgruppe. Ist schon klar, wer zuständig ist, gibst du den Auftrag gleich in die richtige Gruppe.",
            "Nach dem Erfassen liegt der Auftrag offen in der Gruppe. Die Kundin bekommt eine Mail mit der Auftragsnummer.",
        ],
        async f =>
        {
            var s = f.Seite;
            await f.Gehe("/");
            await f.Pause(400);
            await f.Schritt(1, "Einen Auftrag erfassen", "zum Beispiel während eines Anrufs", () => f.Pause(600));
            await f.Schritt(2, "„Auftrag erfassen“ steht auf jeder Seite oben rechts", "Tastenkürzel: Alt+N", async () =>
            {
                await f.Pause(1500);
                await f.Senden(s.Locator("header a.neu"));
            });
            await f.Schritt(3, "1. Kunde suchen", "Name, Telefon, E-Mail, Organisationseinheit oder Standort", async () =>
            {
                var treffer = s.Locator("#kundentreffer label", new() { HasText = "Gabi Müller" });
                await f.Pause(700);
                await f.Tippe(s.Locator("#kundensuche"), "müller", 120);
                await treffer.WaitForAsync();
                await f.Pause(1200);
                await f.Klick(treffer);
            });
            await f.Schritt(4, "2. Betreff und Beschreibung", "Geräte nennst du im Text: „INV“ und die Inventarnummer", async () =>
            {
                await f.Tippe(s.Locator("#Eingabe_Felder_Betreff"), "Headset für Telefonkonferenzen");
                await f.Tippe(s.Locator("#Eingabe_Felder_Beschreibung"),
                    "Frau Müller braucht ein Headset mit USB-Anschluss für ihr Notebook (INV NB-1018).", 34);
            });
            await f.Schritt(5, "3. Dringlichkeit und Wichtigkeit", "Daraus ergibt sich die Priorität, hier P5", async () =>
            {
                await f.Klick(Stufe(s, "Dringlichkeit", "niedrig"));
                await f.Klick(Stufe(s, "Wichtigkeit", "niedrig"));
            });
            await f.Schritt(6, "4. Gruppe", "Vorbelegt ist die Eingangsgruppe. Hier geht es gleich an die Beschaffung.", async () =>
            {
                await f.Pause(2200);
                await f.Waehle(s.Locator("#Eingabe_GruppeId"), "Beschaffung");
            });
            await f.Text("5. Erfassen");
            await f.Senden(s.Locator(".knoepfe button.haupt"));
            await f.Schritt(7, "Der Auftrag liegt jetzt offen in der Gruppe Beschaffung", "Die Kundin bekommt eine Mail mit der Auftragsnummer.",
                () => f.Zu(s.Locator(".zustand"), 900));
        });

    private static Drehbuch Uebernehmen() => new("04-uebernehmen-und-bearbeiten", "Übernehmen und bearbeiten",
        "Übernehmen, intern kommentieren und dem Kunden schreiben.", "bernd",
        [
            "Ein offener Auftrag gehört noch niemandem. Du findest ihn unter Offene Aufträge.",
            "Solange ihn niemand übernommen hat, kannst du ihn nur ansehen.",
            "Mit Übernehmen wirst du sein Bearbeiter. Erst jetzt kannst du kommentieren, dem Kunden schreiben, weiterleiten und abschließen.",
            "Ein interner Kommentar ist für die Kollegen. Der Kunde sieht ihn nie, auch nicht in einer Mail. Deshalb ist er gelb.",
            "Eine Nachricht an den Kunden sieht er im Browser, und er bekommt sie als Mail.",
            "Im Verlauf steht alles, was am Auftrag geschehen ist, das Neueste zuerst.",
        ],
        async f =>
        {
            var s = f.Seite;
            var drucker = s.Locator("a", new() { HasText = "Drucker im Flur druckt nicht" });
            await f.Gehe("/Gruppen");
            await f.Pause(400);
            await f.Schritt(1, "Einen offenen Auftrag übernehmen", "Du findest ihn unter „Offene Aufträge“", () => f.Zu(drucker));
            await f.Schritt(2, "Ein offener Auftrag lässt sich nur ansehen", "", async () =>
            {
                await f.Senden(drucker);
                await f.Pause(600);
                await f.Zu(s.Locator(".voraussetzung"));
            });
            await f.Schritt(3, "Übernehmen", "Damit bist du der Bearbeiter", async () =>
            {
                await f.Senden(s.GetByRole(AriaRole.Button, new() { Name = "Übernehmen", Exact = true }));
                await f.Zu(s.Locator(".zustand"));
                await f.Pause(700);
                await f.Zu(s.Locator(".leiste"));
            });
            await f.Schritt(4, "Interner Kommentar", "Der Kunde sieht ihn nie, auch nicht in einer Mail", async () =>
            {
                await f.Klick(Aktion(s, "Interner Kommentar"));
                await f.Tippe(s.Locator("#intern-text"),
                    "Am Druckserver nachgesehen: Die Warteschlange hing an einem beschädigten Auftrag. Geleert und Dienst neu gestartet.", 26);
                await f.Senden(s.Locator(".panel[data-aktion=intern] button.haupt"));
            });
            await f.Schritt(5, "Nachricht an den Kunden", "Er sieht sie im Browser und bekommt sie als Mail", async () =>
            {
                await f.Klick(Aktion(s, "Nachricht an den Kunden"));
                await f.Tippe(s.Locator("#kunde-text"),
                    "Der Drucker sollte wieder drucken. Probieren Sie es bitte aus und geben Sie mir kurz Bescheid?", 26);
                await f.Senden(s.Locator(".panel[data-aktion=nachricht] button.haupt"));
            });
            await f.Schritt(6, "Der Verlauf", "alles, was am Auftrag geschehen ist, das Neueste zuerst", async () =>
            {
                await f.Zu(s.Locator("#h-verlauf"));
                await f.Pause(900);
                await f.Zu(s.Locator("ol.verlauf li.intern"));
            });
        });

    private Drehbuch Weiterleiten() => new("05-weiterleiten", "Weiterleiten",
        "An eine andere Gruppe geben, mit Hinweis und bevorzugtem Bearbeiter.", "bernd",
        [
            "Der Kunde hat geantwortet. Das zeigt die Marke Kunde wartet, hier und in den Listen.",
            "Der Drucker druckt wieder, aber nur schwarz-weiß. Das muss die Systemverwaltung klären.",
            "Weitergeleitet wird immer an eine Gruppe, nie an eine Person.",
            "Du kannst aber einen bevorzugten Bearbeiter nennen. Das ist ein Hinweis an die Gruppe. Übernehmen kann den Auftrag trotzdem jeder.",
            "Der Hinweis an die Gruppe ist intern. Der Kunde sieht ihn nicht.",
            "Nach dem Weiterleiten liegt der Auftrag offen in der anderen Gruppe, und du bist nicht mehr sein Bearbeiter.",
        ],
        async f =>
        {
            var s = f.Seite;
            await f.Gehe("/Auftraege/1");
            await f.Pause(400);
            await f.Schritt(1, "Der Kunde hat geantwortet", "Die Marke „Kunde wartet“ steht hier und in den Listen", () => f.Zu(s.Locator(".marke-wartet")));
            await f.Schritt(2, "Das muss eine andere Gruppe klären", "", () => f.Zu(s.Locator("ol.verlauf li")));
            await f.Schritt(3, "Weiterleiten", "immer an eine Gruppe, nie an eine Person", async () =>
            {
                await f.Klick(Aktion(s, "Weiterleiten"));
                await f.Waehle(s.Locator("#ziel"), "Systemverwaltung");
            });
            await f.Schritt(4, "Bevorzugter Bearbeiter", "ein Hinweis an die Gruppe, keine Zuweisung", () => f.Waehle(s.Locator("#bevorzugt"), "Clara Probe"));
            await f.Schritt(5, "Hinweis an die Gruppe", "intern: Der Kunde sieht ihn nicht", () => f.Tippe(s.Locator("#weiter-text"),
                "Drucken geht wieder, aber nur schwarz-weiß. Bitte die Farbfreigabe am Druckserver prüfen.", 26));
            await f.Text("Weiterleiten");
            await f.Senden(s.Locator(".panel[data-aktion=weiterleiten] button.haupt"));
            await f.Schritt(6, "Der Auftrag liegt offen in der anderen Gruppe", "Du bist nicht mehr sein Bearbeiter", () => f.Zu(s.Locator(".zustand")));
        })
    {
        // Vorher antwortet der Kunde auf die Nachricht aus dem vorigen Video.
        Vorher = () => kamera.StillAsync("dieter", async p =>
        {
            await p.GotoAsync(kamera.Adresse + "/Meine/1");
            await p.FillAsync("#text", "Er druckt wieder, danke. Allerdings kommt jetzt alles nur noch in Schwarz-Weiß heraus.");
            await p.GetByRole(AriaRole.Button, new() { Name = "Absenden" }).ClickAsync();
            await p.WaitForLoadStateAsync();
        }),
    };

    private static Drehbuch Abschliessen() => new("06-abschliessen-und-wieder-oeffnen", "Abschließen und wieder öffnen",
        "Der Abschlusstext geht an den Kunden; wieder öffnen kann jeder.", "bernd",
        [
            "Ist ein Auftrag erledigt, schließt du ihn ab.",
            "Der Abschlusstext ist Pflicht. Schreib hinein, was getan wurde: Der Kunde bekommt diesen Text als Mail.",
            "Der Auftrag ist jetzt abgeschlossen und verschwindet aus deinen Listen.",
            "Fehlt doch noch etwas, lässt er sich wieder öffnen. Das kann jeder Bearbeiter, und auch der Kunde selbst.",
            "Danach liegt er wieder offen in seiner letzten Gruppe.",
        ],
        async f =>
        {
            var s = f.Seite;
            await f.Gehe("/");
            await f.Pause(400);
            await f.Schritt(1, "Einen Auftrag abschließen", "", () => f.Senden(s.Locator("a", new() { HasText = "Postfach für das Projekt" })));
            await f.Schritt(2, "Der Abschlusstext ist Pflicht", "Der Kunde bekommt ihn als Mail", async () =>
            {
                await f.Klick(Aktion(s, "Abschließen"));
                await f.Tippe(s.Locator("#abschluss-text"), "Das Postfach ist eingerichtet. Zugriff haben Frau Übung und Herr Müller.", 30);
            });
            await f.Text("Auftrag abschließen");
            await f.Senden(s.Locator(".panel[data-aktion=abschliessen] button.haupt"));
            await f.Schritt(3, "Abgeschlossen", "Der Auftrag verschwindet aus deinen Listen", () => f.Zu(s.Locator(".zustand")));
            await f.Schritt(4, "Wieder öffnen", "Das kann jeder Bearbeiter und auch der Kunde selbst", async () =>
            {
                await f.Klick(Aktion(s, "Wieder öffnen"));
                await f.Tippe(s.Locator("#oeffnen-text"), "Ein weiterer Kollege braucht Zugriff.", 30);
                await f.Senden(s.Locator(".panel[data-aktion=oeffnen] button.haupt"));
            });
            await f.Schritt(5, "Wieder offen", "in seiner letzten Gruppe", () => f.Zu(s.Locator(".zustand")));
        });

    private Drehbuch Nennen() => new("07-geraete-und-auftraege-nennen", "Geräte und Aufträge nennen",
        "Verknüpft wird, was im Text steht: „INV“ und „AVIS“ mit Nummer.", "bernd",
        [
            "Geräte und andere Aufträge verknüpfst du, indem du sie im Text nennst. Ein eigenes Feld gibt es dafür nicht.",
            "In dieser Beschreibung steht das Kürzel INV und eine Inventarnummer. AVIS kennt das Gerät und zeigt es rechts bei den Angaben.",
            "Das gilt für jeden Text, auch für einen internen Kommentar.",
            "Einen anderen Auftrag nennst du genauso: mit dem Wort AVIS und seiner Nummer.",
            "Jetzt stehen beide Geräte und der andere Auftrag bei den Angaben.",
            "Der Bezug ist nur ein Verweis. Jeder Auftrag steht für sich, mit eigener Gruppe und eigenem Abschluss. Am genannten Auftrag steht, wer ihn nennt.",
        ],
        async f =>
        {
            var s = f.Seite;
            ILocator Angabe(string text) => s.Locator("dl.angaben a", new() { HasText = text });
            await f.Gehe($"/Auftraege/{_notebook}");
            await f.Pause(400);
            await f.Schritt(1, "Geräte und Aufträge nennen", "Verknüpft wird, was im Text steht", () => f.Zu(s.Locator("h1")));
            await f.Schritt(2, "„INV“ und die Inventarnummer", "Das Gerät steht rechts bei den Angaben", async () =>
            {
                await f.Zu(s.Locator("section[aria-labelledby=h-beschreibung] .md"));
                await f.Pause(2600);
                await f.Zu(Angabe("NB-1018"));
            });
            await f.Schritt(3, "Das gilt für jeden Text", "auch für einen internen Kommentar", async () =>
            {
                await f.Senden(s.GetByRole(AriaRole.Button, new() { Name = "Übernehmen", Exact = true }));
                await f.Klick(Aktion(s, "Interner Kommentar"));
            });
            await f.Schritt(4, "„AVIS“ und die Auftragsnummer", "So nennst du einen anderen Auftrag", async () =>
            {
                await f.Tippe(s.Locator("#intern-text"), "Ersatzgerät INV NB-1017 ausgegeben. Der Monitor dazu läuft über AVIS 3.", 34);
                await f.Senden(s.Locator(".panel[data-aktion=intern] button.haupt"));
            });
            await f.Schritt(5, "Beide Geräte und der andere Auftrag", "stehen jetzt bei den Angaben", async () =>
            {
                await f.Zu(Angabe("NB-1017"));
                await f.Pause(1200);
                await f.Zu(Angabe("Auftrag 3"));
            });
            await f.Schritt(6, "Nur ein Verweis", "Jeder Auftrag steht für sich. Am genannten steht, wer ihn nennt.", async () =>
            {
                await f.Pause(1500);
                await f.Senden(Angabe("Auftrag 3"));
                await f.Zu(s.Locator("dl.angaben .hinweis", new() { HasText = "nennt diesen" }));
            });
        });

    private static Drehbuch Suchen() => new("08-suchen-und-finden", "Suchen und finden",
        "Mit der Nummer direkt zum Auftrag, mit einem Wort durch alle Texte.", "bernd",
        [
            "Kennst du die Auftragsnummer, tipp sie oben ins Suchfeld. Du landest direkt beim Auftrag.",
            "Sonst suchst du mit einem Wort. Gesucht wird in Betreff, Beschreibung, Kunde, Geräten und in allen Texten des Verlaufs.",
            "Hier findet die Suche den Auftrag über die Antwort des Kunden.",
            "Unter Alle Aufträge kannst du zusätzlich nach Status, Gruppe und Bearbeiter filtern, zum Beispiel alle abgeschlossenen Aufträge.",
            "Aufträge gesperrter Gruppen findet die Suche nur für deren Mitglieder.",
        ],
        async f =>
        {
            var s = f.Seite;
            await f.Gehe("/");
            await f.Pause(400);
            await f.Schritt(1, "Mit der Auftragsnummer direkt zum Auftrag", "Tastenkürzel für das Suchfeld: Alt+S", async () =>
            {
                await f.Tippe(s.Locator("#schnellsuche"), "1", 200);
                await f.Pause(700);
                await f.Taste("Enter");
                await s.WaitForURLAsync(Adresse("Auftraege/1$"));
            });
            await f.Schritt(2, "Mit einem Wort suchen", "in Betreff, Beschreibung, Kunde, Geräten und im ganzen Verlauf", async () =>
            {
                await f.Tippe(s.Locator("#schnellsuche"), "schwarz-weiß", 110);
                await f.Pause(600);
                await f.Taste("Enter");
                await s.WaitForURLAsync(Adresse(@"Auftraege\?q="));
            });
            await f.Schritt(3, "Gefunden über die Antwort des Kunden", "", () => f.Zu(s.Locator("table.liste tbody tr a")));
            await f.Schritt(4, "Filtern", "nach Status, Gruppe und Bearbeiter", async () =>
            {
                await s.FillAsync("#q", "");
                await f.Waehle(s.Locator("#status"), "Abgeschlossen");
                await f.Senden(s.Locator("form.suchzeile button.haupt"));
            });
            await f.Schritt(5, "Gesperrte Gruppen", "Ihre Aufträge findet die Suche nur für ihre Mitglieder", () => f.Zu(s.Locator("table.liste tbody tr")));
        });

    private static Drehbuch Kundensicht() => new("09-aus-sicht-des-kunden", "Aus Sicht des Kunden",
        "Was Kunden sehen und selbst tun können.", "dieter",
        [
            "So sehen deine Kunden AVIS: eine Liste ihrer eigenen Aufträge, oben die laufenden, darunter die abgeschlossenen.",
            "Mit Neuer Auftrag stellt der Kunde ein Anliegen. Es landet in der Eingangsgruppe.",
            "An seinem Auftrag sieht er den Stand und die Nachrichten der IT. Interne Kommentare sieht er nicht.",
            "Er kann antworten und Bilder oder Dateien nachreichen. Die IT sieht das sofort.",
            "Einen abgeschlossenen Auftrag kann er selbst wieder öffnen, wenn das Problem weiter besteht. Das alles geht auch per E-Mail.",
        ],
        async f =>
        {
            var s = f.Seite;
            await f.Gehe("/Meine");
            await f.Pause(400);
            await f.Schritt(1, "AVIS aus Sicht des Kunden", "die eigenen Aufträge: laufende und abgeschlossene", () => f.Zu(s.Locator("table.liste")));
            await f.Schritt(2, "Neuer Auftrag", "Er landet in der Eingangsgruppe", async () =>
            {
                await f.Senden(s.Locator("header a.neu"));
                await f.Tippe(s.Locator("#Eingabe_Betreff"), "Bildschirm flackert");
                await f.Tippe(s.Locator("#Eingabe_Beschreibung"), "Seit heute Morgen flackert mein Monitor, sobald das Notebook angeschlossen ist.", 26);
                await f.Senden(s.Locator(".knoepfe button.haupt"));
            });
            await f.Schritt(3, "Der eigene Auftrag", "Stand und Nachrichten der IT, keine internen Kommentare", async () =>
            {
                await f.Gehe("/Meine");
                await f.Senden(s.Locator("a", new() { HasText = "Drucker im Flur druckt nicht" }));
                await f.Zu(s.Locator("ol.verlauf"));
            });
            await f.Schritt(4, "Antworten", "auch mit Bildern und Dateien", async () =>
            {
                await f.Tippe(s.Locator("#text"), "Danke, ich warte auf Ihre Rückmeldung.", 34);
                await f.Senden(s.GetByRole(AriaRole.Button, new() { Name = "Absenden" }));
            });
            await f.Schritt(5, "Wieder öffnen", "wenn das Problem weiter besteht – alles geht auch per E-Mail", async () =>
            {
                await f.Gehe("/Meine");
                await f.Senden(s.Locator("a", new() { HasText = "Maus am Notebook defekt" }));
                await f.Zu(s.Locator("#h-antwort"));
            });
        });

    private static Drehbuch KundenUndGeraete() => new("10-kunden-und-geraete", "Kunden und Geräte",
        "Nachschlagen, was zu einem Kunden oder Gerät schon war.", "bernd",
        [
            "Unter Kunden suchst du nach Name, Telefon, E-Mail, Organisationseinheit oder Standort.",
            "Die Seite eines Kunden zeigt seine laufenden und abgeschlossenen Aufträge und seine Geräte.",
            "Von hier aus kannst du auch gleich einen Auftrag für ihn erfassen.",
            "Kunden, die aus dem Verzeichnis kommen, lassen sich in AVIS nicht ändern. Ihre Angaben werden jede Nacht abgeglichen.",
            "Auch jedes Gerät hat eine Seite: mit seinen Angaben und allen Aufträgen, in denen es genannt wurde.",
        ],
        async f =>
        {
            var s = f.Seite;
            await f.Gehe("/");
            await f.Pause(400);
            await f.Schritt(1, "Kunden nachschlagen", "Name, Telefon, E-Mail, Organisationseinheit oder Standort", async () =>
            {
                await f.Senden(s.Locator("nav.bereiche a", new() { HasText = "Kunden" }));
                await f.Tippe(s.Locator("#q"), "dieter", 110);
                await f.Taste("Enter");
                await s.WaitForURLAsync(Adresse(@"Kunden\?q="));
            });
            await f.Schritt(2, "Die Seite eines Kunden", "laufende und abgeschlossene Aufträge, seine Geräte", async () =>
            {
                await f.Senden(s.Locator("table.liste a", new() { HasText = "Dieter Kunde" }));
                await f.Pause(900);
                await f.Zu(s.Locator("h2", new() { HasText = "Abgeschlossene Aufträge" }));
            });
            await f.Schritt(3, "Auftrag für diesen Kunden erfassen", "", () => f.Zu(s.Locator("a", new() { HasText = "Auftrag für Dieter Kunde erfassen" })));
            await f.Schritt(4, "Angaben", "Kunden aus dem Verzeichnis werden jede Nacht abgeglichen", () => f.Zu(s.Locator("form.kasten h3")));
            await f.Schritt(5, "Die Seite eines Geräts", "seine Angaben und alle Aufträge, in denen es genannt wurde", async () =>
            {
                await f.Senden(s.Locator("main a", new() { HasText = "NB-1017" }));
                await f.Zu(s.Locator("table.liste"));
            });
        });

    private Drehbuch Texte() => new("11-texte-vorschau-und-bilder", "Texte, Vorschau und Bilder",
        "Formatieren mit einfachen Zeichen, Vorschau, Bildschirmfotos einfügen.", "bernd",
        [
            "Längere Texte kannst du mit einfachen Zeichen formatieren: zwei Sternchen für fett, eine Zahl mit Punkt für eine Liste.",
            "Die Vorschau zeigt, wie der Text später im Auftrag aussieht. Mit Weiter schreiben kommst du zurück.",
            "Ein Bildschirmfoto fügst du mit Steuerung und V direkt in das Textfeld ein. Es erscheint unter dem Feld.",
            "Dateien kannst du auch in das Textfeld ziehen oder über das Dateifeld auswählen.",
            "Nach dem Speichern steht der Kommentar mit dem Bild im Verlauf. Was an einem internen Kommentar hängt, sieht der Kunde nicht.",
        ],
        async f =>
        {
            var s = f.Seite;
            var vorschau = s.Locator(".panel[data-aktion=intern] .vorschauknopf");
            await f.Gehe($"/Auftraege/{_notebook}");
            await f.Pause(400);
            await f.Schritt(1, "Texte formatieren", "**fett**, Listen mit „1.“ oder „-“", async () =>
            {
                await f.Klick(Aktion(s, "Interner Kommentar"));
                await f.Tippe(s.Locator("#intern-text"),
                    "**Ursache:** Die Festplatte ist defekt.\n\n1. Daten gesichert\n2. Festplatte getauscht\n3. Notebook neu eingerichtet", 34);
            });
            await f.Schritt(2, "Vorschau", "So steht der Text später im Auftrag", async () =>
            {
                await f.Klick(vorschau);
                await s.Locator(".panel[data-aktion=intern] .vorschau:not([hidden])").WaitForAsync();
                await f.Pause(3200);
                await f.Klick(vorschau);
            });
            await f.Schritt(3, "Bildschirmfoto einfügen mit Strg+V", "Es erscheint unter dem Textfeld", async () =>
            {
                await f.Klick(s.Locator("#intern-text"));
                await f.Pause(900);
                // Die Zwischenablage des Rechners bleibt aus dem Spiel: Die Seite bekommt das Einfügen gemeldet, als käme es von dort.
                await s.EvaluateAsync(
                    """
                    bild => {
                        const bytes = Uint8Array.from(atob(bild), z => z.charCodeAt(0));
                        const sammlung = new DataTransfer();
                        sammlung.items.add(new File([bytes], 'image.png', { type: 'image/png' }));
                        document.getElementById('intern-text').dispatchEvent(
                            new ClipboardEvent('paste', { clipboardData: sammlung, bubbles: true, cancelable: true }));
                    }
                    """, _bildschirmfoto);
                await f.Zu(s.Locator(".panel[data-aktion=intern] ul.dateivorschau li"));
            });
            await f.Schritt(4, "Dateien anhängen", "in das Textfeld ziehen oder über das Dateifeld wählen", () => f.Zu(s.Locator("#intern-dateien")));
            await f.Text("Intern speichern");
            await f.Senden(s.Locator(".panel[data-aktion=intern] button.haupt"));
            await f.Schritt(5, "Kommentar mit Bild im Verlauf", "Anhänge interner Kommentare sieht der Kunde nicht", () => f.Zu(s.Locator("ol.verlauf li.intern")));
        })
    {
        // Ein „Bildschirmfoto“ zum Einfügen: eine erfundene Fehlermeldung.
        Vorher = async () => _bildschirmfoto = await kamera.StillAsync("bernd", async p =>
        {
            await p.SetViewportSizeAsync(520, 220);
            await p.SetContentAsync(
                """
                <body style="margin:0;font:15px Segoe UI,Arial,sans-serif;background:#e9edf2">
                <div style="margin:18px;background:#fff;border:1px solid #9aa5b4;border-radius:6px;box-shadow:0 3px 10px #0003">
                  <div style="background:#34495e;color:#fff;padding:8px 12px;border-radius:6px 6px 0 0">Datenträgerprüfung</div>
                  <div style="padding:16px 14px">⚠ Auf dem Datenträger wurden fehlerhafte Sektoren gefunden.<br>
                    <span style="color:#5a6573">Fehlercode 0x0007 – bitte den Datenträger ersetzen.</span></div>
                  <div style="padding:0 14px 14px;text-align:right">
                    <span style="border:1px solid #9aa5b4;border-radius:4px;padding:4px 18px">OK</span></div>
                </div>
                """);
            return Convert.ToBase64String(await p.ScreenshotAsync());
        }),
    };
}
