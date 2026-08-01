using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReclamosMDP.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAdministradorReclamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdministradorId",
                table: "Reclamos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reclamos_AdministradorId",
                table: "Reclamos",
                column: "AdministradorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reclamos_AspNetUsers_AdministradorId",
                table: "Reclamos",
                column: "AdministradorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reclamos_AspNetUsers_AdministradorId",
                table: "Reclamos");

            migrationBuilder.DropIndex(
                name: "IX_Reclamos_AdministradorId",
                table: "Reclamos");

            migrationBuilder.DropColumn(
                name: "AdministradorId",
                table: "Reclamos");
        }
    }
}
