using Avis.Web.Data;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Web.Pages.Kunden;

/// <summary>
/// Suche nach einem Kunden mit Auswahl eines Treffers, für die Ansichten
/// _Kundenauswahl und _Kundentreffer.
/// </summary>
public class Kundenauswahl
{
    public const int Hoechstzahl = 10;
    public const int Mindestlaenge = 2;

    public string? Suche { get; private init; }
    public int? GewaehltId { get; private init; }

    /// <summary>Ob neben den Treffern auch "Neuer Kunde" zur Wahl steht.</summary>
    public bool NeuErlaubt { get; private init; }

    public IReadOnlyList<Kunde> Treffer { get; private init; } = [];
    public bool Mehr { get; private init; }
    public bool Gesucht { get; private init; }

    public static async Task<Kundenauswahl> LadenAsync(AvisDb db, string? suche, int? gewaehltId, bool neuErlaubt)
    {
        var begriff = suche?.Trim() ?? "";
        var gesucht = begriff.Length >= Mindestlaenge;

        var treffer = new List<Kunde>();
        if (gesucht)
        {
            treffer = await db.Kunden.AsNoTracking()
                .Include(k => k.Standort)
                .Suchen(begriff)
                .OrderBy(k => k.Name)
                .Take(Hoechstzahl + 1)
                .ToListAsync();
        }
        var mehr = treffer.Count > Hoechstzahl;
        treffer = treffer.Take(Hoechstzahl).ToList();

        // Der gewählte Kunde bleibt sichtbar, auch wenn die Suche ihn nicht mehr trifft.
        if (gewaehltId is not null && treffer.All(k => k.Id != gewaehltId))
        {
            var gewaehlt = await db.Kunden.AsNoTracking().Include(k => k.Standort).FirstOrDefaultAsync(k => k.Id == gewaehltId);
            if (gewaehlt is not null)
                treffer.Insert(0, gewaehlt);
            else
                gewaehltId = null;
        }

        return new Kundenauswahl
        {
            Suche = begriff,
            GewaehltId = gewaehltId,
            NeuErlaubt = neuErlaubt,
            Treffer = treffer,
            Mehr = mehr,
            Gesucht = gesucht,
        };
    }
}
