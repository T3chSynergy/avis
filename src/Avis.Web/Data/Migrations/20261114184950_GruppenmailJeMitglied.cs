using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GruppenmailJeMitglied : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MailBeiNeuemAuftrag",
                table: "Gruppenmitglieder",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EmpfaengerId",
                table: "Benachrichtigungen",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MailBeiNeuemAuftrag",
                table: "Gruppenmitglieder");

            migrationBuilder.DropColumn(
                name: "EmpfaengerId",
                table: "Benachrichtigungen");
        }
    }
}
