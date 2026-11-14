using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Abgleichlaeufe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Abgleichlaeufe",
                columns: table => new
                {
                    Art = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Zeitpunkt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LetzterErfolg = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Gelesen = table.Column<int>(type: "INTEGER", nullable: false),
                    Neu = table.Column<int>(type: "INTEGER", nullable: false),
                    Geaendert = table.Column<int>(type: "INTEGER", nullable: false),
                    Reaktiviert = table.Column<int>(type: "INTEGER", nullable: false),
                    Inaktiv = table.Column<int>(type: "INTEGER", nullable: false),
                    Fehler = table.Column<string>(type: "TEXT", nullable: true),
                    Hinweise = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abgleichlaeufe", x => x.Art);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Abgleichlaeufe");
        }
    }
}
