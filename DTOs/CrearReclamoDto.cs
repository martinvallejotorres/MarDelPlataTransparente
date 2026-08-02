using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ReclamosMDP.API.DTOs
{
    public class CrearReclamoDto
    {
        [Required(ErrorMessage = "El título es obligatorio.")]
        [StringLength(
            100,
            MinimumLength = 5,
            ErrorMessage = "El título debe tener entre 5 y 100 caracteres."
        )]
        public string Titulo { get; set; } = "";

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Tipo { get; set; } = "";

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(
            1000,
            MinimumLength = 10,
            ErrorMessage = "La descripción debe tener entre 10 y 1000 caracteres."
        )]
        public string Descripcion { get; set; } = "";

        [Required(ErrorMessage = "La dirección es obligatoria.")]
        [StringLength(
            200,
            MinimumLength = 5,
            ErrorMessage = "La dirección debe tener entre 5 y 200 caracteres."
        )]
        public string Direccion { get; set; } = "";

        public IFormFile? Foto { get; set; }
    }
}