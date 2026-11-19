using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Avis.Kurzvideos;

/// <summary>Angaben für einen Auftrag, der über das Formular "Auftrag erfassen" angelegt wird.</summary>
internal sealed record Neu(string Suche, string Kunde, string Betreff, string Text)
{
    public string? Dringlichkeit { get; init; }
    public string? Wichtigkeit { get; init; }
    public string? Kategorie { get; init; }
    public string? Termin { get; init; }
    public string? Gruppe { get; init; }
    public string? Bevorzugt { get; init; }
    public bool Selbst { get; init; }
}

/// <summary>
/// Was Videos und Bildschirmfotos gleichermaßen in der Oberfläche tun, ohne dass es im Bild zu sehen sein muss:
/// einen Auftrag erfassen, eine Aktion am Auftrag ansteuern.
/// </summary>
internal static class Handgriffe
{
    public static Regex Adresse(string muster) => new(muster, RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>Die Schaltfläche einer Aktion in der Leiste am Auftrag, etwa "Interner Kommentar".</summary>
    public static ILocator Aktion(IPage p, string name) => p.Locator(".leiste label.knopf", new() { HasText = name });

    public static ILocator Stufe(IPage p, string feld, string wert) =>
        p.GetByRole(AriaRole.Radiogroup, new() { Name = feld }).GetByText(wert, new() { Exact = true });

    /// <summary>Füllt das Formular "Auftrag erfassen", sendet es aber nicht ab.</summary>
    public static async Task FuellenAsync(IPage p, string anwendung, Neu a)
    {
        await p.GotoAsync(anwendung + "/Auftraege/Neu");
        await p.FillAsync("#kundensuche", a.Suche);
        await p.Locator("#kundentreffer label", new() { HasText = a.Kunde }).First.ClickAsync();
        await p.FillAsync("#Eingabe_Felder_Betreff", a.Betreff);
        await p.FillAsync("#Eingabe_Felder_Beschreibung", a.Text);
        if (a.Dringlichkeit is not null) await Stufe(p, "Dringlichkeit", a.Dringlichkeit).ClickAsync();
        if (a.Wichtigkeit is not null) await Stufe(p, "Wichtigkeit", a.Wichtigkeit).ClickAsync();
        if (a.Kategorie is not null) await p.SelectOptionAsync("#Eingabe_Felder_KategorieId", new SelectOptionValue { Label = a.Kategorie });
        if (a.Termin is not null) await p.FillAsync("#Eingabe_Felder_Termin", a.Termin);
        if (a.Gruppe is not null) await p.SelectOptionAsync("#Eingabe_GruppeId", new SelectOptionValue { Label = a.Gruppe });
        if (a.Bevorzugt is not null) await p.SelectOptionAsync("#Eingabe_BevorzugtId", new SelectOptionValue { Label = a.Bevorzugt });
    }

    /// <summary>Sendet das gefüllte Formular ab und gibt die Nummer des neuen Auftrags zurück.</summary>
    public static async Task<int> AbsendenAsync(IPage p, bool selbst = false)
    {
        if (selbst)
        {
            await p.GetByRole(AriaRole.Button, new() { Name = "Erfassen und selbst übernehmen" }).ClickAsync();
        }
        else
        {
            await p.Locator(".knoepfe button.haupt").ClickAsync();
        }

        await p.WaitForURLAsync(Adresse(@"Auftraege/\d+$"));
        return int.Parse(p.Url[(p.Url.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture);
    }

    public static async Task<int> ErfassenAsync(IPage p, string anwendung, Neu a)
    {
        await FuellenAsync(p, anwendung, a);
        return await AbsendenAsync(p, a.Selbst);
    }
}
