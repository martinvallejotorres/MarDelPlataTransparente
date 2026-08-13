using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ReclamosMDP.API.Migrations
{
    /// <inheritdoc />
    public partial class PersistirObrasPublicas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObrasAniosSincronizados",
                columns: table => new
                {
                    Anio = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompletadaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObrasAniosSincronizados", x => x.Anio);
                });

            migrationBuilder.CreateTable(
                name: "ObrasPublicas",
                columns: table => new
                {
                    EventoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnioFuente = table.Column<int>(type: "integer", nullable: false),
                    MesFuente = table.Column<int>(type: "integer", nullable: true),
                    Nombre = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Organismo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Expediente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Licitacion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UbicacionTexto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FuenteUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Plazo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TipoGeometria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PresupuestoOficial = table.Column<decimal>(type: "numeric", nullable: true),
                    GarantiaOferta = table.Column<decimal>(type: "numeric", nullable: true),
                    FechaApertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuperficieM2 = table.Column<double>(type: "double precision", nullable: true),
                    FrentesTrabajo = table.Column<int>(type: "integer", nullable: true),
                    DocumentosJson = table.Column<string>(type: "text", nullable: false),
                    UbicacionesJson = table.Column<string>(type: "text", nullable: false),
                    EventosRelacionadosJson = table.Column<string>(type: "text", nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObrasPublicas", x => x.EventoId);
                });

            migrationBuilder.CreateTable(
                name: "ObrasTramos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObraEventoId = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Calle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Desde = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Hasta = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LatitudInicio = table.Column<double>(type: "double precision", nullable: true),
                    LongitudInicio = table.Column<double>(type: "double precision", nullable: true),
                    LatitudFin = table.Column<double>(type: "double precision", nullable: true),
                    LongitudFin = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObrasTramos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObrasTramos_ObrasPublicas_ObraEventoId",
                        column: x => x.ObraEventoId,
                        principalTable: "ObrasPublicas",
                        principalColumn: "EventoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObrasPublicas_AnioFuente",
                table: "ObrasPublicas",
                column: "AnioFuente");

            migrationBuilder.CreateIndex(
                name: "IX_ObrasTramos_ObraEventoId_Calle_Desde_Hasta",
                table: "ObrasTramos",
                columns: new[] { "ObraEventoId", "Calle", "Desde", "Hasta" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObrasAniosSincronizados");

            migrationBuilder.DropTable(
                name: "ObrasTramos");

            migrationBuilder.DropTable(
                name: "ObrasPublicas");
        }
    }
}
