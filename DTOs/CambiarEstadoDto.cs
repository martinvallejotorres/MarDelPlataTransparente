namespace ReclamosMDP.API.DTOs
{
    public class CambiarEstadoDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(40)]
        public string Estado { get; set; } = "";
    }
}
