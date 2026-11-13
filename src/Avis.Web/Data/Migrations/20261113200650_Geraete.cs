using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Geraete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventargeraetId",
                table: "Auftraege",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Geraete",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Inventarnummer = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Nummernschluessel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Typ = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StandortId = table.Column<int>(type: "INTEGER", nullable: true),
                    KundeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExterneKennung = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Quelle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Suchtext = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Geraete", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Geraete_Kunden_KundeId",
                        column: x => x.KundeId,
                        principalTable: "Kunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Geraete_Standorte_StandortId",
                        column: x => x.StandortId,
                        principalTable: "Standorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auftraege_InventargeraetId",
                table: "Auftraege",
                column: "InventargeraetId");

            migrationBuilder.CreateIndex(
                name: "IX_Geraete_KundeId",
                table: "Geraete",
                column: "KundeId");

            migrationBuilder.CreateIndex(
                name: "IX_Geraete_Nummernschluessel",
                table: "Geraete",
                column: "Nummernschluessel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Geraete_StandortId",
                table: "Geraete",
                column: "StandortId");

            migrationBuilder.AddForeignKey(
                name: "FK_Auftraege_Geraete_InventargeraetId",
                table: "Auftraege",
                column: "InventargeraetId",
                principalTable: "Geraete",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auftraege_Geraete_InventargeraetId",
                table: "Auftraege");

            migrationBuilder.DropTable(
                name: "Geraete");

            migrationBuilder.DropIndex(
                name: "IX_Auftraege_InventargeraetId",
                table: "Auftraege");

            migrationBuilder.DropColumn(
                name: "InventargeraetId",
                table: "Auftraege");
        }
    }
}
