using System.DirectoryServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Avis.Web.Abgleich;

/// <summary>
/// Liest die Kunden mit einem LDAP-Filter aus dem Active Directory der
/// Domäne, in der der Server Mitglied ist. Der Filter aus den Einstellungen
/// bestimmt, wer Kunde ist. AVIS ergänzt ihn selbst darum, dass nur
/// Benutzerkonten zählen und deaktivierte nicht.
///
/// Läuft nur unter Windows und ist noch nicht an einem echten Verzeichnis
/// erprobt. Gelesen wird mit dem Konto, unter dem AVIS läuft.
/// </summary>
[SupportedOSPlatform("windows")]
public class ActiveDirectoryKundenquelle(IOptions<KundenabgleichOptionen> optionen) : IKundenquelle
{
    public string Name => KundenabgleichOptionen.ActiveDirectory;

    public IReadOnlyList<Quellkunde> Lesen()
    {
        var o = optionen.Value;
        using var wurzel = string.IsNullOrWhiteSpace(o.Suchbasis) ? null : new DirectoryEntry("LDAP://" + o.Suchbasis.Trim());
        using var suche = new DirectorySearcher
        {
            Filter = o.Suchfilter,
            SearchScope = SearchScope.Subtree,
            // Ohne Seiten liefert das Verzeichnis höchstens 1000 Einträge.
            PageSize = 500,
        };
        // Ohne Suchbasis sucht das Verzeichnis in der ganzen Domäne des Servers.
        if (wurzel is not null)
            suche.SearchRoot = wurzel;
        suche.PropertiesToLoad.AddRange(
            ["objectGUID", "sAMAccountName", "msDS-PrincipalName", "displayName", "name", "mail", "telephoneNumber", "department", "physicalDeliveryOfficeName"]);

        var kunden = new List<Quellkunde>();
        using var treffer = suche.FindAll();
        foreach (SearchResult eintrag in treffer)
        {
            if (Wert(eintrag, "objectGUID") is not byte[] kennung)
                continue;

            kunden.Add(new Quellkunde(
                new Guid(kennung).ToString("D"),
                Anmeldename(eintrag),
                Text(eintrag, "displayName") ?? Text(eintrag, "name"),
                Text(eintrag, "mail"),
                Text(eintrag, "telephoneNumber"),
                Text(eintrag, "department"),
                // Das Feld "Büro" des Kontos: die Raumnummer.
                Text(eintrag, "physicalDeliveryOfficeName")));
        }
        return kunden;
    }

    /// <summary>
    /// Der Anmeldename in der Form "domäne\name", wie ihn die Windows-Anmeldung
    /// liefert. Das Verzeichnis nennt ihn in msDS-PrincipalName; fehlt der
    /// Wert, steht die Domäne des Servers davor.
    /// </summary>
    private static string? Anmeldename(SearchResult eintrag) =>
        Text(eintrag, "msDS-PrincipalName")
        ?? (Text(eintrag, "sAMAccountName") is { } konto ? $@"{Environment.UserDomainName}\{konto}" : null);

    private static object? Wert(SearchResult eintrag, string merkmal) =>
        eintrag.Properties[merkmal] is { Count: > 0 } werte ? werte[0] : null;

    private static string? Text(SearchResult eintrag, string merkmal) =>
        Wert(eintrag, merkmal) as string is { Length: > 0 } text ? text : null;
}
