using System.DirectoryServices;
using System.Runtime.Versioning;

namespace Avis.Web.Abgleich;

/// <summary>Ein Benutzerkonto, wie es im Active Directory steht.</summary>
/// <param name="Kennung">Die objectGUID: bleibt gleich, auch wenn das Konto umbenannt wird.</param>
/// <param name="Anmeldename">In der Form "domäne\name", wie ihn die Windows-Anmeldung liefert.</param>
/// <param name="Buero">Das Feld "Büro" des Kontos (physicalDeliveryOfficeName): die Raumnummer.</param>
public record Verzeichniskonto(string Kennung, string? Anmeldename, string? Name, string? Email, string? Telefon, string? Abteilung, string? Buero);

/// <summary>
/// Liest Benutzerkonten mit einem LDAP-Filter aus dem Active Directory der
/// Domäne, in der der Server Mitglied ist. Kunden und Bearbeiter kommen auf
/// demselben Weg, nur mit verschiedenen Filtern.
///
/// Das Lesen läuft nur unter Windows. Gelesen wird mit dem Konto, unter dem
/// AVIS läuft.
/// </summary>
public static class ActiveDirectoryKonten
{
    /// <summary>
    /// Der Filter, mit dem gesucht wird: der eingestellte, eingeschränkt auf
    /// Benutzerkonten von Personen, die nicht deaktiviert sind. Die lange
    /// Zahl steht für "Bit gesetzt", die 2 für "deaktiviert".
    /// </summary>
    public static string Suchfilter(string? filter) =>
        $"(&(objectCategory=person)(objectClass=user)(!(userAccountControl:1.2.840.113556.1.4.803:=2)){filter?.Trim()})";

    /// <summary>
    /// Prüft beim Start, ob ein Filter gesetzt ist und wie einer aussieht.
    /// Ohne Filter träfe die Suche jedes Konto der Domäne; das soll niemand
    /// versehentlich einstellen. Der Name der Einstellung steht in der Meldung.
    /// </summary>
    public static void FilterPruefen(string? filter, string abschnitt)
    {
        var bereinigt = filter?.Trim() ?? "";
        if (bereinigt.Length == 0)
            throw new InvalidOperationException($"Für {abschnitt}:Art = ActiveDirectory muss {abschnitt}:Filter gesetzt sein.");
        if (!bereinigt.StartsWith('(') || !bereinigt.EndsWith(')'))
            throw new InvalidOperationException($"{abschnitt}:Filter muss ein LDAP-Filter in Klammern sein, zum Beispiel (department=*).");
    }

    /// <summary>Alle Konten, auf die der Filter passt. Ohne Suchbasis wird in der ganzen Domäne des Servers gesucht.</summary>
    [SupportedOSPlatform("windows")]
    public static List<Verzeichniskonto> Lesen(string? filter, string? suchbasis)
    {
        using var wurzel = string.IsNullOrWhiteSpace(suchbasis) ? null : new DirectoryEntry("LDAP://" + suchbasis.Trim());
        using var suche = new DirectorySearcher
        {
            Filter = Suchfilter(filter),
            SearchScope = SearchScope.Subtree,
            // Ohne Seiten liefert das Verzeichnis höchstens 1000 Einträge.
            PageSize = 500,
        };
        if (wurzel is not null)
            suche.SearchRoot = wurzel;
        suche.PropertiesToLoad.AddRange(
            ["objectGUID", "sAMAccountName", "msDS-PrincipalName", "displayName", "name", "mail", "telephoneNumber", "department", "physicalDeliveryOfficeName"]);

        var konten = new List<Verzeichniskonto>();
        using var treffer = suche.FindAll();
        foreach (SearchResult eintrag in treffer)
        {
            if (Wert(eintrag, "objectGUID") is not byte[] kennung)
                continue;

            konten.Add(new Verzeichniskonto(
                new Guid(kennung).ToString("D"),
                Anmeldename(eintrag),
                Text(eintrag, "displayName") ?? Text(eintrag, "name"),
                Text(eintrag, "mail"),
                Text(eintrag, "telephoneNumber"),
                Text(eintrag, "department"),
                Text(eintrag, "physicalDeliveryOfficeName")));
        }
        return konten;
    }

    /// <summary>
    /// Der Anmeldename in der Form "domäne\name". Das Verzeichnis nennt ihn in
    /// msDS-PrincipalName; fehlt der Wert, steht die Domäne des Servers davor.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? Anmeldename(SearchResult eintrag) =>
        Text(eintrag, "msDS-PrincipalName")
        ?? (Text(eintrag, "sAMAccountName") is { } konto ? $@"{Environment.UserDomainName}\{konto}" : null);

    [SupportedOSPlatform("windows")]
    private static object? Wert(SearchResult eintrag, string merkmal) =>
        eintrag.Properties[merkmal] is { Count: > 0 } werte ? werte[0] : null;

    [SupportedOSPlatform("windows")]
    private static string? Text(SearchResult eintrag, string merkmal) =>
        Wert(eintrag, merkmal) as string is { Length: > 0 } text ? text : null;
}
