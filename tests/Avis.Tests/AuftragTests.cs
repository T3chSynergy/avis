using Avis.Web.Domain;

namespace Avis.Tests;

public class AuftragTests
{
    private static readonly DateTime Jetzt = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

    private readonly Gruppe _hotline = new() { Id = 1, Name = "IT-Hotline", IstEingangsgruppe = true };
    private readonly Gruppe _netzwerk = new() { Id = 2, Name = "Netzwerk" };
    private readonly Benutzer _anna = new() { Id = 1, Anmeldename = "anna", Name = "Anna Beispiel", IstBearbeiter = true };
    private readonly Benutzer _bernd = new() { Id = 2, Anmeldename = "bernd", Name = "Bernd Muster", IstBearbeiter = true };
    private readonly Kunde _kunde = new() { Id = 1, Name = "Dieter Kunde" };

    private static readonly AuftragAngaben Angaben = new("Drucker defekt", "Druckt nicht.");

    private Auftrag Neu(AuftragAngaben? angaben = null) =>
        Auftrag.Erfassen(angaben ?? Angaben, _kunde, _anna, _hotline, Eingang.Telefon, Jetzt);

    [Fact]
    public void Neuer_Auftrag_liegt_offen_in_der_Gruppe()
    {
        var auftrag = Neu();

        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.Same(_hotline, auftrag.Gruppe);
        Assert.Null(auftrag.Bearbeiter);
        Assert.Equal(VerlaufArt.Erfasst, Assert.Single(auftrag.Verlauf).Art);
    }

    [Fact]
    public void Uebernehmen_setzt_den_Bearbeiter_und_behaelt_die_Gruppe()
    {
        var auftrag = Neu();

        auftrag.Uebernehmen(_anna, Jetzt);

        Assert.Equal(AuftragStatus.InBearbeitung, auftrag.Status);
        Assert.Same(_anna, auftrag.Bearbeiter);
        Assert.Same(_hotline, auftrag.Gruppe);
    }

    [Fact]
    public void Jeder_Bearbeiter_kann_einen_Auftrag_von_einem_anderen_uebernehmen()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_anna, Jetzt);

        auftrag.Uebernehmen(_bernd, Jetzt);

        Assert.Same(_bernd, auftrag.Bearbeiter);
        Assert.Equal("Übernommen von Anna Beispiel", auftrag.Verlauf[^1].Beschreibung);
    }

    [Fact]
    public void Zweimal_selbst_uebernehmen_geht_nicht()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_anna, Jetzt);

        Assert.Throws<AuftragException>(() => auftrag.Uebernehmen(_anna, Jetzt));
    }

    [Fact]
    public void Weiterleiten_legt_den_Auftrag_offen_in_die_neue_Gruppe()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_anna, Jetzt);

        auftrag.Weiterleiten(_netzwerk, _anna, Jetzt, _bernd, "Bitte Switch prüfen.");

        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.Same(_netzwerk, auftrag.Gruppe);
        Assert.Null(auftrag.Bearbeiter);
        Assert.Same(_bernd, auftrag.BevorzugterBearbeiter);
        Assert.Equal("Bitte Switch prüfen.", auftrag.Verlauf[^1].Text);
    }

    [Fact]
    public void Der_bevorzugte_Bearbeiter_ist_nur_ein_Hinweis()
    {
        var auftrag = Neu();
        auftrag.Weiterleiten(_netzwerk, _anna, Jetzt, _bernd);

        auftrag.Uebernehmen(_anna, Jetzt);

        Assert.Same(_anna, auftrag.Bearbeiter);
        Assert.Null(auftrag.BevorzugterBearbeiter);
    }

    [Fact]
    public void Weiterleiten_an_die_Gruppe_in_der_der_Auftrag_offen_liegt_geht_nicht()
    {
        var auftrag = Neu();

        Assert.Throws<AuftragException>(() => auftrag.Weiterleiten(_hotline, _anna, Jetzt));
    }

    [Fact]
    public void Ein_Bearbeiter_kann_den_Auftrag_an_die_eigene_Gruppe_zurueckgeben()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_anna, Jetzt);

        auftrag.Weiterleiten(_hotline, _anna, Jetzt);

        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
    }

    [Fact]
    public void Nur_der_zustaendige_Bearbeiter_schliesst_ab()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_anna, Jetzt);

        Assert.Throws<AuftragException>(() => auftrag.Abschliessen(_bernd, "Erledigt.", Jetzt));

        auftrag.Abschliessen(_anna, "Erledigt.", Jetzt);
        Assert.Equal(AuftragStatus.Abgeschlossen, auftrag.Status);
    }

    [Fact]
    public void Ein_abgeschlossener_Auftrag_laesst_sich_nicht_uebernehmen_oder_weiterleiten()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_anna, Jetzt);
        auftrag.Abschliessen(_anna, "Erledigt.", Jetzt);

        Assert.Throws<AuftragException>(() => auftrag.Uebernehmen(_bernd, Jetzt));
        Assert.Throws<AuftragException>(() => auftrag.Weiterleiten(_netzwerk, _anna, Jetzt));
        Assert.Throws<AuftragException>(() => auftrag.NachrichtAnKunden(_anna, "Hallo", Jetzt));
    }

    [Fact]
    public void Wieder_oeffnen_legt_den_Auftrag_offen_in_die_letzte_Gruppe()
    {
        var auftrag = Neu();
        auftrag.Weiterleiten(_netzwerk, _anna, Jetzt);
        auftrag.Uebernehmen(_bernd, Jetzt);
        auftrag.Abschliessen(_bernd, "Erledigt.", Jetzt);

        auftrag.WiederOeffnen(_anna, Jetzt, "Fehler tritt wieder auf.");

        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.Same(_netzwerk, auftrag.Gruppe);
        Assert.Null(auftrag.Bearbeiter);
    }

    [Fact]
    public void Der_Kunde_oeffnet_wieder_und_der_bisherige_Bearbeiter_wird_bevorzugt()
    {
        var auftrag = Neu();
        auftrag.Weiterleiten(_netzwerk, _anna, Jetzt);
        auftrag.Uebernehmen(_bernd, Jetzt);
        auftrag.Abschliessen(_bernd, "Erledigt.", Jetzt);
        var version = auftrag.Version;

        auftrag.KundeOeffnetWieder(_anna, "Geht wieder nicht.", Jetzt);

        Assert.Equal(AuftragStatus.Offen, auftrag.Status);
        Assert.Same(_netzwerk, auftrag.Gruppe);
        Assert.Null(auftrag.Bearbeiter);
        Assert.Same(_bernd, auftrag.BevorzugterBearbeiter);
        Assert.Equal(version + 1, auftrag.Version);
        Assert.True(auftrag.Verlauf[^1].FuerKundenSichtbar);
        Assert.Throws<AuftragException>(() => auftrag.KundeOeffnetWieder(_anna, "Nochmal.", Jetzt));
    }

    [Fact]
    public void Ein_ausgeschiedener_Bearbeiter_wird_nicht_bevorzugt()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_bernd, Jetzt);
        auftrag.Abschliessen(_bernd, "Erledigt.", Jetzt);
        _bernd.Aktiv = false;

        auftrag.KundeOeffnetWieder(_anna, "Geht wieder nicht.", Jetzt);

        Assert.Null(auftrag.BevorzugterBearbeiter);
        Assert.Equal("Vom Kunden wieder geöffnet in Gruppe IT-Hotline", auftrag.Verlauf[^1].Beschreibung);
    }

    [Fact]
    public void Wieder_oeffnen_geht_nur_bei_abgeschlossenen_Auftraegen()
    {
        Assert.Throws<AuftragException>(() => Neu().WiederOeffnen(_anna, Jetzt));
    }

    [Fact]
    public void Interne_Kommentare_und_Weiterleitungen_sieht_der_Kunde_nicht()
    {
        var auftrag = Neu();
        auftrag.InternKommentieren(_anna, "Passwort liegt im Tresor.", Jetzt);
        auftrag.NachrichtAnKunden(_anna, "Wir kümmern uns.", Jetzt);
        auftrag.Weiterleiten(_netzwerk, _anna, Jetzt, text: "Interner Hinweis.");

        var sichtbar = auftrag.Verlauf.Where(v => v.FuerKundenSichtbar).Select(v => v.Art);

        Assert.Equal([VerlaufArt.Erfasst, VerlaufArt.NachrichtAnKunden], sichtbar);
    }

    [Fact]
    public void Aktionen_erhoehen_die_Version_Kommentare_nicht()
    {
        var auftrag = Neu();
        var vorher = auftrag.Version;

        auftrag.InternKommentieren(_anna, "Notiz.", Jetzt);
        auftrag.NachrichtAnKunden(_anna, "Hallo.", Jetzt);
        Assert.Equal(vorher, auftrag.Version);

        auftrag.Uebernehmen(_anna, Jetzt);
        Assert.Equal(vorher + 1, auftrag.Version);

        auftrag.Aendern(Angaben with { Dringlichkeit = Stufe.Hoch }, _anna, Jetzt);
        Assert.Equal(vorher + 2, auftrag.Version);
    }

    [Fact]
    public void Aendern_haelt_alte_und_neue_Werte_im_Verlauf_fest()
    {
        var stoerung = new Kategorie { Id = 1, Name = "Störung" };
        var anfrage = new Kategorie { Id = 2, Name = "Anfrage" };
        var haupthaus = new Standort { Id = 1, Name = "Haupthaus" };
        var auftrag = Neu(Angaben with { Kategorie = stoerung, Geraet = "PC-0815" });

        var geaendert = auftrag.Aendern(
            new("Drucker im Flur defekt", "Druckt nicht.")
            {
                Kategorie = anfrage,
                Standort = haupthaus,
                Dringlichkeit = Stufe.Hoch,
                Termin = new DateOnly(2026, 11, 20),
            },
            _bernd, Jetzt);

        Assert.True(geaendert);
        Assert.Equal("Drucker im Flur defekt", auftrag.Betreff);
        Assert.Same(anfrage, auftrag.Kategorie);
        Assert.Null(auftrag.Geraet);
        Assert.Equal(2, auftrag.Prioritaet);

        var eintrag = auftrag.Verlauf[^1];
        Assert.Equal(VerlaufArt.Geaendert, eintrag.Art);
        Assert.Same(_bernd, eintrag.Benutzer);
        Assert.False(eintrag.FuerKundenSichtbar);
        Assert.Equal(
            [
                "Betreff: Drucker defekt → Drucker im Flur defekt",
                "Kategorie: Störung → Anfrage",
                "Standort: – → Haupthaus",
                "Gerät: PC-0815 → –",
                "Dringlichkeit: mittel → hoch",
                "Termin: – → 20.11.2026",
            ],
            eintrag.Beschreibung!.Split('\n'));
        Assert.Null(eintrag.Text);
    }

    [Fact]
    public void Der_bisherige_Text_der_Beschreibung_bleibt_im_Verlauf()
    {
        var auftrag = Neu();

        auftrag.Aendern(Angaben with { Beschreibung = "Druckt nur leere Seiten." }, _anna, Jetzt);

        Assert.Equal("Druckt nur leere Seiten.", auftrag.Beschreibung);
        Assert.Equal("Beschreibung geändert. Bisheriger Text:", auftrag.Verlauf[^1].Beschreibung);
        Assert.Equal("Druckt nicht.", auftrag.Verlauf[^1].Text);
    }

    [Fact]
    public void Aendern_ohne_Unterschied_schreibt_nichts_in_den_Verlauf()
    {
        var auftrag = Neu(new("Drucker defekt", "Zeile eins\nZeile zwei") { Geraet = "PC-0815" });
        var eintraege = auftrag.Verlauf.Count;

        // So kommt ein unverändertes Formular aus dem Browser zurück.
        var geaendert = auftrag.Aendern(
            new(" Drucker defekt ", "Zeile eins\r\nZeile zwei\r\n") { Geraet = "PC-0815 " }, _anna, Jetzt);

        Assert.False(geaendert);
        Assert.Equal(eintraege, auftrag.Verlauf.Count);
    }

    [Fact]
    public void Betreff_und_Beschreibung_duerfen_nicht_leer_werden()
    {
        var auftrag = Neu();

        Assert.Throws<AuftragException>(() => auftrag.Aendern(Angaben with { Betreff = "  " }, _anna, Jetzt));
        Assert.Throws<AuftragException>(() => auftrag.Aendern(Angaben with { Beschreibung = "" }, _anna, Jetzt));
    }

    [Fact]
    public void Ein_abgeschlossener_Auftrag_laesst_sich_nicht_aendern()
    {
        var auftrag = Neu();
        auftrag.Uebernehmen(_anna, Jetzt);
        auftrag.Abschliessen(_anna, "Erledigt.", Jetzt);

        Assert.Throws<AuftragException>(() => auftrag.Aendern(Angaben with { Dringlichkeit = Stufe.Hoch }, _anna, Jetzt));
    }

    [Theory]
    [InlineData(Stufe.Hoch, Stufe.Hoch, 1)]
    [InlineData(Stufe.Hoch, Stufe.Mittel, 2)]
    [InlineData(Stufe.Mittel, Stufe.Hoch, 2)]
    [InlineData(Stufe.Hoch, Stufe.Niedrig, 3)]
    [InlineData(Stufe.Mittel, Stufe.Mittel, 3)]
    [InlineData(Stufe.Niedrig, Stufe.Hoch, 3)]
    [InlineData(Stufe.Mittel, Stufe.Niedrig, 4)]
    [InlineData(Stufe.Niedrig, Stufe.Mittel, 4)]
    [InlineData(Stufe.Niedrig, Stufe.Niedrig, 5)]
    public void Prioritaet_ergibt_sich_aus_Dringlichkeit_und_Wichtigkeit(Stufe dringlichkeit, Stufe wichtigkeit, int erwartet)
    {
        var auftrag = Neu(Angaben with { Dringlichkeit = dringlichkeit, Wichtigkeit = wichtigkeit });

        Assert.Equal(erwartet, auftrag.Prioritaet);
    }
}
