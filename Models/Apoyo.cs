using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ReclamosMDP.API.Models
{
    public class Apoyo
    {
        
        public int Id { get; set; }

        // Reclamo relacionado
        public int ReclamoId { get; set; }

        public Reclamo Reclamo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";

        public ApplicationUser Usuario { get; set; } = null!;

        public DateTime Fecha { get; set; } = DateTime.UtcNow;
    }
}