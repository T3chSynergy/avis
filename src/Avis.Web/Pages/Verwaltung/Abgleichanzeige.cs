using Avis.Web.Abgleich;

namespace Avis.Web.Pages.Verwaltung;

/// <summary>
/// Das Ergebnis eines Abgleichs für die Ansicht _Abgleichergebnis. Kunden
/// werden inaktiv, Geräte ausgemustert: Die Wörter kommen von der Seite.
/// </summary>
public record Abgleichanzeige(Abgleichergebnis? Ergebnis, string Reaktiviert, string Inaktiv);
