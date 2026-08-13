namespace ReclamosMDP.API.DTOs
{
    public class RegisterDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(80, MinimumLength = 2)]
        public string Nombre { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        [System.ComponentModel.DataAnnotations.StringLength(254)]
        public string Email { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 6)]
        public string Password { get; set; } = "";
    }
}
