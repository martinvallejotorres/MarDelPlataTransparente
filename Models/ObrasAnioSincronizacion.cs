using System.ComponentModel.DataAnnotations;

namespace ReclamosMDP.API.Models;

public class ObrasAnioSincronizacion
{
    [Key]
    public int Anio { get; set; }
    public DateTime CompletadaUtc { get; set; }
    public int Cantidad { get; set; }
}
