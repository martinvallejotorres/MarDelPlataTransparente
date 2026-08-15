namespace ReclamosMDP.API.DTOs;

public class MedioAmbienteDto
{
    public int Anio { get; set; }
    public string Cobertura { get; set; } = "";
    public decimal ResiduosDispuestosToneladas { get; set; }
    public long CamionesDescargados { get; set; }
    public decimal MaterialIngresadoToneladas { get; set; }
    public decimal MaterialRecuperadoToneladas { get; set; }
    public decimal TasaRecuperacion { get; set; }
    public int MuestrasAgua { get; set; }
    public int ResultadosEColiDetectables { get; set; }
    public int PlayasMuestreadas { get; set; }
    public int PlayasSobreReferencia { get; set; }
    public List<MesAmbienteDto> Meses { get; set; } = [];
    public List<PlayaCalidadDto> Playas { get; set; } = [];
    public string FuenteUrl { get; set; } = "";
    public bool EsParcial { get; set; }
    public bool SerieOperativaPublicada { get; set; } = true;
    public string FechaCorte { get; set; } = "";
    public string AvisoPublicacion { get; set; } = "";
    public List<PublicacionParcialDto> PublicacionesParciales { get; set; } = [];
}

public class MesAmbienteDto
{
    public string Mes { get; set; } = "";
    public decimal ResiduosDispuestosToneladas { get; set; }
    public long CamionesDescargados { get; set; }
    public decimal MaterialIngresadoToneladas { get; set; }
    public decimal MaterialRecuperadoToneladas { get; set; }
}

public class PlayaCalidadDto
{
    public string Playa { get; set; } = "";
    public decimal Enterococos { get; set; }
    public bool SuperaReferencia { get; set; }
}
