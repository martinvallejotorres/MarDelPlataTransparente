namespace ReclamosMDP.API.DTOs
{
    public class ObraDetalleDto
    {
        public int EventoId { get; set; }

        public string Nombre { get; set; } = "";

        public string Organismo { get; set; } = "";

        public string Expediente { get; set; } = "";

        public string PresupuestoTexto { get; set; } = "";

        public string Plazo { get; set; } = "";

        public string UbicacionTexto { get; set; } = "";

        public string Estado { get; set; } = "";

        public string FuenteUrl { get; set; } = "";

        public decimal? PresupuestoOficial { get; set; }

        public decimal? GarantiaOferta { get; set; }

        public DateTime? FechaApertura { get; set; }

        public string Licitacion { get; set; } = "";

        public double? SuperficieM2 { get; set; }

        public int? FrentesTrabajo { get; set; }

        public string TipoGeometria { get; set; } = "";

        public List<DocumentoObraDto> Documentos { get; set; } = new();

        public List<UbicacionObraDto> Ubicaciones { get; set; } = new();
    }


    public class DocumentoObraDto
    {
        public string Nombre { get; set; } = "";

        public string Url { get; set; } = "";
    }
}