namespace ReclamosMDP.API.Models
{
    public class Reclamo
    {
        public int Id { get; set; }

        public string? Tipo { get; set; }

        public string? Descripcion { get; set; }

        public string? Direccion { get; set; }

        public string? Zona { get; set; }

        public double Latitud { get; set; }

        public double Longitud { get; set; }

        public DateTime Fecha { get; set; }

        public string Titulo { get; set; } = "";

        public int Apoyos { get; set; } = 0;

        public string Estado { get; set; } = "Recibido";

        public string? FotoUrl { get; set; }
    }
}
