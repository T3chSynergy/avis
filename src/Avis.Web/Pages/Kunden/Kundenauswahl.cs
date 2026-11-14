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

    /// <summary>
    /// Die Wahl ohne Kunden, die neben den Treffern steht: "neu" für einen
    /// neuen Kunden, "kein" für keinen Kunden, sonst gibt es keine.
    /// </summary>
    public string? Leer { get; private init; }

    /// <summary>Ob "neuer Kunde" gewählt ist. Von selbst ist er es nie.</summary>
    public bool NeuGewaehlt { get; private init; }

    /// <summary>
    /// Der Text der Wahl ohne Kunden, oder null, wenn sie nicht angeboten
    /// wird. Einen neuen Kunden legt man erst an, wenn die Suche ihn nicht
    /// gefunden hat: Die Wahl erscheint deshalb erst nach einer Suche.
    /// </summary>
    public string? Leertext => Leer switch
    {
        "neu" when Gesucht || NeuGewaehlt => "Nicht dabei: neuen Kunden anlegen",
        "kein" => "Kein Kunde",
        _ => null,
    };

    /// <summary>Ob die Wahl ohne Kunden angekreuzt ist. "Kein Kunde" ist es, solange kein Kunde gewählt ist.</summary>
    public bool LeerGewaehlt => Leer == "neu" ? NeuGewaehlt && GewaehltId is null : GewaehltId is null;

    public IReadOnlyList<Kunde> Treffer { get; private init; } = [];
    public bool Mehr { get; private init; }
    public bool Gesucht { get; private init; }

    /// <summary>Ob die Suche selbst nichts fand. Der gewählte Kunde steht auch dann in den Treffern.</summary>
    public bool NichtsGefunden { get; private init; }

    public static async Task<Kundenauswahl> LadenAsync(AvisDb db, string? suche, int? gewaehltId, string? leer = null, bool neuGewaehlt = false)
    {
        var begriff = suche?.Trim() ?? "";
        var gesucht = begriff.Length >= Mindestlaenge;

        var treffer = new List<Kunde>();
        if (gesucht)
        {
            // Inaktive Kunden stehen nicht zur Wahl. Ist einer schon gewählt, bleibt er unten sichtbar.
            treffer = await db.Kunden.AsNoTracking()
                .Where(k => k.Aktiv)
                .Suchen(begriff)
                .OrderBy(k => k.Name)
                .Take(Hoechstzahl + 1)
                .ToListAsync();
        }
        var mehr = treffer.Count > Hoechstzahl;
        var nichtsGefunden = gesucht && treffer.Count == 0;
        treffer = treffer.Take(Hoechstzahl).ToList();

        // Der gewählte Kunde bleibt sichtbar, auch wenn die Suche ihn nicht mehr trifft.
        if (gewaehltId is not null && treffer.All(k => k.Id != gewaehltId))
        {
            var gewaehlt = await db.Kunden.AsNoTracking().FirstOrDefaultAsync(k => k.Id == gewaehltId);
            if (gewaehlt is not null)
                treffer.Insert(0, gewaehlt);
            else
                gewaehltId = null;
        }

        return new Kundenauswahl
        {
            Suche = begriff,
            GewaehltId = gewaehltId,
            Leer = leer,
            NeuGewaehlt = neuGewaehlt,
            Treffer = treffer,
            Mehr = mehr,
            Gesucht = gesucht,
            NichtsGefunden = nichtsGefunden,
        };
    }
}
