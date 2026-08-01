namespace ReclamosMDP.API.Models
{
    public class HistorialEstado
    {
        public int Id { get; set; }


        public int ReclamoId { get; set; }

        public Reclamo Reclamo { get; set; } = null!;


        public string EstadoAnterior { get; set; } = "";

        public string EstadoNuevo { get; set; } = "";


        public DateTime Fecha { get; set; } = DateTime.UtcNow;


        public string UsuarioId { get; set; } = "";

        public ApplicationUser Usuario { get; set; } = null!;
    }
}