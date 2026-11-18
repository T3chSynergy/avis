// Nimmt die Kurzvideos zu AVIS auf und baut die Seite, auf der sie veröffentlicht werden.
// Aufruf und Voraussetzungen stehen in docs/entwicklung.md, Abschnitt „Kurzvideos“.
using Avis.Kurzvideos;
using Microsoft.Playwright;

if (args is ["einrichten"])
{
    // Lädt den Browser, der zur Version von Playwright passt, samt dem, was er vom System braucht.
    return Microsoft.Playwright.Program.Main(["install", "--with-deps", "chromium"]);
}

try
{
    var optionen = Optionen.Lesen(args);
    Directory.CreateDirectory(optionen.Ziel);
    Console.WriteLine(optionen.Schnell ? "Nur prüfen: ohne Ton, ohne Wartezeiten, ohne Dateien."
        : optionen.MitTon ? "Aufnahme mit Ton." : "Aufnahme ohne Ton: OPENROUTER_API_KEY ist nicht gesetzt.");

    using var playwright = await Playwright.CreateAsync();
    using var sprecher = new Sprecher(optionen);
    await using var kamera = await Kamera.StartenAsync(playwright, optionen, sprecher);
    var drehbuecher = new Drehbuecher(kamera);
    await drehbuecher.TestdatenAsync();

    var aufnahmen = new List<Aufnahme>();
    foreach (var (drehbuch, nummer) in drehbuecher.Alle().Select((d, i) => (d, i + 1)))
    {
        // Die Vorbereitung läuft auch für ausgelassene Videos, weil die folgenden auf ihr aufbauen.
        if (drehbuch.Vorher is not null)
        {
            await drehbuch.Vorher();
        }

        if (optionen.Nur.Count == 0 || optionen.Nur.Contains(nummer))
        {
            aufnahmen.Add(await kamera.AufnehmenAsync(drehbuch));
        }
    }

    if (!optionen.Schnell)
    {
        Seite.Schreiben(optionen, aufnahmen);
        Console.WriteLine($"Fertig: {Path.Combine(optionen.Ziel, "index.html")}");
    }

    return 0;
}
catch (Exception fehler) when (fehler is InvalidOperationException or PlaywrightException or HttpRequestException or ArgumentException)
{
    Console.Error.WriteLine("Abbruch: " + fehler.Message);
    return 1;
}
