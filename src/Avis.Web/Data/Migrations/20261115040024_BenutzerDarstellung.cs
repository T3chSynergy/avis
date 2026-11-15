using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class BenutzerDarstellung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Darstellung",
                table: "Benutzer",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Darstellung",
                table: "Benutzer");
        }
    }
}
