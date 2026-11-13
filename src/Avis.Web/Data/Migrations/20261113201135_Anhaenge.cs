using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avis.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Anhaenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Anhaenge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VerlaufseintragId = table.Column<int>(type: "INTEGER", nullable: false),
                    Dateiname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Inhaltstyp = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Groesse = table.Column<long>(type: "INTEGER", nullable: false),
                    Ablagename = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anhaenge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anhaenge_Verlauf_VerlaufseintragId",
                        column: x => x.VerlaufseintragId,
                        principalTable: "Verlauf",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anhaenge_VerlaufseintragId",
                table: "Anhaenge",
                column: "VerlaufseintragId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Anhaenge");
        }
    }
}
