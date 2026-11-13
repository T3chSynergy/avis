using Avis.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Avis.Web.Data;

/// <summary>
/// Datenbankzugriff. Das Modell verzichtet auf Besonderheiten von SQLite,
/// damit ein späterer Wechsel zu SQL Server nur den Anbieter und die
/// Migrationen betrifft.
/// </summary>
public class AvisDb(DbContextOptions<AvisDb> options) : DbContext(options)
{
    public DbSet<Auftrag> Auftraege => Set<Auftrag>();
    public DbSet<Verlaufseintrag> Verlauf => Set<Verlaufseintrag>();
    public DbSet<Benutzer> Benutzer => Set<Benutzer>();
    public DbSet<Gruppe> Gruppen => Set<Gruppe>();
    public DbSet<Kunde> Kunden => Set<Kunde>();
    public DbSet<Kategorie> Kategorien => Set<Kategorie>();
    public DbSet<Standort> Standorte => Set<Standort>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Zeitpunkte werden in UTC gespeichert. Beim Lesen geht die Angabe
        // sonst verloren.
        builder.Properties<DateTime>().HaveConversion<UtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Benutzer>(e =>
        {
            e.ToTable("Benutzer");
            e.Property(b => b.Anmeldename).HasMaxLength(200);
            e.Property(b => b.Name).HasMaxLength(200);
            e.Property(b => b.Email).HasMaxLength(320);
            e.HasIndex(b => b.Anmeldename).IsUnique();
        });

        model.Entity<Gruppe>(e =>
        {
            e.ToTable("Gruppen");
            e.Property(g => g.Name).HasMaxLength(100);
            e.Property(g => g.Beschreibung).HasMaxLength(500);
            e.HasIndex(g => g.Name).IsUnique();
            e.HasMany(g => g.Mitglieder)
                .WithMany(b => b.Gruppen)
                .UsingEntity(j => j.ToTable("Gruppenmitglieder"));
        });

        model.Entity<Kunde>(e =>
        {
            e.ToTable("Kunden");
            e.Property(k => k.Name).HasMaxLength(200);
            e.Property(k => k.Email).HasMaxLength(320);
            e.Property(k => k.Telefon).HasMaxLength(50);
            e.Property(k => k.Organisationseinheit).HasMaxLength(200);
            e.Property(k => k.Anmeldename).HasMaxLength(200);
            e.Property(k => k.ExterneKennung).HasMaxLength(200);
            e.Property(k => k.Quelle).HasMaxLength(100);
            e.HasIndex(k => k.Email);
            e.HasIndex(k => k.Anmeldename);
        });

        model.Entity<Kategorie>(e =>
        {
            e.ToTable("Kategorien");
            e.Property(k => k.Name).HasMaxLength(100);
        });

        model.Entity<Standort>(e =>
        {
            e.ToTable("Standorte");
            e.Property(s => s.Name).HasMaxLength(100);
        });

        model.Entity<Auftrag>(e =>
        {
            e.ToTable("Auftraege");
            e.Property(a => a.Betreff).HasMaxLength(200);
            e.Property(a => a.Geraet).HasMaxLength(200);
            e.Property(a => a.Version).IsConcurrencyToken();
            e.HasMany(a => a.Verlauf).WithOne(v => v.Auftrag).HasForeignKey(v => v.AuftragId);
            e.HasIndex(a => new { a.GruppeId, a.AbgeschlossenAm });
            e.HasIndex(a => new { a.BearbeiterId, a.AbgeschlossenAm });
        });

        model.Entity<Verlaufseintrag>(e => e.ToTable("Verlauf"));

        // Nichts wird mitgelöscht. Aufträge und ihr Verlauf bleiben erhalten,
        // Stammdaten werden deaktiviert statt gelöscht.
        foreach (var fk in model.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
