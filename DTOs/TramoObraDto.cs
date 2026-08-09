public class TramoObraDto
{
    public string Descripcion { get; set; } = "";

    public string Calle { get; set; } = "";

    public string Desde { get; set; } = "";

    public string Hasta { get; set; } = "";

    public double? LatitudInicio { get; set; }

    public double? LongitudInicio { get; set; }

    public double? LatitudFin { get; set; }

    public double? LongitudFin { get; set; }
}