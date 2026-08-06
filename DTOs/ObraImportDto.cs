namespace ReclamosMDP.API.DTOs
{
    public class ObraImportDto
    {
        public int EventoId { get; set; }

        public string Nombre { get; set; } = "";

        public string Organismo { get; set; } = "";

        public string Fecha { get; set; } = "";

        public string FuenteUrl { get; set; } = "";
    }
}