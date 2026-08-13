using System.ComponentModel.DataAnnotations;

namespace ReclamosMDP.API.Models;

public class ObraPublica
{
    [Key]
    public int EventoId { get; set; }

    public int AnioFuente { get; set; }
    public int? MesFuente { get; set; }

    [MaxLength(500)] public string Nombre { get; set; } = "";
    [MaxLength(200)] public string Organismo { get; set; } = "";
    [MaxLength(120)] public string Expediente { get; set; } = "";
    [MaxLength(120)] public string Licitacion { get; set; } = "";
    public string UbicacionTexto { get; set; } = "";
    [MaxLength(80)] public string Estado { get; set; } = "";
    [MaxLength(1000)] public string FuenteUrl { get; set; } = "";
    [MaxLength(120)] public string Plazo { get; set; } = "";
    [MaxLength(40)] public string TipoGeometria { get; set; } = "";

    public decimal? PresupuestoOficial { get; set; }
    public decimal? GarantiaOferta { get; set; }
    public DateTime? FechaApertura { get; set; }
    public double? SuperficieM2 { get; set; }
    public int? FrentesTrabajo { get; set; }
    public string DocumentosJson { get; set; } = "[]";
    public string UbicacionesJson { get; set; } = "[]";
    public string EventosRelacionadosJson { get; set; } = "[]";
    public DateTime ActualizadaUtc { get; set; }

    public List<ObraTramo> Tramos { get; set; } = new();
}
