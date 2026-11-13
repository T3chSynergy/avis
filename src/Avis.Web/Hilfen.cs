using Avis.Web.Domain;
using Microsoft.Data.Sqlite;

namespace Avis.Web;

public static class Datenbankdatei
{
    /// <summary>
    /// Bezieht einen relativen Pfad der SQLite-Datei auf den Ordner der
    /// Anwendung und legt den Ordner der Datei an, falls er fehlt.
    /// </summary>
    public static string Vorbereiten(string verbindung, string basisordner)
    {
        var builder = new SqliteConnectionStringBuilder(verbindung);
        var datei = builder.DataSource;
        if (string.IsNullOrEmpty(datei) || datei.StartsWith(':'))
            return verbindung;

        builder.DataSource = Path.GetFullPath(datei, basisordner);
        Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);
        return builder.ToString();
    }
}

/// <summary>Zeitpunkte liegen in UTC vor und werden in der eingestellten Zeitzone angezeigt.</summary>
public class Ortszeit(IConfiguration konfiguration)
{
    private readonly TimeZoneInfo _zone =
        TimeZoneInfo.FindSystemTimeZoneById(konfiguration["Zeitzone"] ?? "Europe/Berlin");

    public string Anzeigen(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone).ToString("dd.MM.yyyy HH:mm");
}

public static class Anzeige
{
    private static readonly System.Globalization.CultureInfo Deutsch = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Immer in deutscher Schreibweise, auch im Hintergrund außerhalb einer Seitenanfrage.</summary>
    public static string Dateigroesse(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Byte",
        < 1024 * 1024 => string.Create(Deutsch, $"{bytes / 1024.0:0} KB"),
        _ => string.Create(Deutsch, $"{bytes / (1024.0 * 1024.0):0.0} MB"),
    };

    public static string Text(this AuftragStatus status) => status switch
    {
        AuftragStatus.Offen => "offen",
        AuftragStatus.InBearbeitung => "in Bearbeitung",
        AuftragStatus.Abgeschlossen => "abgeschlossen",
        _ => status.ToString(),
    };

    public static string Text(this Stufe stufe) => stufe switch
    {
        Stufe.Niedrig => "niedrig",
        Stufe.Mittel => "mittel",
        Stufe.Hoch => "hoch",
        _ => stufe.ToString(),
    };

    public static string Text(this Eingang eingang) => eingang switch
    {
        Eingang.Web => "Web",
        Eingang.EMail => "E-Mail",
        Eingang.Telefon => "Telefon",
        _ => eingang.ToString(),
    };

    public static string Text(this VerlaufArt art) => art switch
    {
        VerlaufArt.Erfasst => "Erfasst",
        VerlaufArt.Uebernommen => "Übernommen",
        VerlaufArt.Weitergeleitet => "Weitergeleitet",
        VerlaufArt.InternerKommentar => "Interner Kommentar",
        VerlaufArt.NachrichtAnKunden => "Nachricht an den Kunden",
        VerlaufArt.AntwortDesKunden => "Antwort des Kunden",
        VerlaufArt.Geaendert => "Geändert",
        VerlaufArt.Abgeschlossen => "Abgeschlossen",
        VerlaufArt.WiederGeoeffnet => "Wieder geöffnet",
        VerlaufArt.VomKundenWiederGeoeffnet => "Vom Kunden wieder geöffnet",
        _ => art.ToString(),
    };
}
