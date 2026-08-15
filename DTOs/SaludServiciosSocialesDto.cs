namespace ReclamosMDP.API.DTOs;

public class SaludServiciosSocialesDto
{
    public int Anio { get; set; }
    public string Cobertura { get; set; } = "";
    public long Consultas { get; set; }
    public int CentrosConActividad { get; set; }
    public int Especialidades { get; set; }
    public List<SerieSaludDto> Meses { get; set; } = [];
    public List<SerieSaludDto> PorCentro { get; set; } = [];
    public List<SerieSaludDto> PorEspecialidad { get; set; } = [];
    public List<string> Centros { get; set; } = [];
    public List<string> Prestaciones { get; set; } = [];
    public List<AtencionSaludDto> Detalle { get; set; } = [];
    public string FuenteUrl { get; set; } = "";
    public bool EsParcial { get; set; }
    public bool SerieOperativaPublicada { get; set; } = true;
    public string FechaCorte { get; set; } = "";
    public string AvisoPublicacion { get; set; } = "";
    public List<PublicacionParcialDto> PublicacionesParciales { get; set; } = [];
}

public class AtencionSaludDto
{
    public string Centro { get; set; } = "";
    public string Prestacion { get; set; } = "";
    public List<long> Meses { get; set; } = [];
}

public class SerieSaludDto
{
    public string Nombre { get; set; } = "";
    public long Cantidad { get; set; }
}
