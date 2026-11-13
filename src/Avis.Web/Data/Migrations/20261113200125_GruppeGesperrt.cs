using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GruppeGesperrt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Gesperrt",
                table: "Gruppen",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gesperrt",
                table: "Gruppen");
        }
    }
}
