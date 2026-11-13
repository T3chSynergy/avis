using Avis.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web;

/// <summary>
/// Die Zahlen an den Reitern der Navigation: was bei mir liegt und was in
/// meinen Gruppen auf jemanden wartet.
/// </summary>
public class Navigationszahlen(AvisDb db, ILogger<Navigationszahlen> log)
{
    public record Zahlen(int Meine, int Gruppen);

    public async Task<Zahlen> LadenAsync(int benutzerId)
    {
        try
        {
            var sichtbar = db.AuftraegeFuer(benutzerId);
            var meine = await sichtbar.CountAsync(a => a.AbgeschlossenAm == null
                && (a.BearbeiterId == benutzerId || (a.BearbeiterId == null && a.BevorzugterBearbeiterId == benutzerId)));
            var gruppen = await sichtbar.Offen().CountAsync(a => a.Gruppe.Aktiv && a.Gruppe.Mitglieder.Any(m => m.Id == benutzerId));
            return new Zahlen(meine, gruppen);
        }
        catch (Exception ex)
        {
            // Die Navigation steht auch auf der Fehlerseite. Dort darf sie nicht selbst scheitern.
            log.LogWarning(ex, "Die Zahlen der Navigation konnten nicht geladen werden.");
            return new Zahlen(0, 0);
        }
    }
}
