using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Auftragsbezuege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auftragsbezuege",
                columns: table => new
                {
                    AuftragId = table.Column<int>(type: "INTEGER", nullable: false),
                    GenannterAuftragId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auftragsbezuege", x => new { x.AuftragId, x.GenannterAuftragId });
                    table.ForeignKey(
                        name: "FK_Auftragsbezuege_Auftraege_AuftragId",
                        column: x => x.AuftragId,
                        principalTable: "Auftraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auftragsbezuege_Auftraege_GenannterAuftragId",
                        column: x => x.GenannterAuftragId,
                        principalTable: "Auftraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auftragsbezuege_GenannterAuftragId",
                table: "Auftragsbezuege",
                column: "GenannterAuftragId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auftragsbezuege");
        }
    }
}
