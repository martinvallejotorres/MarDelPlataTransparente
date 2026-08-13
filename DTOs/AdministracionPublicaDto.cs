namespace ReclamosMDP.API.DTOs;

public class AdministracionPublicaDto
{
    public int Anio { get; set; }
    public string Periodo { get; set; } = "";
    public string Organismo { get; set; } = "Administración Central";
    public int TotalAgentes { get; set; }
    public int TotalDesignaciones { get; set; }
    public int PlantaPermanente { get; set; }
    public int PlantaTemporaria { get; set; }
    public int OtrosTiposPlanta { get; set; }
    public string FuenteUrl { get; set; } = "";
    public List<SerieAdministracionDto> PorPlanta { get; set; } = new();
    public List<SerieAdministracionDto> PorCargaHoraria { get; set; } = new();
    public List<SerieAdministracionDto> PorNivel { get; set; } = new();
    public List<SerieAdministracionDto> CargosFrecuentes { get; set; } = new();
    public List<TendenciaAdministracionDto> Tendencia { get; set; } = new();
}

public class SerieAdministracionDto
{
    public string Etiqueta { get; set; } = "";
    public int Cantidad { get; set; }
}

public class TendenciaAdministracionDto
{
    public int Anio { get; set; }
    public int Agentes { get; set; }
    public int Designaciones { get; set; }
}
