namespace ReclamosMDP.API.DTOs;

public class ComparacionTramoDto
{
    public string Calle { get; set; } = "";
    public string Desde { get; set; } = "";
    public string Hasta { get; set; } = "";
    public List<int> Anios { get; set; } = new();
    public List<int> Eventos { get; set; } = new();
}
