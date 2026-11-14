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
    public DbSet<Gruppenmitglied> Gruppenmitglieder => Set<Gruppenmitglied>();
    public DbSet<Kunde> Kunden => Set<Kunde>();
    public DbSet<Kategorie> Kategorien => Set<Kategorie>();
    public DbSet<Standort> Standorte => Set<Standort>();
    public DbSet<Benachrichtigung> Benachrichtigungen => Set<Benachrichtigung>();
    public DbSet<Inventargeraet> Geraete => Set<Inventargeraet>();
    public DbSet<Anhang> Anhaenge => Set<Anhang>();
    public DbSet<Maileingang> Maileingaenge => Set<Maileingang>();

    /// <summary>
    /// Ob zu neuen Verlaufseinträgen Mails vorgemerkt werden. Beim Anlegen
    /// der Testdaten ist das ausgeschaltet.
    /// </summary>
    public bool BenachrichtigungenAnlegen { get; set; } = true;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BenachrichtigungenVormerken();
        SuchtexteAktualisieren();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BenachrichtigungenVormerken();
        SuchtexteAktualisieren();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Merkt zu jedem neuen Verlaufseintrag, der jemanden betrifft, eine Mail
    /// vor. Das geschieht hier an einer Stelle, damit keine Seite es
    /// vergessen kann. Wer die Mail bekommt und was darin steht, entscheidet
    /// sich beim Versand (siehe Mailtexte).
    /// </summary>
    private void BenachrichtigungenVormerken()
    {
        if (!BenachrichtigungenAnlegen)
            return;

        var neu = ChangeTracker.Entries<Verlaufseintrag>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .Where(v => v.Art switch
            {
                VerlaufArt.Erfasst or VerlaufArt.NachrichtAnKunden or VerlaufArt.Abgeschlossen => true,
                VerlaufArt.AntwortDesKunden or VerlaufArt.VomKundenWiederGeoeffnet => true,
                VerlaufArt.Weitergeleitet => v.Auftrag.BevorzugterBearbeiterId is not null,
                _ => false,
            })
            .ToList();

        foreach (var eintrag in neu)
        {
            Benachrichtigungen.Add(new Benachrichtigung
            {
                Verlaufseintrag = eintrag,
                ErstelltAm = eintrag.Zeitpunkt,
                NaechsterVersuchAm = eintrag.Zeitpunkt,
            });

            // Beim Erfassen geht die erste Mail an den Kunden. Wer als
            // bevorzugter Bearbeiter genannt ist, bekommt eine eigene.
            if (eintrag.Art == VerlaufArt.Erfasst && eintrag.Auftrag.BevorzugterBearbeiterId is not null)
            {
                Benachrichtigungen.Add(new Benachrichtigung
                {
                    Verlaufseintrag = eintrag,
                    AnBevorzugtenBearbeiter = true,
                    ErstelltAm = eintrag.Zeitpunkt,
                    NaechsterVersuchAm = eintrag.Zeitpunkt,
                });
            }
        }

        GruppenmailsVormerken();
    }

    /// <summary>
    /// Liegt ein Auftrag neu in einer Gruppe, weil er dort erfasst oder
    /// dorthin weitergeleitet wurde, bekommt jedes Mitglied eine Mail, das
    /// sie für diese Gruppe eingeschaltet hat. Ausgenommen sind, wer den
    /// Eintrag selbst ausgelöst hat, und der bevorzugte Bearbeiter: Der
    /// bekommt schon seine eigene Mail.
    /// </summary>
    private void GruppenmailsVormerken()
    {
        var neuInGruppe = ChangeTracker.Entries<Verlaufseintrag>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            // Wer beim Erfassen gleich selbst übernimmt, legt nichts in die Gruppe.
            .Where(v => v.Art is (VerlaufArt.Erfasst or VerlaufArt.Weitergeleitet) && v.Auftrag.Status == AuftragStatus.Offen)
            .ToList();

        foreach (var eintrag in neuInGruppe)
        {
            var auftrag = eintrag.Auftrag;
            var gruppe = auftrag.GruppeId;
            var empfaenger = Gruppenmitglieder
                .Where(m => m.GruppeId == gruppe && m.MailBeiNeuemAuftrag)
                .Select(m => m.BenutzerId)
                .ToList();

            foreach (var benutzer in empfaenger.Where(id => id != eintrag.BenutzerId && id != auftrag.BevorzugterBearbeiterId))
            {
                Benachrichtigungen.Add(new Benachrichtigung
                {
                    Verlaufseintrag = eintrag,
                    EmpfaengerId = benutzer,
                    ErstelltAm = eintrag.Zeitpunkt,
                    NaechsterVersuchAm = eintrag.Zeitpunkt,
                });
            }
        }
    }

    private void SuchtexteAktualisieren()
    {
        foreach (var eintrag in ChangeTracker.Entries<Kunde>())
        {
            if (eintrag.State is EntityState.Added or EntityState.Modified)
                eintrag.Entity.SuchtextAktualisieren();
        }

        foreach (var eintrag in ChangeTracker.Entries<Inventargeraet>())
        {
            if (eintrag.State is EntityState.Added or EntityState.Modified)
                eintrag.Entity.SuchtextAktualisieren();
        }
    }

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
            // Die Spalten heißen, wie EF sie anfangs aus den Namen der Listen gebildet hat.
            e.HasMany(g => g.Mitglieder)
                .WithMany(b => b.Gruppen)
                .UsingEntity<Gruppenmitglied>(
                    r => r.HasOne<Benutzer>().WithMany().HasForeignKey(m => m.BenutzerId),
                    l => l.HasOne<Gruppe>().WithMany().HasForeignKey(m => m.GruppeId),
                    j =>
                    {
                        j.ToTable("Gruppenmitglieder");
                        j.HasKey(m => new { m.GruppeId, m.BenutzerId });
                        j.Property(m => m.GruppeId).HasColumnName("GruppenId");
                        j.Property(m => m.BenutzerId).HasColumnName("MitgliederId");
                    });
        });

        model.Entity<Kunde>(e =>
        {
            e.ToTable("Kunden");
            e.Property(k => k.Name).HasMaxLength(200);
            e.Property(k => k.Email).HasMaxLength(320);
            e.Property(k => k.Telefon).HasMaxLength(50);
            e.Property(k => k.Organisationseinheit).HasMaxLength(200);
            e.Property(k => k.Anmeldename).HasMaxLength(200);
            e.Property(k => k.Raum).HasMaxLength(100);
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

        model.Entity<Verlaufseintrag>(e =>
        {
            e.ToTable("Verlauf");
            e.HasMany(v => v.Anhaenge).WithOne(a => a.Verlaufseintrag).HasForeignKey(a => a.VerlaufseintragId);
        });

        model.Entity<Maileingang>(e =>
        {
            e.ToTable("Maileingaenge");
            e.Property(m => m.Kennung).HasMaxLength(500);
            e.Property(m => m.Von).HasMaxLength(320);
            e.Property(m => m.Betreff).HasMaxLength(300);
            e.Property(m => m.Hinweis).HasMaxLength(1000);
            e.HasIndex(m => m.Kennung).IsUnique();
        });

        model.Entity<Anhang>(e =>
        {
            e.ToTable("Anhaenge");
            e.Property(a => a.Dateiname).HasMaxLength(255);
            e.Property(a => a.Inhaltstyp).HasMaxLength(200);
            e.Property(a => a.Ablagename).HasMaxLength(100);
            e.Property(a => a.Bildtyp).HasMaxLength(50);
        });

        model.Entity<Inventargeraet>(e =>
        {
            e.ToTable("Geraete");
            e.Property(g => g.Inventarnummer).HasMaxLength(50);
            e.Property(g => g.Nummernschluessel).HasMaxLength(50);
            e.Property(g => g.Bezeichnung).HasMaxLength(200);
            e.Property(g => g.Typ).HasMaxLength(100);
            e.Property(g => g.Raum).HasMaxLength(100);
            e.Property(g => g.ExterneKennung).HasMaxLength(200);
            e.Property(g => g.Quelle).HasMaxLength(100);
            e.HasIndex(g => g.Nummernschluessel).IsUnique();
        });

        model.Entity<Benachrichtigung>(e =>
        {
            e.ToTable("Benachrichtigungen");
            e.Property(b => b.An).HasMaxLength(320);
            e.Property(b => b.Betreff).HasMaxLength(300);
            e.Property(b => b.Hinweis).HasMaxLength(1000);
            e.HasIndex(b => new { b.Status, b.NaechsterVersuchAm });
        });

        // Nichts wird mitgelöscht. Aufträge und ihr Verlauf bleiben erhalten,
        // Stammdaten werden deaktiviert statt gelöscht.
        foreach (var fk in model.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
