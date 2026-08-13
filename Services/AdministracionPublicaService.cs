using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Caching.Memory;
using ReclamosMDP.API.DTOs;

namespace ReclamosMDP.API.Services;

public class AdministracionPublicaService
{
    private const string DatasetUrl = "https://datos.mardelplata.gob.ar/?q=dataset/planta-de-personal-administraci%C3%B3n-central";
    private const string DelegacionesUrl = "https://datos.mardelplata.gob.ar/sites/default/files/delegaciones.geojson";

    private static readonly IReadOnlyDictionary<int, (string Periodo, string Url)> Recursos =
        new Dictionary<int, (string, string)>
        {
            [2024] = ("Diciembre 2024", "https://datos.mardelplata.gob.ar/sites/default/files/Admin.%20Central_0.csv"),
            [2025] = ("Diciembre 2025", "https://datos.mardelplata.gob.ar/sites/default/files/adm%20central%2031-12-25.csv"),
            [2026] = ("Junio 2026", "https://datos.mardelplata.gob.ar/sites/default/files/adm%20central%2030-06-26.csv")
        };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public AdministracionPublicaService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public IReadOnlyCollection<int> ObtenerAnios() => Recursos.Keys.OrderDescending().ToArray();

    public async Task<AdministracionPublicaDto> ObtenerResumen(
        int anio,
        string planta = "todas",
        string? cargo = null)
    {
        if (!Recursos.TryGetValue(anio, out var recurso))
            throw new ArgumentOutOfRangeException(nameof(anio), "No hay un corte configurado para ese año.");

        var filas = await ObtenerFilas(anio);
        var filtradas = filas.Where(f =>
            (planta.Equals("todas", StringComparison.OrdinalIgnoreCase) ||
             NormalizarPlanta(f.Planta) == planta.ToUpperInvariant()) &&
            (string.IsNullOrWhiteSpace(cargo) ||
             f.Descripcion.Contains(cargo.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var tendencias = new List<TendenciaAdministracionDto>();
        foreach (var otroAnio in Recursos.Keys.Order())
        {
            var corte = await ObtenerFilas(otroAnio);
            tendencias.Add(new TendenciaAdministracionDto
            {
                Anio = otroAnio,
                Agentes = corte.Select(f => f.Legajo).Where(x => x.Length > 0).Distinct().Count(),
                Designaciones = corte.Count
            });
        }

        return new AdministracionPublicaDto
        {
            Anio = anio,
            Periodo = recurso.Periodo,
            TotalAgentes = filtradas.Select(f => f.Legajo).Where(x => x.Length > 0).Distinct().Count(),
            TotalDesignaciones = filtradas.Count,
            PlantaPermanente = filtradas.Count(f => NormalizarPlanta(f.Planta) == "P"),
            PlantaTemporaria = filtradas.Count(f => NormalizarPlanta(f.Planta) == "T"),
            OtrosTiposPlanta = filtradas.Count(f => !new[] { "P", "T" }.Contains(NormalizarPlanta(f.Planta))),
            FuenteUrl = DatasetUrl,
            PorPlanta = Agrupar(filtradas, f => EtiquetaPlanta(f.Planta), 8),
            PorCargaHoraria = Agrupar(filtradas, f => NormalizarModulo(f.Modulo), 10),
            PorNivel = Agrupar(filtradas, f => string.IsNullOrWhiteSpace(f.Nivel) ? "Sin nivel" : $"Nivel {f.Nivel.Trim()}", 10),
            CargosFrecuentes = Agrupar(filtradas, f => LimpiarEtiqueta(f.Descripcion), 10),
            Tendencia = tendencias
        };
    }

    public async Task<string> ObtenerDelegacionesGeoJson()
    {
        return await _cache.GetOrCreateAsync("administracion-delegaciones-geojson", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            using var request = CrearRequest(DelegacionesUrl);
            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }) ?? "{\"type\":\"FeatureCollection\",\"features\":[]}";
    }

    private async Task<List<FilaPersonal>> ObtenerFilas(int anio)
    {
        return await _cache.GetOrCreateAsync($"administracion-personal-{anio}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            using var request = CrearRequest(Recursos[anio].Url);
            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();

            using var stream = new MemoryStream(bytes);
            // Los cortes históricos mezclan Windows-1252 y UTF-8. Latin1 conserva
            // los bytes y RepararTexto corrige únicamente las secuencias UTF-8 dobles.
            using var reader = new StreamReader(stream, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                BadDataFound = null,
                MissingFieldFound = null,
                HeaderValidated = null,
                TrimOptions = TrimOptions.Trim
            });

            var resultado = new List<FilaPersonal>();
            await csv.ReadAsync();
            while (await csv.ReadAsync())
            {
                resultado.Add(new FilaPersonal(
                    Campo(csv, 0),
                    Campo(csv, 3),
                    Campo(csv, 4),
                    Campo(csv, 5),
                    RepararTexto(Campo(csv, 6))));
            }

            return resultado;
        }) ?? new List<FilaPersonal>();
    }

    private static HttpRequestMessage CrearRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 MDPTransparente/1.0");
        request.Headers.Referrer = new Uri(DatasetUrl);
        return request;
    }

    private static string Campo(CsvReader csv, int indice)
    {
        return csv.TryGetField<string>(indice, out var valor) && valor != null
            ? valor.Trim()
            : "";
    }

    private static List<SerieAdministracionDto> Agrupar(
        IEnumerable<FilaPersonal> filas,
        Func<FilaPersonal, string> selector,
        int limite) => filas
        .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
        .Select(g => new SerieAdministracionDto { Etiqueta = g.Key, Cantidad = g.Count() })
        .OrderByDescending(x => x.Cantidad)
        .ThenBy(x => x.Etiqueta)
        .Take(limite)
        .ToList();

    private static string NormalizarPlanta(string valor) => (valor ?? "").Trim().ToUpperInvariant();
    private static string EtiquetaPlanta(string valor) => NormalizarPlanta(valor) switch
    {
        "P" => "Permanente",
        "T" => "Temporaria",
        "C" => "Contratada",
        _ => "Otra / sin informar"
    };
    private static string NormalizarModulo(string valor) => string.IsNullOrWhiteSpace(valor) ? "Sin informar" : valor.Trim().ToLowerInvariant();
    private static string LimpiarEtiqueta(string valor) => string.IsNullOrWhiteSpace(valor) ? "Sin descripción" : valor.Trim();

    private static string RepararTexto(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || !valor.Contains('Ã')) return valor;
        try { return Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(valor)); }
        catch { return valor; }
    }

    private sealed record FilaPersonal(string Legajo, string Planta, string Nivel, string Modulo, string Descripcion);
}
