using ReclamosMDP.API.DTOs;

namespace ReclamosMDP.API.Services
{
    public class SeguridadService
    {
        public Task<SeguridadDto> ObtenerResumen()
        {
            var datos =
                new SeguridadDto
                {
                    Anio = 2026,

                    Camaras = 1383,

                    PresupuestoMantenimientoCamaras =
                        3960000000m,

                    Licitacion =
                        "Licitación Pública 02/26",

                    Descripcion =
                        "Mantenimiento del sistema municipal de cámaras de seguridad.",

                    Fuente =
                        "Municipalidad de General Pueyrredon / Boletín Oficial de la Provincia de Buenos Aires",

                    FechaActualizacion =
                        new DateTime(
                            2026,
                            6,
                            30,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc
                        )
                };


            return Task.FromResult(
                datos
            );
        }
    }
}