using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuftragSuchtext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Suchtext",
                table: "Verlauf",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Suchtext",
                table: "Auftraege",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Suchtext",
                table: "Verlauf");

            migrationBuilder.DropColumn(
                name: "Suchtext",
                table: "Auftraege");
        }
    }
}
