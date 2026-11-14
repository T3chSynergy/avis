using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class StandortAlsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Zuerst die neue Form, solange die alte Liste noch da ist: Aus "Raum" wird der Standort,
            // und der Auftrag bekommt ihn als Text.
            migrationBuilder.RenameColumn(
                name: "Raum",
                table: "Kunden",
                newName: "Standort");

            migrationBuilder.RenameColumn(
                name: "Raum",
                table: "Geraete",
                newName: "Standort");

            migrationBuilder.AddColumn<string>(
                name: "Standort",
                table: "Auftraege",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            // Wer noch keinen Standort als Text hat, bekommt den Namen aus der bisherigen Auswahlliste, und die
            // Suche findet ihn. lower() wandelt in SQLite nur ASCII um; ein Umlaut am Wortanfang wird erst
            // beim nächsten Speichern des Eintrags gefunden.
            foreach (var tabelle in new[] { "Kunden", "Geraete" })
            {
                migrationBuilder.Sql($"""
                    UPDATE "{tabelle}"
                    SET "Standort" = (SELECT s."Name" FROM "Standorte" s WHERE s."Id" = "{tabelle}"."StandortId"),
                        "Suchtext" = trim("Suchtext" || ' ' || lower((SELECT s."Name" FROM "Standorte" s WHERE s."Id" = "{tabelle}"."StandortId")))
                    WHERE "Standort" IS NULL AND "StandortId" IS NOT NULL;
                    """);
            }
            migrationBuilder.Sql("""
                UPDATE "Auftraege"
                SET "Standort" = (SELECT s."Name" FROM "Standorte" s WHERE s."Id" = "Auftraege"."StandortId")
                WHERE "StandortId" IS NOT NULL;
                """);

            // Dann die Auswahlliste und die Verweise auf sie entfernen. SQLite baut dafür die drei Tabellen um.
            // Die Verweise werden vorher geleert, sonst ließe sich die Liste nicht löschen, solange sie jemand nennt.
            foreach (var tabelle in new[] { "Kunden", "Geraete", "Auftraege" })
                migrationBuilder.Sql($"""UPDATE "{tabelle}" SET "StandortId" = NULL WHERE "StandortId" IS NOT NULL;""");

            migrationBuilder.DropForeignKey(
                name: "FK_Auftraege_Standorte_StandortId",
                table: "Auftraege");

            migrationBuilder.DropForeignKey(
                name: "FK_Geraete_Standorte_StandortId",
                table: "Geraete");

            migrationBuilder.DropForeignKey(
                name: "FK_Kunden_Standorte_StandortId",
                table: "Kunden");

            migrationBuilder.DropTable(
                name: "Standorte");

            migrationBuilder.DropIndex(
                name: "IX_Kunden_StandortId",
                table: "Kunden");

            migrationBuilder.DropIndex(
                name: "IX_Geraete_StandortId",
                table: "Geraete");

            migrationBuilder.DropIndex(
                name: "IX_Auftraege_StandortId",
                table: "Auftraege");

            migrationBuilder.DropColumn(
                name: "StandortId",
                table: "Kunden");

            migrationBuilder.DropColumn(
                name: "StandortId",
                table: "Geraete");

            migrationBuilder.DropColumn(
                name: "StandortId",
                table: "Auftraege");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Standort",
                table: "Auftraege");

            migrationBuilder.RenameColumn(
                name: "Standort",
                table: "Kunden",
                newName: "Raum");

            migrationBuilder.RenameColumn(
                name: "Standort",
                table: "Geraete",
                newName: "Raum");

            migrationBuilder.AddColumn<int>(
                name: "StandortId",
                table: "Kunden",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StandortId",
                table: "Geraete",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StandortId",
                table: "Auftraege",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Standorte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Standorte", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kunden_StandortId",
                table: "Kunden",
                column: "StandortId");

            migrationBuilder.CreateIndex(
                name: "IX_Geraete_StandortId",
                table: "Geraete",
                column: "StandortId");

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_StandortId",
                table: "Auftraege",
                column: "StandortId");

            migrationBuilder.AddForeignKey(
                name: "FK_Auftraege_Standorte_StandortId",
                table: "Auftraege",
                column: "StandortId",
                principalTable: "Standorte",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Geraete_Standorte_StandortId",
                table: "Geraete",
                column: "StandortId",
                principalTable: "Standorte",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Kunden_Standorte_StandortId",
                table: "Kunden",
                column: "StandortId",
                principalTable: "Standorte",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
