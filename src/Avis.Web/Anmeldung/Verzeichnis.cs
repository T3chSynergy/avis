using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

namespace Avis.Web.Anmeldung;

/// <summary>Was ein Verzeichnis über ein Anmeldekonto weiß.</summary>
public record Verzeichniseintrag(string? Name, string? Email, string? Telefon, string? Organisationseinheit);

/// <summary>
/// Liefert zu einem Anmeldenamen Name und Kontaktdaten. Die Anmeldung selbst
/// nennt nur den Anmeldenamen.
/// </summary>
public interface IVerzeichnis
{
    /// <summary>Gibt null zurück, wenn nichts bekannt ist. Wirft keine Ausnahme.</summary>
    Verzeichniseintrag? Suchen(string anmeldename);
}

public class KeinVerzeichnis : IVerzeichnis
{
    public Verzeichniseintrag? Suchen(string anmeldename) => null;
}

/// <summary>
/// Liest aus dem Active Directory der Domäne, in der der Server Mitglied ist.
/// Läuft nur unter Windows und ist noch nicht an einem echten Verzeichnis
/// erprobt. Schlägt die Abfrage fehl, arbeitet AVIS mit dem Anmeldenamen weiter.
/// </summary>
[SupportedOSPlatform("windows")]
public class ActiveDirectoryVerzeichnis(ILogger<ActiveDirectoryVerzeichnis> log) : IVerzeichnis
{
    public Verzeichniseintrag? Suchen(string anmeldename)
    {
        try
        {
            var trenner = anmeldename.LastIndexOf('\\');
            var konto = trenner >= 0 ? anmeldename[(trenner + 1)..] : anmeldename;

            using var kontext = new PrincipalContext(ContextType.Domain);
            using var benutzer = UserPrincipal.FindByIdentity(kontext, IdentityType.SamAccountName, konto);
            if (benutzer is null)
                return null;

            var abteilung = (benutzer.GetUnderlyingObject() as DirectoryEntry)?.Properties["department"].Value as string;
            return new Verzeichniseintrag(benutzer.DisplayName, benutzer.EmailAddress, benutzer.VoiceTelephoneNumber, abteilung);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Die Abfrage des Active Directory für {Anmeldename} ist fehlgeschlagen.", anmeldename);
            return null;
        }
    }
}

public static class VerzeichnisExtensions
{
    public static void AddAvisVerzeichnis(this WebApplicationBuilder builder)
    {
        var art = builder.Configuration["Verzeichnis:Art"] ?? "Keines";
        if (art == "ActiveDirectory" && OperatingSystem.IsWindows())
            builder.Services.AddSingleton<IVerzeichnis, ActiveDirectoryVerzeichnis>();
        else if (art is "Keines" or "ActiveDirectory")
            builder.Services.AddSingleton<IVerzeichnis, KeinVerzeichnis>();
        else
            throw new InvalidOperationException($"Unbekannte Einstellung Verzeichnis:Art = {art}.");
    }
}
