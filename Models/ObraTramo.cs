using System.ComponentModel.DataAnnotations;

namespace ReclamosMDP.API.Models;

public class ObraTramo
{
    public int Id { get; set; }
    public int ObraEventoId { get; set; }
    public ObraPublica Obra { get; set; } = null!;

    [MaxLength(300)] public string Descripcion { get; set; } = "";
    [MaxLength(120)] public string Calle { get; set; } = "";
    [MaxLength(120)] public string Desde { get; set; } = "";
    [MaxLength(120)] public string Hasta { get; set; } = "";
    public double? LatitudInicio { get; set; }
    public double? LongitudInicio { get; set; }
    public double? LatitudFin { get; set; }
    public double? LongitudFin { get; set; }
}
