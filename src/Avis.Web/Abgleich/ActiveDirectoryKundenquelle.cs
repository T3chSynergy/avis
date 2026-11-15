using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Avis.Web.Abgleich;

/// <summary>
/// Die Kunden aus dem Active Directory: die Benutzerkonten, auf die der
/// Filter aus den Einstellungen passt (siehe <see cref="ActiveDirectoryKonten"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public class ActiveDirectoryKundenquelle(IOptions<KundenabgleichOptionen> optionen) : IKundenquelle
{
    public string Name => KundenabgleichOptionen.ActiveDirectory;

    public IReadOnlyList<Quellkunde> Lesen() =>
        ActiveDirectoryKonten.Lesen(optionen.Value.Filter, optionen.Value.Suchbasis)
            .Select(k => new Quellkunde(k.Kennung, k.Anmeldename, k.Name, k.Email, k.Telefon, k.Abteilung, k.Buero))
            .ToList();
}
