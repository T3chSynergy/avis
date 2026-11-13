using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Benachrichtigungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Benachrichtigungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VerlaufseintragId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NaechsterVersuchAm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GesendetAm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Versuche = table.Column<int>(type: "INTEGER", nullable: false),
                    An = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    Betreff = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Hinweis = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Benachrichtigungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Benachrichtigungen_Verlauf_VerlaufseintragId",
                        column: x => x.VerlaufseintragId,
                        principalTable: "Verlauf",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Benachrichtigungen_Status_NaechsterVersuchAm",
                table: "Benachrichtigungen",
                columns: new[] { "Status", "NaechsterVersuchAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Benachrichtigungen_VerlaufseintragId",
                table: "Benachrichtigungen",
                column: "VerlaufseintragId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Benachrichtigungen");
        }
    }
}
