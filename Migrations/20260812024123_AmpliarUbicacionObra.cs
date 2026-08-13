using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReclamosMDP.API.Migrations
{
    /// <inheritdoc />
    public partial class AmpliarUbicacionObra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UbicacionTexto",
                table: "ObrasPublicas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UbicacionTexto",
                table: "ObrasPublicas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
