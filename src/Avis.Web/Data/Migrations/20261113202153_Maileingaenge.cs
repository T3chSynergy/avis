using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Maileingaenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Maileingaenge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kennung = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EmpfangenAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Von = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    Betreff = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Ergebnis = table.Column<int>(type: "INTEGER", nullable: false),
                    AuftragId = table.Column<int>(type: "INTEGER", nullable: true),
                    Hinweis = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maileingaenge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Maileingaenge_Auftraege_AuftragId",
                        column: x => x.AuftragId,
                        principalTable: "Auftraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Maileingaenge_AuftragId",
                table: "Maileingaenge",
                column: "AuftragId");

            migrationBuilder.CreateIndex(
                name: "IX_Maileingaenge_Kennung",
                table: "Maileingaenge",
                column: "Kennung",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Maileingaenge");
        }
    }
}
