namespace ReclamosMDP.API.DTOs
{
    public class SeguridadDto
    {
        public int Anio { get; set; }

        public int Camaras { get; set; }

        public decimal PresupuestoMantenimientoCamaras { get; set; }

        public string Licitacion { get; set; } = "";

        public string Descripcion { get; set; } = "";

        public string Fuente { get; set; } = "";

        public DateTime FechaActualizacion { get; set; }
    }
}