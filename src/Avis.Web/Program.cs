using System.Text.Encodings.Web;
using System.Text.Unicode;
using Avis.Web;
using Avis.Web.Anmeldung;
using Avis.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

var verbindung = Datenbankdatei.Vorbereiten(
    builder.Configuration.GetConnectionString("Avis")
        ?? throw new InvalidOperationException("Die Einstellung ConnectionStrings:Avis fehlt."),
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AvisDb>(o => o.UseSqlite(verbindung));
// Umlaute im HTML als Zeichen ausgeben statt als Zeichencodes.
builder.Services.Configure<WebEncoderOptions>(o =>
    o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.GeneralPunctuation));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Ortszeit>();
builder.AddAvisAnmeldung();

builder.Services.AddRazorPages(o =>
{
    // Alle Seiten sind für Bearbeiter. Ausnahmen sind hier aufgeführt.
    o.Conventions.AuthorizeFolder("/", Rollen.Bearbeiter);
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
