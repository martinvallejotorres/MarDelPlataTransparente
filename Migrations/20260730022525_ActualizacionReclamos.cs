using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReclamosMDP.API.Migrations
{
    /// <inheritdoc />
    public partial class ActualizacionReclamos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Apoyos",
                table: "Reclamos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Reclamos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FotoUrl",
                table: "Reclamos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "Reclamos",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Apoyos",
                table: "Reclamos");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Reclamos");

            migrationBuilder.DropColumn(
                name: "FotoUrl",
                table: "Reclamos");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "Reclamos");
        }
    }
}
