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

/// <summary>Eine Aktion ist im aktuellen Zustand des Auftrags nicht erlaubt.</summary>
public class AuftragException(string message) : Exception(message);

/// <summary>
/// Ein Auftrag gehört immer zu genau einer Gruppe und hat höchstens einen
/// Bearbeiter. Der Status ergibt sich daraus und wird nicht gespeichert.
/// Gruppe, Bearbeiter und Abschluss ändern sich nur über die Methoden dieser
/// Klasse, und jede davon schreibt einen Verlaufseintrag.
/// </summary>
public class Auftrag
{
    private readonly List<Verlaufseintrag> _verlauf = [];

    private Auftrag() { }

    /// <summary>Die Auftragsnummer.</summary>
    public int Id { get; private set; }

    public string Betreff { get; set; } = "";
    public string Beschreibung { get; set; } = "";

    public int KundeId { get; private set; }
    public Kunde Kunde { get; private set; } = null!;

    public int ErfasstVonId { get; private set; }
    public Benutzer ErfasstVon { get; private set; } = null!;

    public Eingang Eingang { get; private set; }

    public int? KategorieId { get; set; }
    public Kategorie? Kategorie { get; set; }

    public int? StandortId { get; set; }
    public Standort? Standort { get; set; }

    /// <summary>Freitext, z. B. eine Inventarnummer.</summary>
    public string? Geraet { get; set; }

    public Stufe Dringlichkeit { get; set; } = Stufe.Mittel;
    public Stufe Wichtigkeit { get; set; } = Stufe.Mittel;
    public DateOnly? Termin { get; set; }

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
    /// Zählt bei jeder Aktion hoch. Damit fällt auf, wenn jemand auf einem
    /// veralteten Stand der Seite eine Aktion auslöst.
    /// </summary>
    public int Version { get; private set; }

    public IReadOnlyList<Verlaufseintrag> Verlauf => _verlauf;

    public AuftragStatus Status =>
        AbgeschlossenAm is not null ? AuftragStatus.Abgeschlossen
        : BearbeiterId is not null ? AuftragStatus.InBearbeitung
        : AuftragStatus.Offen;

    /// <summary>1 (höchste) bis 5 (niedrigste), aus Dringlichkeit und Wichtigkeit.</summary>
    public int Prioritaet => 5 - (int)Dringlichkeit - (int)Wichtigkeit;

    public static Auftrag Erfassen(
        string betreff,
        string beschreibung,
        Kunde kunde,
        Benutzer erfasstVon,
        Gruppe gruppe,
        Eingang eingang,
        DateTime jetzt)
    {
        var auftrag = new Auftrag
        {
            Betreff = betreff,
            Beschreibung = beschreibung,
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
        auftrag.Eintragen(VerlaufArt.Erfasst, erfasstVon, jetzt, $"Erfasst in Gruppe {gruppe.Name}");
        return auftrag;
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
        if (BearbeiterId is null && ziel.Id == GruppeId)
            throw new AuftragException($"Der Auftrag liegt bereits in der Gruppe {ziel.Name}.");

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

    /// <summary>Für den Kunden unsichtbar.</summary>
    public void InternKommentieren(Benutzer durch, string text, DateTime jetzt) =>
        Eintragen(VerlaufArt.InternerKommentar, durch, jetzt, null, text);

    /// <summary>Für den Kunden sichtbar.</summary>
    public void NachrichtAnKunden(Benutzer durch, string text, DateTime jetzt)
    {
        NichtAbgeschlossen();
        Eintragen(VerlaufArt.NachrichtAnKunden, durch, jetzt, null, text);
    }

    /// <summary>Nur der zuständige Bearbeiter schließt ab.</summary>
    public void Abschliessen(Benutzer durch, string abschlusstext, DateTime jetzt)
    {
        NichtAbgeschlossen();
        if (BearbeiterId != durch.Id)
            throw new AuftragException("Nur der zuständige Bearbeiter kann abschließen. Übernimm den Auftrag zuerst.");

        AbgeschlossenAm = jetzt;
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

    private void NichtAbgeschlossen()
    {
        if (AbgeschlossenAm is not null)
            throw new AuftragException("Der Auftrag ist abgeschlossen.");
    }

    private void Eintragen(VerlaufArt art, Benutzer durch, DateTime jetzt, string? beschreibung, string? text = null)
    {
        _verlauf.Add(new Verlaufseintrag(this, art, durch, jetzt, beschreibung, string.IsNullOrWhiteSpace(text) ? null : text.Trim()));
        GeaendertAm = jetzt;
        Version++;
    }
}
