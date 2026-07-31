using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace ReclamosMDP.API.Services;

public class ZonaService
{
    private readonly List<(Geometry Geometry, string Nombre)> _barrios = new();

    public ZonaService(IWebHostEnvironment env)
    {
        var ruta = Path.Combine(
            env.ContentRootPath,
            "GeoData",
            "barrios.json"
        );

        var geoJson = File.ReadAllText(ruta);

        var reader = new GeoJsonReader();

        var featureCollection = reader.Read<FeatureCollection>(geoJson);

        foreach (var feature in featureCollection)
        {
            var nombre = feature.Attributes["soc_fomen"]?.ToString();

            if (string.IsNullOrWhiteSpace(nombre))
                continue;

            _barrios.Add((feature.Geometry, nombre));
        }
    }

    public string ObtenerZona(double latitud, double longitud)
    {
        // OJO: Geometry usa X = Longitud, Y = Latitud
        var punto = new Point(longitud, latitud);

        foreach (var barrio in _barrios)
        {
            if (barrio.Geometry.Contains(punto))
                return barrio.Nombre;
        }

        return "Zona desconocida";
    }
}