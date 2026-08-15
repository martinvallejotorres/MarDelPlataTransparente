namespace ReclamosMDP.API.DTOs;

public class MovilidadPublicaDto
{
    public int Anio { get; set; }
    public string Periodo { get; set; } = "";
    public long PasajerosTotal { get; set; }
    public decimal RecaudacionTotal { get; set; }
    public long KilometrosTotal { get; set; }
    public decimal IpkPromedio { get; set; }
    public int FlotaTotal { get; set; }
    public int CantidadLineas { get; set; }
    public List<MesMovilidadDto> Meses { get; set; } = [];
    public List<FrecuenciaLineaDto> Frecuencias { get; set; } = [];
    public List<FlotaEmpresaDto> FlotaPorEmpresa { get; set; } = [];
    public List<LineaTransporteDto> Lineas { get; set; } = [];
    public string FuenteUrl { get; set; } = "";
    public bool EsParcial { get; set; }
    public bool SerieOperativaPublicada { get; set; } = true;
    public string FechaCorte { get; set; } = "";
    public string AvisoPublicacion { get; set; } = "";
    public List<PublicacionParcialDto> PublicacionesParciales { get; set; } = [];
}

public class MesMovilidadDto
{
    public string Mes { get; set; } = "";
    public long BoletoPlano { get; set; }
    public long BoletoDiferencial { get; set; }
    public long BoletoSuburbano { get; set; }
    public long GratuitosSube { get; set; }
    public decimal Recaudacion { get; set; }
    public long Kilometros { get; set; }
    public decimal Ipk { get; set; }
    public long Pasajeros => BoletoPlano + BoletoDiferencial + BoletoSuburbano + GratuitosSube;
}

public class FrecuenciaLineaDto
{
    public string Linea { get; set; } = "";
    public int ServiciosDiarios { get; set; }
}

public class FlotaEmpresaDto
{
    public string Empresa { get; set; } = "";
    public int Unidades { get; set; }
}

public class LineaTransporteDto
{
    public string Empresa { get; set; } = "";
    public string Linea { get; set; } = "";
}
