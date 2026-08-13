namespace ReclamosMDP.API.DTOs;

public class CategoriaDatosDto
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Icono { get; set; } = "";
    public string Color { get; set; } = "";
    public string? SeccionLocal { get; set; }
    public List<FuenteCategoriaDto> Fuentes { get; set; } = new();
}

public class FuenteCategoriaDto
{
    public string Nombre { get; set; } = "";
    public string Url { get; set; } = "";
    public string Alcance { get; set; } = "";
    public bool Principal { get; set; }
}
