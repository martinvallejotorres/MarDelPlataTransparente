using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReclamosMDP.API.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurarRelacionesApoyos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Apoyos_ReclamoId_UsuarioId",
                table: "Apoyos");

            migrationBuilder.DropColumn(
                name: "Apoyos",
                table: "Reclamos");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Apoyos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Apoyos_ApplicationUserId",
                table: "Apoyos",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Apoyos_ReclamoId",
                table: "Apoyos",
                column: "ReclamoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Apoyos_AspNetUsers_ApplicationUserId",
                table: "Apoyos",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Apoyos_AspNetUsers_ApplicationUserId",
                table: "Apoyos");

            migrationBuilder.DropIndex(
                name: "IX_Apoyos_ApplicationUserId",
                table: "Apoyos");

            migrationBuilder.DropIndex(
                name: "IX_Apoyos_ReclamoId",
                table: "Apoyos");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Apoyos");

            migrationBuilder.AddColumn<int>(
                name: "Apoyos",
                table: "Reclamos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Apoyos_ReclamoId_UsuarioId",
                table: "Apoyos",
                columns: new[] { "ReclamoId", "UsuarioId" },
                unique: true);
        }
    }
}
