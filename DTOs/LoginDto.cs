namespace ReclamosMDP.API.DTOs
{
    public class LoginDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        [System.ComponentModel.DataAnnotations.StringLength(254)]
        public string Email { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 6)]
        public string Password { get; set; } = "";
    }
}
