using System.Net;
using System.Text;
using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avis.Tests;

public class AnhangTests(AvisAnwendung app) : IClassFixture<AvisAnwendung>
{
    private static readonly byte[] Bild = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5, 250, 251, 252];
    private static readonly byte[] Protokoll = Encoding.UTF8.GetBytes("Fehler 0x80070005: Zugriff verweigert\n");

    [Fact]
    public async Task Anhaenge_am_Auftrag_lassen_sich_unveraendert_herunterladen()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var id = await ErfassenAsync(anna, "Fehlermeldung beim Start", ("Bildschirmfoto Fehler.png", Bild), ("protokoll.log", Protokoll));

        var anhaenge = await AnhaengeAsync(id);
        Assert.Equal(["Bildschirmfoto Fehler.png", "protokoll.log"], anhaenge.Select(a => a.Dateiname));
        var seite = await anna.GetStringAsync($"/Auftraege/{id}");
        Assert.Contains("Bildschirmfoto Fehler.png", seite);
        Assert.Contains($"/Anhaenge/{anhaenge[0].Id}", seite);

        var antwort = await anna.GetAsync($"/Anhaenge/{anhaenge[0].Id}");
        Assert.Equal(HttpStatusCode.OK, antwort.StatusCode);
        Assert.Equal(Bild, await antwort.Content.ReadAsByteArrayAsync());
        // Immer als Download, nie zur Anzeige im Browser.
        Assert.Equal("attachment", antwort.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("Bildschirmfoto Fehler.png", antwort.Content.Headers.ContentDisposition.FileNameStar);
        Assert.Equal("application/octet-stream", antwort.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", antwort.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Anhaenge_an_internen_Kommentaren_bekommt_der_Kunde_nicht()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var id = await StellenAsync(dieter, "Rechner stürzt ab");
        var seite = $"/Auftraege/{id}";

        // Ein interner Kommentar darf auch nur aus einer Datei bestehen.
        await anna.UebernehmenAsync(app, id);
        await anna.PostMitDateienAsync(seite, $"{seite}?handler=InternerKommentar", new() { ["version"] = "1" }, ("intern-speicherabbild.txt", Protokoll));
        await anna.PostMitDateienAsync(seite, $"{seite}?handler=NachrichtAnKunden", new() { ["version"] = "1", ["text"] = "Bitte dieses Werkzeug starten." }, ("anleitung.pdf", Bild));

        var anhaenge = await AnhaengeAsync(id);
        var intern = anhaenge.Single(a => a.Dateiname == "intern-speicherabbild.txt");
        var fuerKunden = anhaenge.Single(a => a.Dateiname == "anleitung.pdf");

        var sicht = await dieter.GetStringAsync($"/Meine/{id}");
        Assert.Contains("anleitung.pdf", sicht);
        Assert.DoesNotContain("intern-speicherabbild.txt", sicht);
        Assert.Equal(HttpStatusCode.OK, (await dieter.GetAsync($"/Anhaenge/{fuerKunden.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dieter.GetAsync($"/Anhaenge/{intern.Id}")).StatusCode);

        Assert.Equal(Protokoll, await anna.GetByteArrayAsync($"/Anhaenge/{intern.Id}"));
        Assert.Contains("intern-speicherabbild.txt", await anna.GetStringAsync(seite));
    }

    [Fact]
    public async Task Der_Kunde_haengt_Dateien_an_Auftrag_und_Antwort()
    {
        var dieter = await app.AnmeldenAlsAsync("dieter");
        var bernd = await app.AnmeldenAlsAsync("bernd");

        var antwort = await dieter.PostMitDateienAsync("/Meine/Neu", "/Meine/Neu", new()
        {
            ["Eingabe.Betreff"] = "Drucker druckt Streifen",
            ["Eingabe.Beschreibung"] = "Siehe Foto.",
        }, ("streifen.jpg", Bild));
        var id = int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
        await dieter.PostMitDateienAsync($"/Meine/{id}", $"/Meine/{id}", [], ("zweites-foto.jpg", Bild));

        var anhaenge = await AnhaengeAsync(id);
        Assert.Equal(["streifen.jpg", "zweites-foto.jpg"], anhaenge.Select(a => a.Dateiname));
        var seite = await bernd.GetStringAsync($"/Auftraege/{id}");
        Assert.Contains("streifen.jpg", seite);
        Assert.Contains("zweites-foto.jpg", seite);
        Assert.Equal(Bild, await bernd.GetByteArrayAsync($"/Anhaenge/{anhaenge[1].Id}"));
        Assert.Equal(Bild, await dieter.GetByteArrayAsync($"/Anhaenge/{anhaenge[1].Id}"));
    }

    [Fact]
    public async Task Fremde_bekommen_einen_Anhang_nicht()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Nur für Eva", kunde: "Eva Test", ("fuer-eva.txt", Protokoll));
        var anhang = (await AnhaengeAsync(id)).Single();

        // Ein anderer Kunde.
        var dieter = await app.AnmeldenAlsAsync("dieter");
        Assert.Equal(HttpStatusCode.NotFound, (await dieter.GetAsync($"/Anhaenge/{anhang.Id}")).StatusCode);

        // Ein Bearbeiter, nachdem der Auftrag in einer gesperrten Gruppe liegt.
        var bernd = await app.AnmeldenAlsAsync("bernd");
        Assert.Equal(HttpStatusCode.OK, (await bernd.GetAsync($"/Anhaenge/{anhang.Id}")).StatusCode);
        await anna.PostFormularAsync("/Verwaltung/Gruppen", "/Verwaltung/Gruppen", new() { ["name"] = "Anhangtresor" });
        var gruppe = await app.DatenbankAsync(db => db.Gruppen.Where(g => g.Name == "Anhangtresor").Select(g => g.Id).FirstAsync());
        await anna.PostFormularAsync($"/Verwaltung/Gruppen/{gruppe}", $"/Verwaltung/Gruppen/{gruppe}?handler=Speichern", new() { ["name"] = "Anhangtresor", ["aktiv"] = "true", ["gesperrt"] = "true" });
        await anna.UebernehmenAsync(app, id);
        await anna.PostFormularAsync($"/Auftraege/{id}", $"/Auftraege/{id}?handler=Weiterleiten", new() { ["version"] = "2", ["gruppeId"] = gruppe.ToString() });
        Assert.Equal(HttpStatusCode.NotFound, (await bernd.GetAsync($"/Anhaenge/{anhang.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await anna.GetAsync("/Anhaenge/987654")).StatusCode);
    }

    [Fact]
    public async Task Zu_grosse_leere_und_zu_viele_Dateien_werden_abgelehnt_und_hinterlassen_nichts()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var auftraege = await app.DatenbankAsync(db => db.Auftraege.CountAsync());
        var dateien = app.DateienInDerAblage();

        var zuGross = await ErfassenAntwortAsync(anna, "Zu groß", null, ("klein.txt", Protokoll), ("riesig.bin", new byte[1024 * 1024 + 1]));
        Assert.Contains("Die Datei riesig.bin ist größer als 1 MB.", await zuGross.Content.ReadAsStringAsync());

        var leer = await ErfassenAntwortAsync(anna, "Leer", null, ("leer.txt", []));
        Assert.Contains("Die Datei leer.txt ist leer.", await leer.Content.ReadAsStringAsync());

        var zuViele = await ErfassenAntwortAsync(anna, "Zu viele", null, ("1.txt", Protokoll), ("2.txt", Protokoll), ("3.txt", Protokoll), ("4.txt", Protokoll));
        Assert.Contains("höchstens 3 Dateien", await zuViele.Content.ReadAsStringAsync());

        Assert.Equal(auftraege, await app.DatenbankAsync(db => db.Auftraege.CountAsync()));
        Assert.Equal(dateien, app.DateienInDerAblage());
    }

    [Fact]
    public async Task Scheitert_die_Aktion_bleibt_keine_Datei_liegen()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Wird gleich abgeschlossen");
        var seite = $"/Auftraege/{id}";
        await anna.PostFormularAsync(seite, $"{seite}?handler=Uebernehmen", new() { ["version"] = "1" });
        await anna.PostFormularAsync(seite, $"{seite}?handler=Abschliessen", new() { ["version"] = "2", ["text"] = "Erledigt." });
        var dateien = app.DateienInDerAblage();

        var antwort = await anna.PostMitDateienAsync(seite, $"{seite}?handler=NachrichtAnKunden", new() { ["version"] = "3", ["text"] = "Zu spät." }, ("zu-spaet.txt", Protokoll));

        Assert.Contains("Der Auftrag ist abgeschlossen.", await antwort.Content.ReadAsStringAsync());
        Assert.Empty(await AnhaengeAsync(id));
        Assert.Equal(dateien, app.DateienInDerAblage());
    }

    [Fact]
    public async Task Ein_Kommentar_braucht_Text_oder_Datei()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Leerer Kommentar");
        var seite = $"/Auftraege/{id}";
        await anna.UebernehmenAsync(app, id);

        var antwort = await anna.PostMitDateienAsync(seite, $"{seite}?handler=InternerKommentar", new() { ["version"] = "1", ["text"] = "  " });

        Assert.Contains("Bitte einen Text eingeben oder eine Datei anhängen.", await antwort.Content.ReadAsStringAsync());
        Assert.Equal([VerlaufArt.Erfasst, VerlaufArt.Uebernommen], (await LadenAsync(id)).Verlauf.OrderBy(v => v.Id).Select(v => v.Art));
    }

    [Fact]
    public async Task Ein_Dateiname_mit_Pfad_wird_auf_den_Namen_gekuerzt_und_bestimmt_nicht_den_Ablageort()
    {
        var anna = await app.AnmeldenAlsAsync("anna");

        var id = await ErfassenAsync(anna, "Böser Dateiname", (@"..\..\..\web.config", Protokoll), ("/etc/cron.d/eintrag", Protokoll));

        var anhaenge = await AnhaengeAsync(id);
        Assert.Equal(["web.config", "eintrag"], anhaenge.Select(a => a.Dateiname));
        Assert.All(anhaenge, a =>
        {
            Assert.Matches(@"^\d{4}/\d{2}/[0-9a-f]{32}$", a.Ablagename);
            Assert.True(File.Exists(Path.Combine(app.Anhangordner, a.Ablagename)));
        });
    }

    [Fact]
    public async Task Die_Mail_zur_Nachricht_nennt_die_Anhaenge()
    {
        var anna = await app.AnmeldenAlsAsync("anna");
        var id = await ErfassenAsync(anna, "Anleitung per Mail", kunde: "Frank Müller");
        var seite = $"/Auftraege/{id}";
        await anna.UebernehmenAsync(app, id);
        await app.MailsVersendenAsync();

        await anna.PostMitDateienAsync(seite, $"{seite}?handler=NachrichtAnKunden", new() { ["version"] = "1" }, ("anleitung.pdf", Bild), ("formular.docx", Bild));

        var mail = Assert.Single(await app.MailsVersendenAsync());
        Assert.Contains("Anhänge, abrufbar im Auftrag: anleitung.pdf, formular.docx", mail.TextBody);
        Assert.Empty(mail.Attachments);
    }

    private Task<int> ErfassenAsync(HttpClient client, string betreff, params (string Name, byte[] Inhalt)[] dateien) =>
        ErfassenAsync(client, betreff, null, dateien);

    private async Task<int> ErfassenAsync(HttpClient client, string betreff, string? kunde, params (string Name, byte[] Inhalt)[] dateien)
    {
        var antwort = await ErfassenAntwortAsync(client, betreff, kunde, dateien);
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private async Task<HttpResponseMessage> ErfassenAntwortAsync(HttpClient client, string betreff, string? kunde, params (string Name, byte[] Inhalt)[] dateien)
    {
        var felder = new Dictionary<string, string>
        {
            ["Eingabe.Felder.Betreff"] = betreff,
            ["Eingabe.Felder.Beschreibung"] = "Für einen Test.",
            ["Eingabe.GruppeId"] = (await app.DatenbankAsync(db => db.Gruppen.Where(g => g.IstEingangsgruppe).Select(g => g.Id).FirstAsync())).ToString(),
        };
        if (kunde is null)
            felder["Eingabe.KundeName"] = "Test Kunde";
        else
            felder["kundeId"] = (await app.DatenbankAsync(db => db.Kunden.Where(k => k.Name == kunde).Select(k => k.Id).FirstAsync())).ToString();
        return await client.PostMitDateienAsync("/Auftraege/Neu", "/Auftraege/Neu", felder, dateien);
    }

    private static async Task<int> StellenAsync(HttpClient client, string betreff)
    {
        var antwort = await client.PostFormularAsync("/Meine/Neu", "/Meine/Neu", new() { ["Eingabe.Betreff"] = betreff, ["Eingabe.Beschreibung"] = "Für einen Test." });
        return int.Parse(antwort.RequestMessage!.RequestUri!.AbsolutePath.Split('/')[^1]);
    }

    private Task<List<Anhang>> AnhaengeAsync(int auftragId) => app.DatenbankAsync(db => db.Anhaenge
        .AsNoTracking()
        .Where(a => a.Verlaufseintrag.AuftragId == auftragId)
        .OrderBy(a => a.Id)
        .ToListAsync());

    private Task<Auftrag> LadenAsync(int id) => app.DatenbankAsync(db => db.Auftraege
        .AsNoTracking()
        .Include(a => a.Verlauf)
        .FirstAsync(a => a.Id == id));
}
