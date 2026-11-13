using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Anmeldung;

/// <summary>
/// Findet zu einem Anmeldekonto den Kunden, als der die Person eigene
/// Aufträge stellt und sieht.
/// </summary>
public class Kundenzuordnung(AvisDb db, IVerzeichnis verzeichnis)
{
    /// <summary>
    /// Sucht den Kunden zum Konto und legt ihn an, wenn es ihn nicht gibt.
    /// Ein Kunde, den die Hotline schon angelegt hat, wird über die
    /// E-Mail-Adresse des Kontos wiedererkannt. Diese Adresse stammt aus dem
    /// Verzeichnis oder vom Administrator, nicht von der Person selbst. So
    /// kann sich niemand die Aufträge eines anderen zuordnen.
    /// </summary>
    public async Task<Kunde> FuerBenutzerAsync(int benutzerId)
    {
        var benutzer = await db.Benutzer.FirstAsync(b => b.Id == benutzerId);

        var kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Anmeldename == benutzer.Anmeldename);
        if (kunde is not null)
            return kunde;

        if (benutzer.Email is not null)
        {
            kunde = await db.Kunden.FirstOrDefaultAsync(k => k.Email == benutzer.Email);
            if (kunde is not null && kunde.Anmeldename is null)
            {
                kunde.Anmeldename = benutzer.Anmeldename;
                await db.SaveChangesAsync();
                return kunde;
            }
        }

        // Gehört die Adresse schon einem Kunden mit anderem Konto, bleibt sie hier leer.
        var adresseFrei = kunde is null;
        var eintrag = verzeichnis.Suchen(benutzer.Anmeldename);
        kunde = new Kunde
        {
            Name = benutzer.Name,
            Email = adresseFrei ? benutzer.Email : null,
            Anmeldename = benutzer.Anmeldename,
            Telefon = Kuerzen(eintrag?.Telefon, 50),
            Organisationseinheit = Kuerzen(eintrag?.Organisationseinheit, 200),
        };
        db.Kunden.Add(kunde);
        await db.SaveChangesAsync();
        return kunde;
    }

    private static string? Kuerzen(string? text, int laenge)
    {
        var bereinigt = text?.Trim();
        return string.IsNullOrEmpty(bereinigt) ? null : bereinigt[..Math.Min(bereinigt.Length, laenge)];
    }
}
