using System.Text.Encodings.Web;
using System.Text.Unicode;
using Avis.Web;
using Avis.Web.Anhaenge;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Avis.Web.Mail;
using Avis.Web.Posteingang;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

var verbindung = Datenbankdatei.Vorbereiten(
    builder.Configuration.GetConnectionString("Avis")
        ?? throw new InvalidOperationException("Die Einstellung ConnectionStrings:Avis fehlt."),
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AvisDb>(o => o.UseSqlite(verbindung));
// Umlaute und Sonderzeichen im HTML als Zeichen ausgeben statt als
// Zeichencodes. Zeichen mit Bedeutung in HTML werden weiterhin maskiert.
builder.Services.Configure<WebEncoderOptions>(o =>
    o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Ortszeit>();
builder.AddAvisAnmeldung();
builder.AddAvisVerzeichnis();
builder.AddAvisMail();
builder.AddAvisPosteingang();
builder.Services.Configure<AnhangOptionen>(builder.Configuration.GetSection("Anhaenge"));
builder.Services.AddSingleton<Anhangablage>();

builder.Services.AddRazorPages(o =>
{
    // Alle Seiten sind für Bearbeiter. Ausnahmen sind hier aufgeführt.
    o.Conventions.AuthorizeFolder("/Verwaltung", Rollen.Administrator);
    o.Conventions.AuthorizePage("/Geraete/Bearbeiten", Rollen.Administrator);
    o.Conventions.AuthorizeFolder("/Meine", Richtlinien.Angemeldet);
    o.Conventions.AuthorizeFolder("/Anhaenge", Richtlinien.Angemeldet);
    o.Conventions.AuthorizePage("/Index", Richtlinien.Angemeldet);
    o.Conventions.AllowAnonymousToPage("/Anmeldung");
    o.Conventions.AllowAnonymousToPage("/Fehler");
    o.Conventions.AllowAnonymousToPage("/Status");
});

var app = builder.Build();

await app.DatenbankVorbereitenAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Fehler");

app.UseStatusCodePagesWithReExecute("/Status/{0}");

app.UseRequestLocalization("de-DE");
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<BenutzerMiddleware>();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

public partial class Program;
