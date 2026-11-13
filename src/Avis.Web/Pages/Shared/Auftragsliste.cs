using Avis.Web.Domain;

namespace Avis.Web.Pages.Shared;

/// <summary>
/// Eine Liste von Aufträgen für die Ansicht _Auftragsliste. Spalten, die auf
/// einer Seite in jeder Zeile dasselbe zeigen würden, lassen sich weglassen.
/// </summary>
public record Auftragsliste(
    IReadOnlyList<Auftrag> Auftraege,
    bool OhneKunde = false,
    bool OhneGruppe = false,
    bool OhneBearbeiter = false);
