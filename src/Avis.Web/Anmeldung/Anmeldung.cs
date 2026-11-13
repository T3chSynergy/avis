using System.Globalization;
using System.Security.Claims;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Avis.Web.Anmeldung;

public class AnmeldungOptionen
{
    public const string Windows = "Windows";
    public const string Entwicklung = "Entwicklung";

    /// <summary>
    /// "Windows": Anmeldung über Active Directory ohne Passworteingabe.
    /// "Entwicklung": Testanmeldung mit Auswahl des Benutzers, nur in der
    /// Entwicklungsumgebung erlaubt.
    /// </summary>
    public string Art { get; set; } = Windows;

    /// <summary>Anmeldenamen, die bei der Anmeldung Administrator werden.</summary>
    public string[] Administratoren { get; set; } = [];
}

public static class Rollen
{
    public const string Bearbeiter = "Bearbeiter";
    public const string Administrator = "Administrator";
}

public static class Richtlinien
{
    /// <summary>
    /// Jedes aktive Konto, auch ohne Rolle. Reicht für die Seiten, auf denen
    /// jemand als Kunde eigene Aufträge stellt und sieht.
    /// </summary>
    public const string Angemeldet = "Angemeldet";
}

public static class AnmeldungExtensions
{
    private const string BenutzerIdClaim = "avis:benutzer";

    public static void AddAvisAnmeldung(this WebApplicationBuilder builder)
    {
        var abschnitt = builder.Configuration.GetSection("Anmeldung");
        builder.Services.Configure<AnmeldungOptionen>(abschnitt);
        var art = abschnitt["Art"] ?? AnmeldungOptionen.Windows;

        if (art == AnmeldungOptionen.Entwicklung)
        {
            if (!builder.Environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Die Testanmeldung (Anmeldung:Art = Entwicklung) ist nur in der Entwicklungsumgebung erlaubt.");

            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(o =>
                {
                    o.LoginPath = "/Anmeldung";
                    o.AccessDeniedPath = "/Status/403";
                });
        }
        else if (art == AnmeldungOptionen.Windows)
        {
            builder.Services
                .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                .AddNegotiate();
        }
        else
        {
            throw new InvalidOperationException($"Unbekannte Einstellung Anmeldung:Art = {art}.");
        }

        // Eine Seite ohne eigene Angabe ist nur für Bearbeiter. Was Kunden
        // oder nur Administratoren sehen, wird in Program.cs ausdrücklich genannt.
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireRole(Rollen.Bearbeiter)
                .Build())
            .AddPolicy(Richtlinien.Angemeldet, p => p.RequireClaim(BenutzerIdClaim))
            .AddPolicy(Rollen.Bearbeiter, p => p.RequireRole(Rollen.Bearbeiter))
            .AddPolicy(Rollen.Administrator, p => p.RequireRole(Rollen.Administrator));

        builder.Services.AddScoped<Kundenzuordnung>();
    }

    /// <summary>Die Id des angemeldeten Benutzers in der Benutzertabelle.</summary>
    public static int BenutzerId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(BenutzerIdClaim)
            ?? throw new InvalidOperationException("Kein angemeldeter Benutzer."), CultureInfo.InvariantCulture);

    public static string Anzeigename(this ClaimsPrincipal user) =>
        user.FindFirstValue("avis:name") ?? user.Identity?.Name ?? "";

    /// <summary>Ob ein aktives Konto angemeldet ist, mit oder ohne Rolle.</summary>
    public static bool IstAngemeldet(this ClaimsPrincipal user) => user.HasClaim(c => c.Type == BenutzerIdClaim);

    public static bool IstBearbeiter(this ClaimsPrincipal user) => user.IsInRole(Rollen.Bearbeiter);

    public static bool IstAdministrator(this ClaimsPrincipal user) => user.IsInRole(Rollen.Administrator);

    internal static ClaimsIdentity AlsIdentity(Benutzer benutzer)
    {
        var identity = new ClaimsIdentity("Avis");
        identity.AddClaim(new Claim(BenutzerIdClaim, benutzer.Id.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim("avis:name", benutzer.Name));
        if (benutzer.IstBearbeiter)
            identity.AddClaim(new Claim(ClaimTypes.Role, Rollen.Bearbeiter));
        if (benutzer.IstAdministrator)
            identity.AddClaim(new Claim(ClaimTypes.Role, Rollen.Administrator));
        return identity;
    }
}

/// <summary>
/// Ordnet dem angemeldeten Konto den Eintrag in der Benutzertabelle zu und
/// legt ihn beim ersten Besuch an. Die Rollen kommen aus dieser Tabelle, nicht
/// aus dem Anmeldeverfahren.
/// </summary>
public class BenutzerMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AvisDb db, IOptions<AnmeldungOptionen> optionen, IVerzeichnis verzeichnis)
    {
        if (context.User.Identity is { IsAuthenticated: true, Name: { Length: > 0 } name })
        {
            var anmeldename = name.Trim().ToLowerInvariant();
            var benutzer = await db.Benutzer.FirstOrDefaultAsync(b => b.Anmeldename == anmeldename);

            if (benutzer is null)
            {
                var eintrag = verzeichnis.Suchen(anmeldename);
                benutzer = new Benutzer
                {
                    Anmeldename = anmeldename,
                    Name = Bereinigen(eintrag?.Name, 200) ?? OhneDomaene(name.Trim()),
                    Email = Bereinigen(eintrag?.Email, 320)?.ToLowerInvariant(),
                };
                db.Benutzer.Add(benutzer);
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Zwei gleichzeitige erste Anfragen: Die andere war schneller.
                    db.Entry(benutzer).State = EntityState.Detached;
                    benutzer = await db.Benutzer.FirstAsync(b => b.Anmeldename == anmeldename);
                }
            }

            var sollAdministratorSein = optionen.Value.Administratoren
                .Any(a => string.Equals(a.Trim(), anmeldename, StringComparison.OrdinalIgnoreCase));
            if (sollAdministratorSein && !(benutzer.IstAdministrator && benutzer.IstBearbeiter))
            {
                benutzer.IstAdministrator = true;
                benutzer.IstBearbeiter = true;
                await db.SaveChangesAsync();
            }

            // Deaktivierte Konten bekommen keine Rollen und damit keinen Zugriff.
            if (benutzer.Aktiv)
                context.User.AddIdentity(AnmeldungExtensions.AlsIdentity(benutzer));
        }

        await next(context);
    }

    private static string? Bereinigen(string? text, int laenge)
    {
        var bereinigt = text?.Trim();
        return string.IsNullOrEmpty(bereinigt) || bereinigt.Length > laenge ? null : bereinigt;
    }

    private static string OhneDomaene(string name)
    {
        var i = name.LastIndexOf('\\');
        return i >= 0 && i < name.Length - 1 ? name[(i + 1)..] : name;
    }
}
