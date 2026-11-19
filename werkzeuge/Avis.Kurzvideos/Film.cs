using System.Diagnostics;
using Microsoft.Playwright;

namespace Avis.Kurzvideos;

/// <summary>
/// Die Handgriffe eines Videos: ruhige Mausbewegung, Klick, Tippen und die Zeile im Band. Ein Schritt dauert
/// mindestens so lange wie sein gesprochener Satz. "Sparsam" rollt die Seite nur, wenn das Ziel sonst nicht zu
/// sehen wäre, und dann ohne Übergang: In einem GIF kostet jedes bewegte Bild Platz.
/// </summary>
internal sealed class Film(
    IPage seite, Optionen optionen, IReadOnlyDictionary<int, Satz> ton, int breite, int hoehe, int band, bool sparsam)
{
    private readonly Stopwatch _uhr = Stopwatch.StartNew();
    private readonly List<Marke> _marken = [];
    private (double X, double Y) _maus = (breite * 0.6, hoehe * 0.42);

    public IPage Seite => seite;

    public IReadOnlyList<Marke> Marken => _marken;

    public double Sekunden => _uhr.Elapsed.TotalSeconds;

    /// <summary>Hält die Uhr an, sobald der Ablauf zu Ende ist: Danach steht die Länge der Aufnahme fest.</summary>
    public void Anhalten() => _uhr.Stop();

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
        var art = optionen.Schnell || sparsam ? "auto" : "smooth";
        double unten = hoehe - band - 16;
        var sichtbar = sparsam && await ort.EvaluateAsync<bool>(
            "(e, unten) => { const r = e.getBoundingClientRect(); return r.top >= 8 && r.top + Math.min(r.height, 120) <= unten; }", unten);
        if (!sichtbar)
        {
            await ort.EvaluateAsync("(e, art) => e.scrollIntoView({ behavior: art, block: 'center' })", art);
        }

        var r = await RuhigAsync(ort);
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
