using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReclamosMDP.API.Migrations
{
    /// <inheritdoc />
    public partial class RestoreUniqueApoyo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "Apoyos" AS duplicado
                USING "Apoyos" AS original
                WHERE duplicado."ReclamoId" = original."ReclamoId"
                  AND duplicado."UsuarioId" = original."UsuarioId"
                  AND duplicado."Id" > original."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Apoyos_ReclamoId",
                table: "Apoyos");

            migrationBuilder.CreateIndex(
                name: "IX_Apoyos_ReclamoId_UsuarioId",
                table: "Apoyos",
                columns: new[] { "ReclamoId", "UsuarioId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Apoyos_ReclamoId_UsuarioId",
                table: "Apoyos");

            migrationBuilder.CreateIndex(
                name: "IX_Apoyos_ReclamoId",
                table: "Apoyos",
                column: "ReclamoId");
        }
    }
}
