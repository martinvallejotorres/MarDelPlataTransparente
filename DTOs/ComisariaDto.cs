namespace ReclamosMDP.API.DTOs
{
    public class ComisariaDto
    {
        public string Nombre { get; set; } = "";

        public string Direccion { get; set; } = "";

        public string Telefono { get; set; } = "";

        public double Latitud { get; set; }

        public double Longitud { get; set; }

        public string Municipio { get; set; } = "";
    }
}
