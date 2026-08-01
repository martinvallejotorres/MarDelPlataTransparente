using Microsoft.AspNetCore.Identity;

namespace ReclamosMDP.API.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Nombre { get; set; } = "";

        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        public ICollection<Apoyo> Apoyos { get; set; } = new List<Apoyo>();
    }
}
