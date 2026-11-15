using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Avis.Web.Abgleich;

/// <summary>
/// Liest die Assets einer Installation von Snipe-IT über deren Schnittstelle
/// (GET api/v1/hardware). AVIS liest nur und schreibt nichts dorthin.
///
/// Archivierte und gelöschte Assets stehen nicht in dieser Liste; für den
/// Abgleich fehlen sie damit, und AVIS mustert sie aus.
///
/// Die Form der Antworten ist dem Quelltext von Snipe-IT entnommen.
/// </summary>
public class SnipeItGeraetequelle(HttpClient http) : IGeraetequelle
{
    /// <summary>So viele Assets je Abruf. Snipe-IT liefert von sich aus höchstens 500.</summary>
    public const int Seitengroesse = 500;

    /// <summary>Obergrenze, damit ein Fehler der Gegenseite den Abruf nicht endlos laufen lässt.</summary>
    private const int Hoechstseiten = 1000;

    /// <summary>So lange wartet AVIS auf die Antwort zu einem Abruf.</summary>
    public static readonly TimeSpan Zeitlimit = TimeSpan.FromSeconds(60);

    public string Name => GeraeteabgleichOptionen.SnipeIt;

    /// <summary>Richtet Adresse, Zugangsschlüssel und die Kopfzeilen ein, die Snipe-IT verlangt.</summary>
    public static void Einrichten(HttpClient http, GeraeteabgleichOptionen optionen)
    {
        // Der Schrägstrich am Ende sorgt dafür, dass ein Pfad in der Adresse erhalten bleibt.
        http.BaseAddress = new Uri((optionen.Adresse ?? "").Trim().TrimEnd('/') + "/");
        http.Timeout = Zeitlimit;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (optionen.Schluessel ?? "").Trim());
        // Ohne diese Zeile antwortet Snipe-IT bei einem falschen Schlüssel mit der Anmeldeseite statt mit einem Fehler.
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<Quellgeraet>> LesenAsync(CancellationToken abbruch)
    {
        var geraete = new List<Quellgeraet>();
        var offset = 0;
        for (var seite = 0; seite < Hoechstseiten; seite++)
        {
            // Nach der Nummer sortiert, damit sich die Seiten nicht verschieben, wenn während des Abrufs ein Asset dazukommt.
            var adresse = string.Create(CultureInfo.InvariantCulture, $"api/v1/hardware?limit={Seitengroesse}&offset={offset}&sort=id&order=asc");
            HttpResponseMessage antwort;
            try
            {
                antwort = await http.GetAsync(adresse, abbruch);
            }
            catch (OperationCanceledException) when (!abbruch.IsCancellationRequested)
            {
                // So meldet .NET eine Zeitüberschreitung. Ohne diese Zeile sähe sie aus wie ein Abbruch und bliebe ohne Meldung.
                throw new InvalidOperationException(
                    $"Snipe-IT hat nicht innerhalb von {Zeitlimit.TotalSeconds:0} Sekunden geantwortet (ab Asset {offset + 1}).");
            }

            using var _ = antwort;
            if (antwort.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Snipe-IT hat den Zugangsschlüssel abgelehnt.");
            if (!antwort.IsSuccessStatusCode)
                throw new InvalidOperationException($"Snipe-IT antwortet mit dem Fehler {(int)antwort.StatusCode}.");

            await using var inhalt = await antwort.Content.ReadAsStreamAsync(abbruch);
            JsonDocument json;
            try
            {
                json = await JsonDocument.ParseAsync(inhalt, cancellationToken: abbruch);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("Die Antwort von Snipe-IT ist kein JSON. Stimmt die Adresse?");
            }

            using (json)
            {
                var wurzel = json.RootElement;
                if (wurzel.ValueKind != JsonValueKind.Object
                    || !wurzel.TryGetProperty("rows", out var zeilen) || zeilen.ValueKind != JsonValueKind.Array)
                {
                    // Fehler meldet Snipe-IT oft mit dem Status 200 und einem Text in "messages".
                    var meldung = wurzel.ValueKind == JsonValueKind.Object && wurzel.TryGetProperty("messages", out var m) ? m.ToString() : null;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(meldung)
                        ? "Snipe-IT hat keine Liste von Assets geliefert."
                        : $"Snipe-IT meldet: {meldung}");
                }

                foreach (var asset in zeilen.EnumerateArray())
                {
                    if (Geraet(asset) is { } geraet)
                        geraete.Add(geraet);
                }

                var anzahl = zeilen.GetArrayLength();
                offset += anzahl;
                var gesamt = wurzel.TryGetProperty("total", out var t) && t.TryGetInt32(out var zahl) ? zahl : offset;
                if (anzahl == 0 || offset >= gesamt)
                    return geraete;
            }
        }

        throw new InvalidOperationException("Snipe-IT liefert mehr Seiten, als AVIS liest. Der Abruf wurde abgebrochen.");
    }

    private static Quellgeraet? Geraet(JsonElement asset)
    {
        if (asset.ValueKind != JsonValueKind.Object || !asset.TryGetProperty("id", out var id))
            return null;

        // Ausgegeben sein kann ein Asset an einen Benutzer, einen Standort oder ein anderes Asset. Nur ein Benutzer ist ein Nutzer.
        var nutzer = Text(asset, "assigned_to", "type") == "user" ? Text(asset, "assigned_to", "email") : null;

        return new Quellgeraet(
            id.ToString(),
            Text(asset, "asset_tag") ?? "",
            Text(asset, "name") ?? Text(asset, "model", "name"),
            Text(asset, "category", "name"),
            // Wo das Asset gerade ist, sonst sein Stammplatz.
            Text(asset, "location", "name") ?? Text(asset, "rtd_location", "name"),
            nutzer);
    }

    /// <summary>
    /// Der Text an diesem Pfad, oder null, wenn er fehlt oder leer ist.
    /// Snipe-IT liefert Texte für die Anzeige in HTML maskiert ("&amp;amp;" für "&amp;").
    /// </summary>
    private static string? Text(JsonElement element, params string[] pfad)
    {
        foreach (var name in pfad)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
                return null;
        }

        if (element.ValueKind != JsonValueKind.String)
            return null;
        var text = WebUtility.HtmlDecode(element.GetString())?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
