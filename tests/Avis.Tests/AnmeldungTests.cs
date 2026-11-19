using System.Security.Claims;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Avis.Tests;

/// <summary>Wie aus dem Angemeldeten des Anmeldeverfahrens der Benutzer von AVIS mit seinen Rollen wird.</summary>
public class AnmeldungTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    /// <summary>
    /// Verhält sich wie der Angemeldete unter der Windows-Anmeldung, der in einer Windows-Gruppe mit dem Namen
    /// der Rolle ist: Er bejaht die Frage nach der Rolle selbst, ohne dass sie ihm jemand gegeben hätte.
    /// </summary>
    private sealed class MitGruppeInWindows(ClaimsIdentity identitaet, params string[] gruppen) : ClaimsPrincipal(identitaet)
    {
        public override bool IsInRole(string role) => gruppen.Contains(role) || base.IsInRole(role);
    }

    private async Task<ClaimsPrincipal> AnmeldenAsync(string anmeldename)
    {
        using var scope = app.Services.CreateScope();
        var windows = new ClaimsIdentity([new Claim(ClaimTypes.Name, anmeldename)], "Negotiate");
        var anfrage = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = new MitGruppeInWindows(windows, Rollen.Bearbeiter, Rollen.Administrator),
        };
        Assert.True(anfrage.User.IsInRole(Rollen.Administrator));

        await new BenutzerMiddleware(_ => Task.CompletedTask).InvokeAsync(
            anfrage,
            scope.ServiceProvider.GetRequiredService<AvisDb>(),
            scope.ServiceProvider.GetRequiredService<IOptions<AnmeldungOptionen>>(),
            scope.ServiceProvider.GetRequiredService<IVerzeichnis>());
        return anfrage.User;
    }

    [Fact]
    public async Task Die_Rollen_kommen_allein_aus_der_Benutzertabelle()
    {
        // Bernd ist in AVIS Bearbeiter, aber kein Administrator. Eine Windows-Gruppe "Administrator" ändert das nicht.
        var bernd = await AnmeldenAsync("bernd");
        Assert.True(bernd.IsInRole(Rollen.Bearbeiter));
        Assert.False(bernd.IsInRole(Rollen.Administrator));
        Assert.Equal("bernd", bernd.Identity!.Name);
        Assert.True(bernd.IstAngemeldet());

        // Anna ist beides.
        var anna = await AnmeldenAsync("anna");
        Assert.True(anna.IsInRole(Rollen.Bearbeiter));
        Assert.True(anna.IsInRole(Rollen.Administrator));

        // Dieter ist Kunde: angemeldet, aber ohne Rolle.
        var dieter = await AnmeldenAsync("dieter");
        Assert.False(dieter.IsInRole(Rollen.Bearbeiter));
        Assert.True(dieter.IstAngemeldet());
    }

    [Fact]
    public async Task Ein_deaktiviertes_Konto_bekommt_auch_ueber_eine_Windows_Gruppe_keine_Rolle()
    {
        await app.DatenbankAsync(async db =>
        {
            db.Benutzer.Add(new Benutzer { Anmeldename = "ausgeschieden", Name = "Aus Geschieden", IstBearbeiter = true, IstAdministrator = true, Aktiv = false });
            return await db.SaveChangesAsync();
        });

        var ausgeschieden = await AnmeldenAsync("ausgeschieden");

        Assert.False(ausgeschieden.IsInRole(Rollen.Bearbeiter));
        Assert.False(ausgeschieden.IsInRole(Rollen.Administrator));
        Assert.False(ausgeschieden.IstAngemeldet());
    }
}
