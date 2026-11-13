using System.Globalization;
using Avis.Web.Domain;
using Microsoft.Extensions.Options;

namespace Avis.Web.Anhaenge;

public class AnhangOptionen
{
    /// <summary>Ordner für die Dateien, relativ zum Ordner der Anwendung.</summary>
    public string Ordner { get; set; } = "App_Data/Anhaenge";

    /// <summary>Höchstgröße einer einzelnen Datei.</summary>
    public int HoechstgroesseMb { get; set; } = 10;

    /// <summary>Höchstzahl der Dateien, die auf einmal hochgeladen werden.</summary>
    public int Hoechstzahl { get; set; } = 10;
}

/// <summary>
/// Legt hochgeladene Dateien im Dateisystem ab. Den Ort vergibt AVIS selbst
/// (Jahr, Monat, zufälliger Name), der Name vom Benutzer wird nur angezeigt.
/// </summary>
public class Anhangablage(IOptions<AnhangOptionen> optionen, IWebHostEnvironment umgebung, TimeProvider uhr)
{
    private string Wurzel => Path.GetFullPath(optionen.Value.Ordner, umgebung.ContentRootPath);

    /// <summary>
    /// Prüft alle Dateien und speichert sie dann. Bei einem Verstoß wird
    /// nichts gespeichert und eine AuftragException mit der Meldung geworfen.
    /// </summary>
    public async Task<List<Anhang>> SpeichernAsync(IReadOnlyList<IFormFile>? dateien, CancellationToken abbruch = default)
    {
        var o = optionen.Value;
        if (dateien is null || dateien.Count == 0)
            return [];

        if (dateien.Count > o.Hoechstzahl)
            throw new AuftragException($"Es lassen sich höchstens {o.Hoechstzahl} Dateien auf einmal anhängen.");
        foreach (var datei in dateien)
        {
            var name = Bereinigt(datei.FileName);
            if (datei.Length == 0)
                throw new AuftragException($"Die Datei {name} ist leer.");
            if (datei.Length > o.HoechstgroesseMb * 1024L * 1024L)
                throw new AuftragException($"Die Datei {name} ist größer als {o.HoechstgroesseMb} MB.");
        }

        var anhaenge = new List<Anhang>();
        try
        {
            foreach (var datei in dateien)
            {
                // Ohne Kultur, sonst wird aus dem Schrägstrich im Deutschen ein Punkt.
                var monat = uhr.GetUtcNow().ToString("yyyy/MM", CultureInfo.InvariantCulture);
                var ablagename = $"{monat}/{Guid.NewGuid():N}";
                var pfad = Path.Combine(Wurzel, ablagename);
                Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);

                var anhang = new Anhang(Bereinigt(datei.FileName), Gekuerzt(datei.ContentType, 200), datei.Length, ablagename);
                anhaenge.Add(anhang);
                await using var ziel = new FileStream(pfad, FileMode.CreateNew, FileAccess.Write);
                await datei.CopyToAsync(ziel, abbruch);
            }
        }
        catch
        {
            Verwerfen(anhaenge);
            throw;
        }
        return anhaenge;
    }

    /// <summary>Löscht die Dateien wieder, wenn der Auftrag danach nicht gespeichert werden konnte.</summary>
    public void Verwerfen(IEnumerable<Anhang> anhaenge)
    {
        foreach (var anhang in anhaenge)
        {
            try
            {
                File.Delete(Pfad(anhang));
            }
            catch (IOException)
            {
                // Eine liegengebliebene Datei schadet nicht.
            }
        }
    }

    public string Pfad(Anhang anhang) => Path.Combine(Wurzel, anhang.Ablagename);

    /// <summary>Nur der Dateiname ohne Pfad und Steuerzeichen, höchstens 255 Zeichen.</summary>
    private static string Bereinigt(string? dateiname)
    {
        var name = (dateiname ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0)
            name = "Datei";
        return name.Length <= 255 ? name : name[..255];
    }

    private static string? Gekuerzt(string? text, int laenge) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length <= laenge ? text : text[..laenge];
}
