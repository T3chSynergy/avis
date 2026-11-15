using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GeraeteAmAuftrag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Zuerst die neue Tabelle, solange die alten Spalten noch da sind.
            migrationBuilder.CreateTable(
                name: "Auftragsgeraete",
                columns: table => new
                {
                    AuftragId = table.Column<int>(type: "INTEGER", nullable: false),
                    GeraetId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auftragsgeraete", x => new { x.AuftragId, x.GeraetId });
                    table.ForeignKey(
                        name: "FK_Auftragsgeraete_Auftraege_AuftragId",
                        column: x => x.AuftragId,
                        principalTable: "Auftraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftragsgeraete_Geraete_GeraetId",
                        column: x => x.GeraetId,
                        principalTable: "Geraete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auftragsgeraete_GeraetId",
                table: "Auftragsgeraete",
                column: "GeraetId");

            // Das bisher verknüpfte Gerät bleibt verknüpft. Verknüpft ist künftig, was im Text mit "INV" genannt
            // wird; deshalb kommt die Nennung ans Ende der Beschreibung. Sonst ginge die Verknüpfung verloren,
            // sobald jemand etwas zum Auftrag schreibt.
            migrationBuilder.Sql("""
                INSERT INTO "Auftragsgeraete" ("AuftragId", "GeraetId")
                SELECT "Id", "InventargeraetId" FROM "Auftraege" WHERE "InventargeraetId" IS NOT NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE "Auftraege"
                SET "Beschreibung" = "Beschreibung" || char(10) || char(10) || 'Gerät: INV '
                    || (SELECT g."Inventarnummer" FROM "Geraete" g WHERE g."Id" = "Auftraege"."InventargeraetId")
                WHERE "InventargeraetId" IS NOT NULL;
                """);

            // Was als Freitext zum Gerät stand, und ein Standort, der von dem des Kunden abwich, kommen ans
            // Ende der Beschreibung: Die Felder entfallen, die Angaben sollen es nicht. Der Freitext zum Gerät
            // steht schon im Suchtext; für den Standort gilt, was die Migration StandortAlsText zu lower() sagt.
            migrationBuilder.Sql("""
                UPDATE "Auftraege"
                SET "Beschreibung" = "Beschreibung" || char(10) || char(10) || 'Gerät: ' || trim("Geraet")
                WHERE "Geraet" IS NOT NULL AND trim("Geraet") <> '';
                """);
            migrationBuilder.Sql("""
                UPDATE "Auftraege"
                SET "Beschreibung" = "Beschreibung" || char(10) || char(10) || 'Standort: ' || "Standort",
                    "Suchtext" = "Suchtext" || ' standort: ' || lower("Standort")
                WHERE "Standort" IS NOT NULL
                  AND "Standort" <> COALESCE((SELECT k."Standort" FROM "Kunden" k WHERE k."Id" = "Auftraege"."KundeId"), '');
                """);

            // Dann die alten Spalten entfernen. SQLite baut dafür die Tabelle der Aufträge um.
            migrationBuilder.DropForeignKey(
                name: "FK_Auftraege_Geraete_InventargeraetId",
                table: "Auftraege");

            migrationBuilder.DropIndex(
                name: "IX_Auftraege_InventargeraetId",
                table: "Auftraege");

            migrationBuilder.DropColumn(
                name: "Geraet",
                table: "Auftraege");

            migrationBuilder.DropColumn(
                name: "InventargeraetId",
                table: "Auftraege");

            migrationBuilder.DropColumn(
                name: "Standort",
                table: "Auftraege");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auftragsgeraete");

            migrationBuilder.AddColumn<string>(
                name: "Geraet",
                table: "Auftraege",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InventargeraetId",
                table: "Auftraege",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Standort",
                table: "Auftraege",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_InventargeraetId",
                table: "Auftraege",
                column: "InventargeraetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Auftraege_Geraete_InventargeraetId",
                table: "Auftraege",
                column: "InventargeraetId",
                principalTable: "Geraete",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
