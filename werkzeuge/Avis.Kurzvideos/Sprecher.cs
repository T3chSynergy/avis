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
