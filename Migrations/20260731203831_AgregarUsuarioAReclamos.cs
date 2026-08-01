using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReclamosMDP.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUsuarioAReclamos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Reclamos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reclamos_UsuarioId",
                table: "Reclamos",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reclamos_AspNetUsers_UsuarioId",
                table: "Reclamos",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reclamos_AspNetUsers_UsuarioId",
                table: "Reclamos");

            migrationBuilder.DropIndex(
                name: "IX_Reclamos_UsuarioId",
                table: "Reclamos");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Reclamos");
        }
    }
}
