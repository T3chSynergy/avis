using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class KundeAktiv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Von Hand auf true gestellt: Wer schon in der Tabelle steht, ist ein aktiver Kunde.
            // EF schlägt für eine neue Ja/Nein-Spalte von sich aus false vor.
            migrationBuilder.AddColumn<bool>(
                name: "Aktiv",
                table: "Kunden",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aktiv",
                table: "Kunden");
        }
    }
}
