using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Start : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Benutzer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Anmeldename = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    IstBearbeiter = table.Column<bool>(type: "INTEGER", nullable: false),
                    IstAdministrator = table.Column<bool>(type: "INTEGER", nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Benutzer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gruppen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Beschreibung = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IstEingangsgruppe = table.Column<bool>(type: "INTEGER", nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gruppen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kategorien",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategorien", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Standorte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Standorte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gruppenmitglieder",
                columns: table => new
                {
                    GruppenId = table.Column<int>(type: "INTEGER", nullable: false),
                    MitgliederId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gruppenmitglieder", x => new { x.GruppenId, x.MitgliederId });
                    table.ForeignKey(
                        name: "FK_Gruppenmitglieder_Benutzer_MitgliederId",
                        column: x => x.MitgliederId,
                        principalTable: "Benutzer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Gruppenmitglieder_Gruppen_GruppenId",
                        column: x => x.GruppenId,
                        principalTable: "Gruppen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kunden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    Telefon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Organisationseinheit = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Anmeldename = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StandortId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExterneKennung = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Quelle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kunden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kunden_Standorte_StandortId",
                        column: x => x.StandortId,
                        principalTable: "Standorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Auftraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Betreff = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "TEXT", nullable: false),
                    KundeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ErfasstVonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Eingang = table.Column<int>(type: "INTEGER", nullable: false),
                    KategorieId = table.Column<int>(type: "INTEGER", nullable: true),
                    StandortId = table.Column<int>(type: "INTEGER", nullable: true),
                    Geraet = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Dringlichkeit = table.Column<int>(type: "INTEGER", nullable: false),
                    Wichtigkeit = table.Column<int>(type: "INTEGER", nullable: false),
                    Termin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    GruppeId = table.Column<int>(type: "INTEGER", nullable: false),
                    BearbeiterId = table.Column<int>(type: "INTEGER", nullable: true),
                    BevorzugterBearbeiterId = table.Column<int>(type: "INTEGER", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeaendertAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AbgeschlossenAm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auftraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auftraege_Benutzer_BearbeiterId",
                        column: x => x.BearbeiterId,
                        principalTable: "Benutzer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftraege_Benutzer_BevorzugterBearbeiterId",
                        column: x => x.BevorzugterBearbeiterId,
                        principalTable: "Benutzer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftraege_Benutzer_ErfasstVonId",
                        column: x => x.ErfasstVonId,
                        principalTable: "Benutzer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftraege_Gruppen_GruppeId",
                        column: x => x.GruppeId,
                        principalTable: "Gruppen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftraege_Kategorien_KategorieId",
                        column: x => x.KategorieId,
                        principalTable: "Kategorien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftraege_Kunden_KundeId",
                        column: x => x.KundeId,
                        principalTable: "Kunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftraege_Standorte_StandortId",
                        column: x => x.StandortId,
                        principalTable: "Standorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Verlauf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AuftragId = table.Column<int>(type: "INTEGER", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    Zeitpunkt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BenutzerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Beschreibung = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Verlauf", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Verlauf_Auftraege_AuftragId",
                        column: x => x.AuftragId,
                        principalTable: "Auftraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Verlauf_Benutzer_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "Benutzer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_BearbeiterId_AbgeschlossenAm",
                table: "Auftraege",
                columns: new[] { "BearbeiterId", "AbgeschlossenAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_BevorzugterBearbeiterId",
                table: "Auftraege",
                column: "BevorzugterBearbeiterId");

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_ErfasstVonId",
                table: "Auftraege",
                column: "ErfasstVonId");

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_GruppeId_AbgeschlossenAm",
                table: "Auftraege",
                columns: new[] { "GruppeId", "AbgeschlossenAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_KategorieId",
                table: "Auftraege",
                column: "KategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_KundeId",
                table: "Auftraege",
                column: "KundeId");

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_StandortId",
                table: "Auftraege",
                column: "StandortId");

            migrationBuilder.CreateIndex(
                name: "IX_Benutzer_Anmeldename",
                table: "Benutzer",
                column: "Anmeldename",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gruppen_Name",
                table: "Gruppen",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gruppenmitglieder_MitgliederId",
                table: "Gruppenmitglieder",
                column: "MitgliederId");

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_Anmeldename",
                table: "Kunden",
                column: "Anmeldename");

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_Email",
                table: "Kunden",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_StandortId",
                table: "Kunden",
                column: "StandortId");

            migrationBuilder.CreateIndex(
                name: "IX_Verlauf_AuftragId",
                table: "Verlauf",
                column: "AuftragId");

            migrationBuilder.CreateIndex(
                name: "IX_Verlauf_BenutzerId",
                table: "Verlauf",
                column: "BenutzerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gruppenmitglieder");

            migrationBuilder.DropTable(
                name: "Verlauf");

            migrationBuilder.DropTable(
                name: "Auftraege");

            migrationBuilder.DropTable(
                name: "Benutzer");

            migrationBuilder.DropTable(
                name: "Gruppen");

            migrationBuilder.DropTable(
                name: "Kategorien");

            migrationBuilder.DropTable(
                name: "Kunden");

            migrationBuilder.DropTable(
                name: "Standorte");
        }
    }
}
