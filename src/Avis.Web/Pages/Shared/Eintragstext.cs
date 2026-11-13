using Avis.Web.Domain;

namespace Avis.Web.Pages.Shared;

/// <summary>
/// Ein Text mit seinen Anhängen für die Ansicht _Eintragstext. Bilder, auf die
/// der Text verweist, erscheinen an ihrer Stelle im Text und nicht noch einmal
/// in der Liste der Anhänge.
/// </summary>
/// <param name="Text">Der Text mit Markdown, oder null.</param>
/// <param name="Anhaenge">Die Anhänge, die der Leser sehen darf.</param>
/// <param name="AnhaengeAuflisten">Ob die Anhänge, die nicht im Text stehen, darunter aufgelistet werden.</param>
/// <param name="Verweistext">Ein weiterer Text, dessen Bilder nicht aufgelistet werden, etwa die Beschreibung des Auftrags.</param>
public record Eintragstext(
    string? Text,
    IReadOnlyList<Anhang> Anhaenge,
    bool AnhaengeAuflisten = true,
    string? Verweistext = null);
