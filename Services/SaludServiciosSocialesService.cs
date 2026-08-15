using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Caching.Memory;
using ReclamosMDP.API.DTOs;

namespace ReclamosMDP.API.Services;

public class SaludServiciosSocialesService
{
    private const string Dataset = "https://datos.mardelplata.gob.ar/?q=dataset/consultas-por-especialidad-m%C3%A9dica";
    private const string Consultas2025 = "https://datos.mardelplata.gob.ar/sites/default/files/Especialidades%20medicas%20por%20CAPS%20y%20por%20mes%20a%C3%B1o%202025%281%29.csv";
    private const string CentrosGeoJson = "https://datos.mardelplata.gob.ar/sites/default/files/centros-salud.geojson";
    private static readonly string[] Meses = ["01-ene", "02-feb", "03-mar", "04-abr", "05-may", "06-jun", "07-jul", "08-ago", "09-sep", "10-oct", "11-nov", "12-dic"];
    private static readonly string[] NombresMes = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly PortalDatosAbiertosService _portalDatos;

    public SaludServiciosSocialesService(HttpClient http, IMemoryCache cache, PortalDatosAbiertosService portalDatos)
    {
        _http = http;
        _cache = cache;
        _portalDatos = portalDatos;
    }

    public IReadOnlyCollection<int> ObtenerAnios() => [2026, 2025];

    public async Task<SaludServiciosSocialesDto> ObtenerResumen(int anio)
    {
        if (!ObtenerAnios().Contains(anio)) throw new ArgumentOutOfRangeException(nameof(anio));
        if (anio == 2026)
        {
            var publicaciones = await _portalDatos.DescubrirRecursos(2026, "salud-y-servicios-sociales-4");
            return new SaludServiciosSocialesDto
            {
                Anio = 2026, EsParcial = true, SerieOperativaPublicada = false,
                FechaCorte = publicaciones.FirstOrDefault()?.Cobertura ?? "Sin serie estadística 2026 publicada",
                Cobertura = "2026 parcial · el portal aún no publicó consultas por CAPS o especialidad para este período",
                AvisoPublicacion = publicaciones.Count > 0
                    ? "Se detectaron recursos oficiales 2026. Se muestran separados de la serie anual cerrada."
                    : "El municipio todavía no publicó una serie 2026 comparable. Se conserva 2025 como último año disponible.",
                PublicacionesParciales = publicaciones, FuenteUrl = Dataset
            };
        }
        return await _cache.GetOrCreateAsync($"salud-resumen-{anio}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            var filas = await LeerCsv(Consultas2025);
            var meses = Meses.Select((mes, i) => new SerieSaludDto
            {
                Nombre = NombresMes[i],
                Cantidad = filas.Sum(f => Entero(Valor(f, mes)))
            }).ToList();
            var porCentro = Agrupar(filas, "Establecimiento");
            var porEspecialidad = Agrupar(filas, "Prestacion", "Prestación");
            var detalle = filas.Select(f => new AtencionSaludDto
            {
                Centro = Limpiar(Valor(f, "Establecimiento")),
                Prestacion = Limpiar(new[] { Valor(f, "Prestacion"), Valor(f, "Prestación") }.FirstOrDefault(x => x.Length > 0) ?? ""),
                Meses = Meses.Select(m => Entero(Valor(f, m))).ToList()
            }).Where(x => x.Centro.Length > 0 && x.Prestacion.Length > 0).ToList();
            return new SaludServiciosSocialesDto
            {
                Anio = anio,
                Cobertura = "Enero–diciembre 2025 · consultas informadas por establecimiento, prestación y mes",
                Consultas = meses.Sum(x => x.Cantidad),
                CentrosConActividad = porCentro.Count,
                Especialidades = porEspecialidad.Count,
                Meses = meses,
                PorCentro = porCentro.OrderByDescending(x => x.Cantidad).ToList(),
                PorEspecialidad = porEspecialidad.OrderByDescending(x => x.Cantidad).ToList(),
                Centros = porCentro.Select(x => x.Nombre).OrderBy(x => x).ToList(),
                Prestaciones = porEspecialidad.Select(x => x.Nombre).OrderBy(x => x).ToList(),
                Detalle = detalle,
                FuenteUrl = Dataset
            };
        }) ?? new SaludServiciosSocialesDto { Anio = anio, FuenteUrl = Dataset };
    }

    public async Task<string> ObtenerCentrosGeoJson() =>
        await _cache.GetOrCreateAsync("salud-centros-geojson", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await DescargarTexto(CentrosGeoJson);
        }) ?? "{\"type\":\"FeatureCollection\",\"features\":[]}";

    private static List<SerieSaludDto> Agrupar(List<Dictionary<string, string>> filas, params string[] claves) => filas
        .Select(f => new { Nombre = claves.Select(c => Valor(f, c)).FirstOrDefault(v => v.Length > 0) ?? "", Total = Entero(Valor(f, "Total")) })
        .Where(x => x.Nombre.Length > 0)
        .GroupBy(x => Limpiar(x.Nombre), StringComparer.OrdinalIgnoreCase)
        .Select(g => new SerieSaludDto { Nombre = g.Key, Cantidad = g.Sum(x => x.Total) })
        .ToList();

    private async Task<List<Dictionary<string, string>>> LeerCsv(string url)
    {
        var texto = await DescargarTexto(url);
        using var reader = new StringReader(texto);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";", BadDataFound = null, MissingFieldFound = null, HeaderValidated = null, TrimOptions = TrimOptions.Trim
        });
        var resultado = new List<Dictionary<string, string>>();
        if (!await csv.ReadAsync()) return resultado;
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        while (await csv.ReadAsync())
        {
            var fila = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers.Where(h => !string.IsNullOrWhiteSpace(h))) fila[Limpiar(header)] = csv.GetField(header)?.Trim() ?? "";
            resultado.Add(fila);
        }
        return resultado;
    }

    private async Task<string> DescargarTexto(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 MDPTransparente/1.0");
        request.Headers.Referrer = new Uri(Dataset);
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes).TrimStart('\uFEFF'); }
    }

    private static string Valor(Dictionary<string, string> fila, string clave) => fila.TryGetValue(Limpiar(clave), out var valor) ? valor : "";
    private static long Entero(string valor) => long.TryParse(valor.Replace(".", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    private static string Limpiar(string valor) => valor.Trim().TrimStart('¿', '�').Replace("CAPS=", "", StringComparison.OrdinalIgnoreCase);
}
