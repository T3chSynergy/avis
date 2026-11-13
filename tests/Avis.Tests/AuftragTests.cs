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

    private Auftrag Neu() =>
        Auftrag.Erfassen("Drucker defekt", "Druckt nicht.", _kunde, _anna, _hotline, Eingang.Telefon, Jetzt);

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
    public void Jede_Aktion_erhoeht_die_Version()
    {
        var auftrag = Neu();
        var vorher = auftrag.Version;

        auftrag.Uebernehmen(_anna, Jetzt);

        Assert.Equal(vorher + 1, auftrag.Version);
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
        var auftrag = Neu();
        auftrag.Dringlichkeit = dringlichkeit;
        auftrag.Wichtigkeit = wichtigkeit;

        Assert.Equal(erwartet, auftrag.Prioritaet);
    }
}
