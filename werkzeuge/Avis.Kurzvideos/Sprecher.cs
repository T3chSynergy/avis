using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Avis.Kurzvideos;

/// <summary>Ein gesprochener Satz: die Tondatei und ihre Länge in Sekunden.</summary>
internal sealed record Satz(string Datei, double Dauer);

/// <summary>
/// Lässt die Sätze von einer künstlichen Stimme sprechen, über die Schnittstelle von OpenRouter. Jeder Satz
/// wird im Arbeitsordner aufgehoben, damit ein zweiter Lauf ihn nicht noch einmal bezahlt.
/// </summary>
internal sealed class Sprecher(Optionen optionen) : IDisposable
{
    private const string Dienst = "https://openrouter.ai/api/v1/audio/speech";
    private const string Modell = "microsoft/mai-voice-2.1";
    private const string Stimme = "de-DE-Klaus:MAI-Voice-2.1";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };

    public void Dispose() => _http.Dispose();

    /// <summary>Gibt je Nummer die Tondatei und ihre Länge zurück; ohne Ton nichts.</summary>
    public async Task<IReadOnlyDictionary<int, Satz>> SprechenAsync(IReadOnlyList<string> saetze)
    {
        var ton = new Dictionary<int, Satz>();
        if (!optionen.MitTon)
        {
            return ton;
        }

        var ordner = Path.Combine(optionen.Arbeit, "ton");
        Directory.CreateDirectory(ordner);
        for (var i = 0; i < saetze.Count; i++)
        {
            var name = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Modell + Stimme + saetze[i])))[..16];
            var datei = Path.Combine(ordner, name + ".mp3");
            if (!File.Exists(datei))
            {
                await File.WriteAllBytesAsync(datei, await HolenAsync(saetze[i]));
            }

            ton[i + 1] = new Satz(datei, await Schnitt.DauerAsync(datei));
        }

        return ton;
    }

    private async Task<byte[]> HolenAsync(string satz)
    {
        using var anfrage = new HttpRequestMessage(HttpMethod.Post, Dienst)
        {
            Content = JsonContent.Create(new { model = Modell, input = satz, voice = Stimme, response_format = "mp3" }),
        };
        if (optionen.Schluessel is { Length: > 0 } schluessel)
        {
            anfrage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", schluessel);
        }

        using var antwort = await _http.SendAsync(anfrage);
        if (!antwort.IsSuccessStatusCode)
        {
            var grund = await antwort.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Die Sprachausgabe hat mit {(int)antwort.StatusCode} geantwortet: {grund[..Math.Min(grund.Length, 200)]}");
        }

        return await antwort.Content.ReadAsByteArrayAsync();
    }
}

/// <summary>Schneidet mit ffmpeg: Bild und Sätze zu einem MP4, dazu Standbild und Untertitel.</summary>
internal static class Schnitt
{
    /// <summary>So viel später als sein Schritt beginnt ein Satz, damit das Bild zuerst da ist.</summary>
    private const double Vorlauf = 0.25;

    public static async Task<double> DauerAsync(string datei)
    {
        var ausgabe = await StartenAsync("ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", datei);
        return double.Parse(ausgabe.Trim(), CultureInfo.InvariantCulture);
    }

    public static async Task Mp4Async(string quelle, IReadOnlyList<Marke> marken, IReadOnlyDictionary<int, Satz> ton, string ziel)
    {
        List<string> aufruf = ["-y", "-loglevel", "error", "-i", quelle];
        var gesprochen = marken.Where(m => ton.ContainsKey(m.Nr)).ToList();
        if (gesprochen.Count > 0)
        {
            var filter = new StringBuilder();
            for (var i = 0; i < gesprochen.Count; i++)
            {
                aufruf.AddRange(["-i", ton[gesprochen[i].Nr].Datei]);
                var ab = (int)Math.Round((gesprochen[i].Ab + Vorlauf) * 1000);
                filter.Append(CultureInfo.InvariantCulture, $"[{i + 1}:a]adelay={ab}:all=1[a{i}];");
            }

            filter.AppendJoin("", gesprochen.Select((_, i) => $"[a{i}]"));
            filter.Append(CultureInfo.InvariantCulture, $"amix=inputs={gesprochen.Count}:normalize=0,apad[aus]");
            aufruf.AddRange(["-filter_complex", filter.ToString(), "-map", "0:v", "-map", "[aus]",
                "-c:a", "aac", "-b:a", "128k", "-ar", "44100", "-shortest"]);
        }

        aufruf.AddRange(["-c:v", "libx264", "-preset", "slow", "-crf", "23", "-pix_fmt", "yuv420p", "-movflags", "+faststart", ziel]);
        await StartenAsync("ffmpeg", [.. aufruf]);
    }

    /// <summary>
    /// Ein GIF aus einer Aufnahme: wenige Bilder je Sekunde und eine eigene Farbpalette, damit die Datei klein
    /// bleibt. Der Anfang fällt weg, weil die Aufnahme mit einer leeren Seite beginnt.
    /// </summary>
    public static Task GifAsync(string quelle, string ziel, int breite) =>
        StartenAsync("ffmpeg", "-y", "-loglevel", "error", "-ss", "0.5", "-i", quelle, "-vf",
            string.Create(CultureInfo.InvariantCulture, $"fps=8,scale={breite}:-1:flags=lanczos,split[a][b];") +
            "[a]palettegen=max_colors=96:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle",
            "-loop", "0", ziel);

    public static Task StandbildAsync(string video, string ziel) =>
        StartenAsync("ffmpeg", "-y", "-loglevel", "error", "-ss", "1.5", "-i", video, "-frames:v", "1", "-q:v", "4", ziel);

    /// <summary>Die Sätze als Untertitel (WebVTT). Ohne Ton bleibt ein Satz stehen, bis der nächste Schritt beginnt.</summary>
    public static string Untertitel(Drehbuch drehbuch, IReadOnlyList<Marke> marken, double laenge)
    {
        var text = new StringBuilder("WEBVTT\n");
        for (var i = 0; i < marken.Count; i++)
        {
            var ab = marken[i].Ab + Vorlauf;
            var naechster = i + 1 < marken.Count ? marken[i + 1].Ab + Vorlauf : laenge;
            var bis = marken[i].Dauer > 0 ? Math.Min(ab + marken[i].Dauer, naechster) : naechster;
            text.Append(CultureInfo.InvariantCulture, $"\n{Zeit(ab)} --> {Zeit(bis)}\n{drehbuch.Saetze[marken[i].Nr - 1]}\n");
        }

        return text.ToString();
    }

    private static string Zeit(double sekunden) =>
        TimeSpan.FromSeconds(sekunden).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    private static async Task<string> StartenAsync(string programm, params string[] aufruf)
    {
        var start = new ProcessStartInfo(programm) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var teil in aufruf)
        {
            start.ArgumentList.Add(teil);
        }

        using var prozess = Process.Start(start) ?? throw new InvalidOperationException($"{programm} lässt sich nicht starten.");
        var fehler = prozess.StandardError.ReadToEndAsync();
        var ausgabe = await prozess.StandardOutput.ReadToEndAsync();
        await prozess.WaitForExitAsync();
        return prozess.ExitCode == 0
            ? ausgabe
            : throw new InvalidOperationException($"{programm} ist gescheitert: {(await fehler).Trim()}");
    }
}
