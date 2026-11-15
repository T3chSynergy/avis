using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace Avis.Web.Abgleich;

/// <summary>
/// Die Bearbeiter aus dem Active Directory: die Benutzerkonten, auf die der
/// Filter aus den Einstellungen passt, in der Regel die Mitglieder einer
/// Gruppe (siehe <see cref="ActiveDirectoryKonten"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public class ActiveDirectoryBearbeiterquelle(IOptions<BearbeiterabgleichOptionen> optionen) : IBearbeiterquelle
{
    public string Name => BearbeiterabgleichOptionen.ActiveDirectory;

    public IReadOnlyList<Quellbearbeiter> Lesen() =>
        ActiveDirectoryKonten.Lesen(optionen.Value.Filter, optionen.Value.Suchbasis)
            .Select(k => new Quellbearbeiter(k.Kennung, k.Anmeldename, k.Name, k.Email))
            .ToList();
}
