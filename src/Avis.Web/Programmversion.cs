using System.Reflection;

namespace Avis.Web;

/// <summary>
/// Welche Fassung von AVIS läuft. Die Nummer setzt der Workflow "Paket" beim
/// Bauen aus dem Namen des Release (v1.2.3 wird 1.2.3). Ohne Release steht
/// dort 0.0.0; dann sagt der Stand, aus welchem Commit gebaut wurde.
/// </summary>
public static class Programmversion
{
    /// <summary>Zum Anzeigen, zum Beispiel "1.2.3 (Stand 9214917)".</summary>
    public static string Text { get; } = Anzeige(
        typeof(Programmversion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Der Build schreibt "Nummer+Commit". Vom Commit genügen sieben Zeichen,
    /// so kürzt auch Git ihn ab.
    /// </summary>
    public static string Anzeige(string? angabe)
    {
        if (string.IsNullOrWhiteSpace(angabe))
            return "unbekannt";

        var teile = angabe.Trim().Split('+', 2);
        var stand = teile.Length == 2 ? teile[1].Trim() : "";
        return stand.Length == 0 ? teile[0] : $"{teile[0]} (Stand {(stand.Length > 7 ? stand[..7] : stand)})";
    }
}
