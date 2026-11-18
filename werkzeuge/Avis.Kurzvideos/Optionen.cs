using System.Globalization;

namespace Avis.Kurzvideos;

/// <summary>Womit das Werkzeug aufgerufen wurde.</summary>
/// <param name="Adresse">Wo AVIS läuft, in der Entwicklungsumgebung mit frischen Testdaten.</param>
/// <param name="Ziel">Ordner für die Seite mit den Videos.</param>
/// <param name="Arbeit">Ordner für Zwischenstände: rohe Aufnahmen und gesprochene Sätze.</param>
/// <param name="Schnell">Nur prüfen, ob die Abläufe durchlaufen: ohne Ton, ohne Wartezeiten, ohne Dateien.</param>
/// <param name="Schluessel">Schlüssel für die Sprachausgabe; ohne ihn bleiben die Videos stumm.</param>
/// <param name="Stand">Was auf der Seite als Stand von AVIS steht, etwa der Name des Release.</param>
/// <param name="Bis">
/// Nummer des letzten Videos, das aufgenommen wird; 0 baut nur die Kurzanleitung. Einzelne Videos lassen sich
/// nicht herausgreifen, weil jedes auf den vorigen aufbaut.
/// </param>
internal sealed record Optionen(
    string Adresse, string Ziel, string Arbeit, bool Schnell, string? Schluessel, string Stand, int Bis)
{
    public bool MitTon => !Schnell && !string.IsNullOrWhiteSpace(Schluessel);

    /// <summary>Der Stand, wie er auf den Seiten steht.</summary>
    public string StandOderEntwicklung => Stand.Length > 0 ? Stand : "Entwicklungsstand";

    public static Optionen Lesen(string[] args)
    {
        var arbeit = Path.Combine(Path.GetTempPath(), "avis-kurzvideos");
        var optionen = new Optionen("http://localhost:5274", Path.Combine(arbeit, "seite"), arbeit,
            false, Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"), "", int.MaxValue);
        for (var i = 0; i < args.Length; i++)
        {
            string Wert() => ++i < args.Length ? args[i] : throw new ArgumentException($"Nach {args[i - 1]} fehlt die Angabe.");
            optionen = args[i] switch
            {
                "--adresse" => optionen with { Adresse = Wert().TrimEnd('/') },
                "--ziel" => optionen with { Ziel = Wert() },
                "--arbeit" => optionen with { Arbeit = Wert() },
                "--stand" => optionen with { Stand = Wert() },
                "--schnell" => optionen with { Schnell = true },
                "--bis" => optionen with { Bis = int.Parse(Wert(), CultureInfo.InvariantCulture) },
                _ => throw new ArgumentException(
                    $"Unbekannte Angabe {args[i]}. Bekannt sind --adresse, --ziel, --arbeit, --stand, --bis 3 und --schnell."),
            };
        }

        return optionen;
    }
}
