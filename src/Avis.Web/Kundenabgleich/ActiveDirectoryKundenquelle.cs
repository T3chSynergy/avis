using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Extensions.Options;

namespace Avis.Web.Kundenabgleich;

/// <summary>
/// Liest die Mitglieder einer Gruppe aus dem Active Directory der Domäne, in
/// der der Server Mitglied ist. Wer in der Gruppe ist, ist Kunde; verschachtelte
/// Gruppen zählen mit, deaktivierte Konten nicht.
///
/// Läuft nur unter Windows und ist noch nicht an einem echten Verzeichnis
/// erprobt. Gelesen wird mit dem Konto, unter dem AVIS läuft.
///
/// Die Gruppe muss ihre Mitglieder im Verzeichnis führen. Eine dynamische
/// Verteilergruppe von Exchange tut das nicht: Sie besteht nur aus einem
/// Filter und liefert hier keine Mitglieder. Der Abgleich ändert dann nichts.
/// </summary>
[SupportedOSPlatform("windows")]
public class ActiveDirectoryKundenquelle(IOptions<KundenabgleichOptionen> optionen) : IKundenquelle
{
    public string Name => KundenabgleichOptionen.ActiveDirectory;

    public IReadOnlyList<Quellkunde> Lesen()
    {
        var name = optionen.Value.Gruppe ?? "";
        using var kontext = new PrincipalContext(ContextType.Domain);
        using var gruppe = GroupPrincipal.FindByIdentity(kontext, name)
            ?? throw new InvalidOperationException($"Die Gruppe {name} wurde im Active Directory nicht gefunden.");

        var kunden = new List<Quellkunde>();
        foreach (var mitglied in gruppe.GetMembers(recursive: true))
        {
            using (mitglied)
            {
                if (mitglied is not UserPrincipal { Guid: { } kennung } benutzer || benutzer.Enabled == false)
                    continue;

                var abteilung = (benutzer.GetUnderlyingObject() as DirectoryEntry)?.Properties["department"].Value as string;
                kunden.Add(new Quellkunde(
                    kennung.ToString("D"),
                    Anmeldename(benutzer),
                    benutzer.DisplayName ?? benutzer.Name,
                    benutzer.EmailAddress,
                    benutzer.VoiceTelephoneNumber,
                    abteilung));
            }
        }
        return kunden;
    }

    /// <summary>Der Anmeldename in der Form "domäne\name", wie ihn die Windows-Anmeldung liefert.</summary>
    private static string? Anmeldename(UserPrincipal benutzer)
    {
        try
        {
            return benutzer.Sid.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }
}
