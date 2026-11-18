using System.Diagnostics;
using System.Globalization;
using Microsoft.Playwright;

namespace Avis.Kurzvideos;

/// <summary>Ein aufgenommenes Video: wo es liegt, wie lang es ist und wann welcher Satz beginnt.</summary>
internal sealed record Aufnahme(Drehbuch Drehbuch, string Datei, double Laenge, IReadOnlyList<Marke> Marken);

/// <summary>Beginn eines Schritts in Sekunden und die Länge seines gesprochenen Satzes (0 ohne Ton).</summary>
internal sealed record Marke(int Nr, double Ab, double Dauer);

/// <summary>
/// Bedient AVIS in einem Browser und nimmt dabei das Bild auf. Die Testanmeldung der Entwicklungsumgebung
/// erspart das Anmelden im Bild: Jedes Video beginnt angemeldet.
/// </summary>
internal sealed class Kamera(IBrowser browser, Optionen optionen, Sprecher sprecher) : IAsyncDisposable
{
    public const int Breite = 1280, Hoehe = 720, Band = 88;

    private static readonly string Einblendung = $"({Quelle.Lesen("Einblendung.js")})({Band});";

    public string Adresse => optionen.Adresse;

    public static async Task<Kamera> StartenAsync(IPlaywright playwright, Optionen optionen, Sprecher sprecher)
    {
        // Mit diesen Angaben beschriftet der Browser seine eigenen Teile deutsch, etwa das Dateifeld.
        Environment.SetEnvironmentVariable("LANGUAGE", "de");
        Environment.SetEnvironmentVariable("LANG", "de_DE.UTF-8");
        var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "chromium", Args = ["--lang=de-DE"] });
        return new Kamera(browser, optionen, sprecher);
    }

    public ValueTask DisposeAsync() => browser.DisposeAsync();

    private static BrowserNewContextOptions Kontext(string? zustand = null) => new()
    {
        Locale = "de-DE",
        TimezoneId = "Europe/Berlin",
        ViewportSize = new() { Width = Breite, Height = Hoehe },
        ColorScheme = ColorScheme.Light,
        StorageState = zustand,
    };

    /// <summary>Meldet einen Testbenutzer an und gibt den Zustand zurück, mit dem ein Kontext angemeldet beginnt.</summary>
    private async Task<string> AnmeldenAsync(string anmeldename)
    {
        await using var kontext = await browser.NewContextAsync(Kontext());
        var seite = await kontext.NewPageAsync();
        await seite.GotoAsync(Adresse + "/Anmeldung");
        await seite.Locator($"button[name=anmeldename][value={anmeldename}]").ClickAsync();
        await seite.WaitForLoadStateAsync();
        return await kontext.StorageStateAsync();
    }

    /// <summary>Etwas ohne Aufnahme tun, zum Beispiel Testdaten anlegen.</summary>
    public async Task<T> StillAsync<T>(string anmeldename, Func<IPage, Task<T>> tun)
    {
        await using var kontext = await browser.NewContextAsync(Kontext(await AnmeldenAsync(anmeldename)));
        return await tun(await kontext.NewPageAsync());
    }

    public Task StillAsync(string anmeldename, Func<IPage, Task> tun) =>
        StillAsync(anmeldename, async seite => { await tun(seite); return 0; });

    /// <summary>Nimmt ein Video auf und legt es als MP4 mit Standbild und Untertiteln im Zielordner ab.</summary>
    public async Task<Aufnahme> AufnehmenAsync(Drehbuch drehbuch)
    {
        var ton = await sprecher.SprechenAsync(drehbuch.Saetze);
        var roh = Path.Combine(optionen.Arbeit, "roh");
        var einstellungen = Kontext(await AnmeldenAsync(drehbuch.Anmeldename));
        einstellungen.RecordVideoDir = roh;
        einstellungen.RecordVideoSize = new() { Width = Breite, Height = Hoehe };

        var kontext = await browser.NewContextAsync(einstellungen);
        await kontext.AddInitScriptAsync(Einblendung);
        var seite = await kontext.NewPageAsync();
        seite.PageError += (_, fehler) => Console.WriteLine("  Fehler in der Seite: " + fehler);
        var film = new Film(seite, optionen, ton);

        Exception? fehler = null;
        try
        {
            await drehbuch.Ablauf(film);
            await film.Pause(700);
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException or InvalidOperationException)
        {
            fehler = e;
            await seite.ScreenshotAsync(new() { Path = Path.Combine(optionen.Ziel, drehbuch.Datei + "-fehler.png") });
        }

        var laenge = film.Sekunden;
        await kontext.CloseAsync();
        var quelle = await seite.Video!.PathAsync();
        if (fehler is not null)
        {
            throw new InvalidOperationException($"{drehbuch.Datei}: {fehler.Message.Split('\n')[0]}", fehler);
        }

        var datei = drehbuch.Datei + ".mp4";
        if (!optionen.Schnell)
        {
            await Schnitt.Mp4Async(quelle, film.Marken, ton, Path.Combine(optionen.Ziel, datei));
            await Schnitt.StandbildAsync(Path.Combine(optionen.Ziel, datei), Path.Combine(optionen.Ziel, drehbuch.Datei + ".jpg"));
            await File.WriteAllTextAsync(Path.Combine(optionen.Ziel, drehbuch.Datei + ".vtt"),
                Schnitt.Untertitel(drehbuch, film.Marken, laenge));
        }

        File.Delete(quelle);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{drehbuch.Datei}: {laenge:0.0} s, {film.Marken.Count} Schritte{(optionen.Schnell ? " (nur geprüft)" : "")}"));
        return new Aufnahme(drehbuch, datei, laenge, film.Marken);
    }
}

/// <summary>
/// Die Handgriffe eines Videos: ruhige Mausbewegung, Klick, Tippen und die Zeile im Band. Ein Schritt dauert
/// mindestens so lange wie sein gesprochener Satz.
/// </summary>
internal sealed class Film(IPage seite, Optionen optionen, IReadOnlyDictionary<int, Satz> ton)
{
    private readonly Stopwatch _uhr = Stopwatch.StartNew();
    private readonly List<Marke> _marken = [];
    private (double X, double Y) _maus = (Kamera.Breite * 0.6, Kamera.Hoehe * 0.42);

    public IPage Seite => seite;

    public IReadOnlyList<Marke> Marken => _marken;

    public double Sekunden => _uhr.Elapsed.TotalSeconds;

    /// <summary>Wartet, damit der Zuschauer folgen kann. Beim schnellen Prüfen fast nicht.</summary>
    public Task Pause(int ms) => seite.WaitForTimeoutAsync(optionen.Schnell ? Math.Min(ms, 30) : ms);

    public async Task Gehe(string pfad)
    {
        await seite.GotoAsync(optionen.Adresse + pfad);
        await seite.Mouse.MoveAsync((float)_maus.X, (float)_maus.Y);
    }

    /// <summary>Setzt die Zeile im Band am unteren Rand.</summary>
    public async Task Text(string haupt, string neben = "")
    {
        await seite.WaitForLoadStateAsync(LoadState.Load);
        await seite.Locator("#film-text").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await seite.EvaluateAsync("z => window.filmText(z[0], z[1])", new[] { haupt, neben });
    }

    /// <summary>Ein Schritt: Zeile im Band, gesprochener Satz mit dieser Nummer und die Handlung dazu.</summary>
    public async Task Schritt(int nr, string haupt, string neben, Func<Task>? tun = null)
    {
        await Text(haupt, neben);
        var dauer = ton.TryGetValue(nr, out var satz) ? satz.Dauer : 0;
        var beginn = Sekunden;
        _marken.Add(new Marke(nr, beginn, dauer));
        if (tun is not null)
        {
            await tun();
        }

        var rest = dauer + 0.55 - (Sekunden - beginn);
        if (rest > 0)
        {
            await seite.WaitForTimeoutAsync((float)(rest * 1000));
        }
    }

    /// <summary>Bewegt die Maus ruhig zum Ziel, mit sanftem Anlauf und Auslauf. Das Ziel liegt danach über dem Band.</summary>
    public async Task Zu(ILocator ort, int dauer = 650)
    {
        ort = ort.First;
        await ort.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        var art = optionen.Schnell ? "auto" : "smooth";
        await ort.EvaluateAsync("(e, art) => e.scrollIntoView({ behavior: art, block: 'center' })", art);
        var r = await RuhigAsync(ort);
        const double unten = Kamera.Hoehe - Kamera.Band - 16;
        var kante = r.Y + Math.Min(r.Height, 120);
        if (kante > unten)
        {
            await seite.EvaluateAsync("z => window.scrollBy({ top: z.weit, behavior: z.art })", new { weit = kante - unten, art });
            r = await RuhigAsync(ort);
        }

        (double X, double Y) ziel = (r.X + Math.Min(r.Width / 2, 140), r.Y + Math.Min(r.Height / 2, 60));
        var schritte = optionen.Schnell ? 3 : Math.Max(12, dauer / 16);
        for (var i = 1; i <= schritte; i++)
        {
            var t = (double)i / schritte;
            var s = t * t * (3 - (2 * t));
            await seite.Mouse.MoveAsync((float)(_maus.X + ((ziel.X - _maus.X) * s)), (float)(_maus.Y + ((ziel.Y - _maus.Y) * s)));
            if (!optionen.Schnell)
            {
                await seite.WaitForTimeoutAsync(14);
            }
        }

        _maus = ziel;
    }

    /// <summary>Wartet, bis die Seite nicht mehr rollt: Sonst trifft der Klick daneben.</summary>
    private async Task<LocatorBoundingBoxResult> RuhigAsync(ILocator ort)
    {
        var vorher = await ort.BoundingBoxAsync();
        for (var i = 0; i < 60; i++)
        {
            await seite.WaitForTimeoutAsync(70);
            var jetzt = await ort.BoundingBoxAsync();
            if (jetzt is not null && vorher is not null && Math.Abs(jetzt.Y - vorher.Y) < 0.5 && Math.Abs(jetzt.X - vorher.X) < 0.5)
            {
                return jetzt;
            }

            vorher = jetzt;
        }

        return vorher ?? throw new InvalidOperationException("Das Ziel ist nicht zu sehen.");
    }

    public async Task Klick(ILocator ort)
    {
        await Zu(ort);
        await Pause(180);
        await DrueckenAsync();
        await Pause(380);
    }

    /// <summary>Klick, der die Seite wechselt (Verweis oder Formular): wartet, bis die neue Seite geladen ist.</summary>
    public async Task Senden(ILocator ort)
    {
        await Zu(ort);
        await Pause(180);
        var geladen = new TaskCompletionSource();
        void Fertig(object? absender, IPage neueSeite) => geladen.TrySetResult();
        seite.Load += Fertig;
        try
        {
            await DrueckenAsync();
            await geladen.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            seite.Load -= Fertig;
        }

        await Pause(380);
    }

    private async Task DrueckenAsync()
    {
        await seite.Mouse.DownAsync();
        await seite.WaitForTimeoutAsync(70);
        await seite.Mouse.UpAsync();
    }

    public async Task Tippe(ILocator ort, string was, int tempo = 40)
    {
        await Klick(ort);
        await seite.Keyboard.TypeAsync(was, new() { Delay = optionen.Schnell ? 0 : tempo });
        await Pause(250);
    }

    /// <summary>Wählt einen Eintrag einer Auswahlliste. Die aufgeklappte Liste zeichnet der Browser nicht ins Bild.</summary>
    public async Task Waehle(ILocator ort, string eintrag)
    {
        await Zu(ort);
        await Pause(500);
        await ort.SelectOptionAsync(new SelectOptionValue { Label = eintrag });
        await Pause(500);
    }

    public Task Taste(string taste) => seite.Keyboard.PressAsync(taste);
}

internal static class Quelle
{
    /// <summary>Liest eine Datei, die im Werkzeug eingebettet ist.</summary>
    public static string Lesen(string name)
    {
        using var strom = typeof(Quelle).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Die eingebettete Datei {name} fehlt.");
        using var leser = new StreamReader(strom);
        return leser.ReadToEnd();
    }
}
