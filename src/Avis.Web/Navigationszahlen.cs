using Avis.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web;

/// <summary>
/// Die Zahlen an den Reitern der Navigation: was ich in Bearbeitung habe und
/// was offen auf eine Übernahme wartet, in meinen Gruppen oder für mich
/// vorgemerkt.
/// </summary>
public class Navigationszahlen(AvisDb db, ILogger<Navigationszahlen> log)
{
    public record Zahlen(int Meine, int Offen);

    public async Task<Zahlen> LadenAsync(int benutzerId)
    {
        try
        {
            var sichtbar = db.AuftraegeFuer(benutzerId);
            var meine = await sichtbar.InBearbeitung().CountAsync(a => a.BearbeiterId == benutzerId);
            var offen = await sichtbar.Offen().CountAsync(a =>
                (a.Gruppe.Aktiv && a.Gruppe.Mitglieder.Any(m => m.Id == benutzerId)) || a.BevorzugterBearbeiterId == benutzerId);
            return new Zahlen(meine, offen);
        }
        catch (Exception ex)
        {
            // Die Navigation steht auch auf der Fehlerseite. Dort darf sie nicht selbst scheitern.
            log.LogWarning(ex, "Die Zahlen der Navigation konnten nicht geladen werden.");
            return new Zahlen(0, 0);
        }
    }
}
