namespace Avis.Web.Domain;

public enum Stufe
{
    Niedrig = 0,
    Mittel = 1,
    Hoch = 2,
}

public enum Eingang
{
    Web = 0,
    EMail = 1,
    Telefon = 2,
}

public enum AuftragStatus
{
    Offen,
    InBearbeitung,
    Abgeschlossen,
}

/// <summary>
/// Die Angaben eines Auftrags, die beim Erfassen gesetzt und später geändert
/// werden können.
/// </summary>
public record AuftragAngaben(string Betreff, string Beschreibung)
{
    public Kategorie? Kategorie { get; init; }
    public Standort? Standort { get; init; }

    /// <summary>Freitext, z. B. eine Inventarnummer.</summary>
    public string? Geraet { get; init; }

    /// <summary>Verknüpfung mit einem Gerät aus dem Inventar, zusätzlich zum Freitext.</summary>
    public Inventargeraet? Inventargeraet { get; init; }

    public Stufe Dringlichkeit { get; init; } = Stufe.Mittel;
    public Stufe Wichtigkeit { get; init; } = Stufe.Mittel;
    public DateOnly? Termin { get; init; }
}

/// <summary>Eine Aktion ist im aktuellen Zustand des Auftrags nicht erlaubt.</summary>
public class AuftragException(string message) : Exception(message);

/// <summary>
/// Ein Auftrag gehört immer zu genau einer Gruppe und hat höchstens einen
/// Bearbeiter. Der Status ergibt sich daraus und wird nicht gespeichert.
/// Alles an einem Auftrag ändert sich nur über die Methoden dieser Klasse,
/// und jede davon schreibt einen Verlaufseintrag.
///
/// Übernehmen darf jeder Bearbeiter. Alles Weitere setzt das Übernehmen
/// voraus: Nur wer den Auftrag bearbeitet, leitet ihn weiter, kommentiert
/// ihn, schreibt dem Kunden, ändert Angaben oder schließt ihn ab.
/// </summary>
public class Auftrag
{
    private readonly List<Verlaufseintrag> _verlauf = [];

    private Auftrag() { }

    /// <summary>Die Auftragsnummer.</summary>
    public int Id { get; private set; }

    public string Betreff { get; private set; } = "";
    public string Beschreibung { get; private set; } = "";

    public int KundeId { get; private set; }
    public Kunde Kunde { get; private set; } = null!;

    public int ErfasstVonId { get; private set; }
    public Benutzer ErfasstVon { get; private set; } = null!;

    public Eingang Eingang { get; private set; }

    public int? KategorieId { get; private set; }
    public Kategorie? Kategorie { get; private set; }

    public int? StandortId { get; private set; }
    public Standort? Standort { get; private set; }

    /// <summary>Freitext, z. B. eine Inventarnummer.</summary>
    public string? Geraet { get; private set; }

    public int? InventargeraetId { get; private set; }
    public Inventargeraet? Inventargeraet { get; private set; }

    public Stufe Dringlichkeit { get; private set; } = Stufe.Mittel;
    public Stufe Wichtigkeit { get; private set; } = Stufe.Mittel;
    public DateOnly? Termin { get; private set; }

    public int GruppeId { get; private set; }
    public Gruppe Gruppe { get; private set; } = null!;

    public int? BearbeiterId { get; private set; }
    public Benutzer? Bearbeiter { get; private set; }

    public int? BevorzugterBearbeiterId { get; private set; }
    public Benutzer? BevorzugterBearbeiter { get; private set; }

    public DateTime ErstelltAm { get; private set; }
    public DateTime GeaendertAm { get; private set; }
    public DateTime? AbgeschlossenAm { get; private set; }

    /// <summary>
    /// Seit wann der Kunde auf eine Reaktion wartet: gesetzt, wenn er antwortet
    /// oder den Auftrag wieder öffnet, und geleert, sobald die IT ihm schreibt
    /// oder den Auftrag abschließt.
    /// </summary>
    public DateTime? KundeWartetSeit { get; private set; }

    /// <summary>
    /// Zählt hoch, wenn sich Gruppe, Bearbeiter, Abschluss oder Angaben
    /// ändern. Damit fällt auf, wenn jemand auf einem veralteten Stand der
    /// Seite eine Aktion auslöst. Kommentare zählen nicht.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Betreff, Beschreibung und Gerät in Kleinbuchstaben. Die Suche
    /// vergleicht nur mit diesem Feld und verhält sich dadurch in jeder
    /// Datenbank gleich, auch bei Umlauten.
    /// </summary>
    public string Suchtext { get; private set; } = "";

    public IReadOnlyList<Verlaufseintrag> Verlauf => _verlauf;

    public AuftragStatus Status =>
        AbgeschlossenAm is not null ? AuftragStatus.Abgeschlossen
        : BearbeiterId is not null ? AuftragStatus.InBearbeitung
        : AuftragStatus.Offen;

    /// <summary>1 (höchste) bis 5 (niedrigste), aus Dringlichkeit und Wichtigkeit.</summary>
    public int Prioritaet => 5 - (int)Dringlichkeit - (int)Wichtigkeit;

    public static Auftrag Erfassen(
        AuftragAngaben angaben,
        Kunde kunde,
        Benutzer erfasstVon,
        Gruppe gruppe,
        Eingang eingang,
        DateTime jetzt,
        IReadOnlyList<Anhang>? anhaenge = null)
    {
        var auftrag = new Auftrag
        {
            Kunde = kunde,
            KundeId = kunde.Id,
            ErfasstVon = erfasstVon,
            ErfasstVonId = erfasstVon.Id,
            Gruppe = gruppe,
            GruppeId = gruppe.Id,
            Eingang = eingang,
            ErstelltAm = jetzt,
            GeaendertAm = jetzt,
        };
        auftrag.Setzen(Bereinigt(angaben));
        auftrag.Eintragen(VerlaufArt.Erfasst, erfasstVon, jetzt, $"Erfasst in Gruppe {gruppe.Name}", anhaenge: anhaenge);
        return auftrag;
    }

    /// <summary>
    /// Ändert die Angaben und hält im Verlauf fest, was vorher galt.
    /// Gibt false zurück, wenn sich nichts geändert hat.
    /// </summary>
    public bool Aendern(AuftragAngaben angaben, Benutzer durch, DateTime jetzt)
    {
        NichtAbgeschlossen();
        NurZustaendig(durch);
        var neu = Bereinigt(angaben);

        var aenderungen = new List<string>();
        void Vergleichen(string feld, string? alt, string? wert, bool geaendert)
        {
            if (geaendert)
                aenderungen.Add($"{feld}: {alt ?? "–"} → {wert ?? "–"}");
        }

        Vergleichen("Betreff", Betreff, neu.Betreff, Betreff != neu.Betreff);
        Vergleichen("Kategorie", Kategorie?.Name, neu.Kategorie?.Name, KategorieId != neu.Kategorie?.Id);
        Vergleichen("Standort", Standort?.Name, neu.Standort?.Name, StandortId != neu.Standort?.Id);
        Vergleichen("Gerät", Geraet, neu.Geraet, Geraet != neu.Geraet);
        Vergleichen("Gerät aus dem Inventar", Inventargeraet?.Inventarnummer, neu.Inventargeraet?.Inventarnummer, InventargeraetId != neu.Inventargeraet?.Id);
        Vergleichen("Dringlichkeit", Dringlichkeit.Text(), neu.Dringlichkeit.Text(), Dringlichkeit != neu.Dringlichkeit);
        Vergleichen("Wichtigkeit", Wichtigkeit.Text(), neu.Wichtigkeit.Text(), Wichtigkeit != neu.Wichtigkeit);
        Vergleichen("Termin", Termin?.ToString("dd.MM.yyyy"), neu.Termin?.ToString("dd.MM.yyyy"), Termin != neu.Termin);

        // Der alte Text der Beschreibung bleibt im Verlauf erhalten.
        string? bisherigeBeschreibung = null;
        if (Beschreibung != neu.Beschreibung)
        {
            aenderungen.Add("Beschreibung geändert. Bisheriger Text:");
            bisherigeBeschreibung = Beschreibung;
        }

        if (aenderungen.Count == 0)
            return false;

        Setzen(neu);
        Eintragen(VerlaufArt.Geaendert, durch, jetzt, string.Join("\n", aenderungen), bisherigeBeschreibung);
        return true;
    }

    /// <summary>Ordnet den Auftrag einem anderen Kunden zu.</summary>
    public void KundeAendern(Kunde kunde, Benutzer durch, DateTime jetzt)
    {
        NichtAbgeschlossen();
        NurZustaendig(durch);
        if (kunde.Id == KundeId)
            throw new AuftragException($"{kunde.Name} ist bereits der Kunde dieses Auftrags.");

        var beschreibung = $"Kunde: {Kunde?.Name ?? "–"} → {kunde.Name}";
        Kunde = kunde;
        KundeId = kunde.Id;
        Eintragen(VerlaufArt.Geaendert, durch, jetzt, beschreibung);
    }

    /// <summary>Jeder Bearbeiter darf übernehmen, auch von einem anderen Bearbeiter.</summary>
    public void Uebernehmen(Benutzer bearbeiter, DateTime jetzt)
    {
        NichtAbgeschlossen();
        if (BearbeiterId == bearbeiter.Id)
            throw new AuftragException("Der Auftrag ist bereits bei dir in Bearbeitung.");

        var beschreibung = BearbeiterId is null
            ? $"Übernommen aus Gruppe {Gruppe.Name}"
            : $"Übernommen von {Bearbeiter?.Name}";

        Bearbeiter = bearbeiter;
        BearbeiterId = bearbeiter.Id;
        BevorzugterBearbeiter = null;
        BevorzugterBearbeiterId = null;
        Eintragen(VerlaufArt.Uebernommen, bearbeiter, jetzt, beschreibung);
    }

    /// <summary>Weitergeleitet wird nur an Gruppen. Der bevorzugte Bearbeiter ist ein Hinweis.</summary>
    public void Weiterleiten(Gruppe ziel, Benutzer durch, DateTime jetzt, Benutzer? bevorzugt = null, string? text = null)
    {
        NichtAbgeschlossen();
        NurZustaendig(durch);

        var beschreibung = $"Weitergeleitet an Gruppe {ziel.Name}";
        if (bevorzugt is not null)
            beschreibung += $", bevorzugt an {bevorzugt.Name}";

        Gruppe = ziel;
        GruppeId = ziel.Id;
        Bearbeiter = null;
        BearbeiterId = null;
        BevorzugterBearbeiter = bevorzugt;
        BevorzugterBearbeiterId = bevorzugt?.Id;
        Eintragen(VerlaufArt.Weitergeleitet, durch, jetzt, beschreibung, text);
    }

    /// <summary>
    /// Gibt den Auftrag ohne Zutun des Bearbeiters an seine Gruppe zurück,
    /// zum Beispiel wenn der Bearbeiter ausscheidet. Das ist eine Handlung der
    /// Verwaltung, kein Weiterleiten durch den Bearbeiter.
    /// </summary>
    public void ZurueckAnGruppe(Benutzer durch, DateTime jetzt, string? grund = null)
    {
        NichtAbgeschlossen();
        if (BearbeiterId is null)
            throw new AuftragException("Der Auftrag liegt bereits offen in seiner Gruppe.");

        Bearbeiter = null;
        BearbeiterId = null;
        Eintragen(VerlaufArt.Weitergeleitet, durch, jetzt, $"Zurück an Gruppe {Gruppe.Name}", grund);
    }

    /// <summary>Für den Kunden unsichtbar.</summary>
    public void InternKommentieren(Benutzer durch, string? text, DateTime jetzt, IReadOnlyList<Anhang>? anhaenge = null)
    {
        NurZustaendig(durch);
        TextOderAnhang(text, anhaenge);
        Eintragen(VerlaufArt.InternerKommentar, durch, jetzt, null, text, aendertDenStand: false, anhaenge);
    }

    /// <summary>Für den Kunden sichtbar.</summary>
    public void NachrichtAnKunden(Benutzer durch, string? text, DateTime jetzt, IReadOnlyList<Anhang>? anhaenge = null)
    {
        NichtAbgeschlossen();
        NurZustaendig(durch);
        TextOderAnhang(text, anhaenge);
        KundeWartetSeit = null;
        Eintragen(VerlaufArt.NachrichtAnKunden, durch, jetzt, null, text, aendertDenStand: false, anhaenge);
    }

    /// <summary>Der Kunde antwortet, zum Beispiel auf eine Rückfrage.</summary>
    public void KundeAntwortet(Benutzer durch, string? text, DateTime jetzt, IReadOnlyList<Anhang>? anhaenge = null)
    {
        NichtAbgeschlossen();
        TextOderAnhang(text, anhaenge);
        KundeWartetSeit ??= jetzt;
        Eintragen(VerlaufArt.AntwortDesKunden, durch, jetzt, null, text, aendertDenStand: false, anhaenge);
    }

    /// <summary>Nur der zuständige Bearbeiter schließt ab.</summary>
    public void Abschliessen(Benutzer durch, string abschlusstext, DateTime jetzt)
    {
        NichtAbgeschlossen();
        NurZustaendig(durch);

        AbgeschlossenAm = jetzt;
        KundeWartetSeit = null;
        Eintragen(VerlaufArt.Abgeschlossen, durch, jetzt, null, abschlusstext);
    }

    /// <summary>Der Auftrag liegt danach wieder offen in seiner letzten Gruppe.</summary>
    public void WiederOeffnen(Benutzer durch, DateTime jetzt, string? text = null)
    {
        if (AbgeschlossenAm is null)
            throw new AuftragException("Der Auftrag ist nicht abgeschlossen.");

        AbgeschlossenAm = null;
        Bearbeiter = null;
        BearbeiterId = null;
        Eintragen(VerlaufArt.WiederGeoeffnet, durch, jetzt, $"Wieder geöffnet in Gruppe {Gruppe.Name}", text);
    }

    /// <summary>
    /// Der Kunde meldet sich zu einem abgeschlossenen Auftrag. Der Auftrag
    /// liegt danach wieder offen in seiner letzten Gruppe. Wer ihn zuletzt
    /// bearbeitet hat, wird als bevorzugter Bearbeiter genannt.
    /// </summary>
    public void KundeOeffnetWieder(Benutzer durch, string? text, DateTime jetzt, IReadOnlyList<Anhang>? anhaenge = null)
    {
        if (AbgeschlossenAm is null)
            throw new AuftragException("Der Auftrag ist nicht abgeschlossen.");
        TextOderAnhang(text, anhaenge);

        var bisher = Bearbeiter is { Aktiv: true, IstBearbeiter: true } ? Bearbeiter : null;
        var beschreibung = $"Vom Kunden wieder geöffnet in Gruppe {Gruppe.Name}";
        if (bisher is not null)
            beschreibung += $", bevorzugt an {bisher.Name}";

        AbgeschlossenAm = null;
        KundeWartetSeit = jetzt;
        Bearbeiter = null;
        BearbeiterId = null;
        BevorzugterBearbeiter = bisher;
        BevorzugterBearbeiterId = bisher?.Id;
        Eintragen(VerlaufArt.VomKundenWiederGeoeffnet, durch, jetzt, beschreibung, text, anhaenge: anhaenge);
    }

    /// <summary>Ein Kommentar oder eine Nachricht braucht Text, eine Datei oder beides.</summary>
    private static void TextOderAnhang(string? text, IReadOnlyList<Anhang>? anhaenge)
    {
        if (string.IsNullOrWhiteSpace(text) && anhaenge is not { Count: > 0 })
            throw new AuftragException("Bitte einen Text eingeben oder eine Datei anhängen.");
    }

    /// <summary>Übernehmen ist die Voraussetzung für alles Weitere am Auftrag.</summary>
    private void NurZustaendig(Benutzer durch)
    {
        if (BearbeiterId != durch.Id)
            throw new AuftragException(NichtUebernommen);
    }

    public const string NichtUebernommen =
        "Übernimm den Auftrag zuerst. Nur wer ihn bearbeitet, kann ihn weiterleiten, kommentieren, dem Kunden schreiben, Angaben ändern oder ihn abschließen.";

    private void NichtAbgeschlossen()
    {
        if (AbgeschlossenAm is not null)
            throw new AuftragException("Der Auftrag ist abgeschlossen.");
    }

    private void Setzen(AuftragAngaben angaben)
    {
        Betreff = angaben.Betreff;
        Beschreibung = angaben.Beschreibung;
        Kategorie = angaben.Kategorie;
        KategorieId = angaben.Kategorie?.Id;
        Standort = angaben.Standort;
        StandortId = angaben.Standort?.Id;
        Geraet = angaben.Geraet;
        Inventargeraet = angaben.Inventargeraet;
        InventargeraetId = angaben.Inventargeraet?.Id;
        Dringlichkeit = angaben.Dringlichkeit;
        Wichtigkeit = angaben.Wichtigkeit;
        Termin = angaben.Termin;
        SuchtextAktualisieren();
    }

    /// <summary>Setzt den Suchtext neu, auch für die geladenen Verlaufseinträge.</summary>
    public void SuchtextAktualisieren()
    {
        Suchtext = string.Join(' ', new[] { Betreff, Beschreibung, Geraet }.Where(t => !string.IsNullOrWhiteSpace(t))).ToLowerInvariant();
        foreach (var eintrag in _verlauf)
            eintrag.SuchtextAktualisieren();
    }

    /// <summary>
    /// Entfernt Leerraum am Rand und vereinheitlicht Zeilenenden, damit ein
    /// unverändert abgeschicktes Formular nicht als Änderung gilt.
    /// </summary>
    private static AuftragAngaben Bereinigt(AuftragAngaben angaben)
    {
        var betreff = angaben.Betreff?.Trim() ?? "";
        var beschreibung = angaben.Beschreibung?.ReplaceLineEndings("\n").Trim() ?? "";
        if (betreff.Length == 0 || beschreibung.Length == 0)
            throw new AuftragException("Betreff und Beschreibung dürfen nicht leer sein.");

        return angaben with
        {
            Betreff = betreff,
            Beschreibung = beschreibung,
            Geraet = string.IsNullOrWhiteSpace(angaben.Geraet) ? null : angaben.Geraet.Trim(),
        };
    }

    private void Eintragen(
        VerlaufArt art,
        Benutzer durch,
        DateTime jetzt,
        string? beschreibung,
        string? text = null,
        bool aendertDenStand = true,
        IReadOnlyList<Anhang>? anhaenge = null)
    {
        _verlauf.Add(new Verlaufseintrag(this, art, durch, jetzt, beschreibung, string.IsNullOrWhiteSpace(text) ? null : text.Trim(), anhaenge));
        GeaendertAm = jetzt;
        if (aendertDenStand)
            Version++;
    }
}
