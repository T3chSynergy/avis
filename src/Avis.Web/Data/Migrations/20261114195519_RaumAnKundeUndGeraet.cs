using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RaumAnKundeUndGeraet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Raum",
                table: "Kunden",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Raum",
                table: "Geraete",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Raum",
                table: "Kunden");

            migrationBuilder.DropColumn(
                name: "Raum",
                table: "Geraete");
        }
    }
}
