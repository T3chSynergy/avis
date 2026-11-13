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

                string? bildtyp;
                await using (var quelle = datei.OpenReadStream())
                {
                    bildtyp = await BildtypAsync(quelle, abbruch);
                }

                var anhang = new Anhang(Bereinigt(datei.FileName), Gekuerzt(datei.ContentType, 200), datei.Length, ablagename, bildtyp);
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

    public long HoechstgroesseBytes => optionen.Value.HoechstgroesseMb * 1024L * 1024L;

    /// <summary>Speichert eine einzelne Datei, zum Beispiel den Anhang einer Mail. Die Größe prüft der Aufrufer.</summary>
    public async Task<Anhang> SpeichernAsync(string dateiname, string? inhaltstyp, Stream inhalt, CancellationToken abbruch = default)
    {
        var monat = uhr.GetUtcNow().ToString("yyyy/MM", CultureInfo.InvariantCulture);
        var ablagename = $"{monat}/{Guid.NewGuid():N}";
        var pfad = Path.Combine(Wurzel, ablagename);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);

        var bildtyp = await BildtypAsync(inhalt, abbruch);
        inhalt.Position = 0;

        await using var ziel = new FileStream(pfad, FileMode.CreateNew, FileAccess.Write);
        await inhalt.CopyToAsync(ziel, abbruch);
        return new Anhang(Bereinigt(dateiname), Gekuerzt(inhaltstyp, 200), ziel.Length, ablagename, bildtyp);
    }

    /// <summary>
    /// Erkennt an den ersten Bytes, ob die Datei ein PNG, JPEG, GIF oder WebP
    /// ist. SVG gehört bewusst nicht dazu, weil es Skripte enthalten kann.
    /// </summary>
    public static async Task<string?> BildtypAsync(Stream inhalt, CancellationToken abbruch = default)
    {
        var kopf = new byte[12];
        var gelesen = await inhalt.ReadAtLeastAsync(kopf, kopf.Length, throwOnEndOfStream: false, abbruch);
        var b = kopf.AsSpan(0, gelesen);

        if (b.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return "image/png";
        if (b.StartsWith<byte>([0xFF, 0xD8, 0xFF]))
            return "image/jpeg";
        if (b.StartsWith("GIF87a"u8) || b.StartsWith("GIF89a"u8))
            return "image/gif";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
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
