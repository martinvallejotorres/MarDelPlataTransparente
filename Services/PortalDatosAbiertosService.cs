using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using ReclamosMDP.API.DTOs;

namespace ReclamosMDP.API.Services;

public partial class PortalDatosAbiertosService
{
    private const string Portal = "https://datos.mardelplata.gob.ar/";
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public PortalDatosAbiertosService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<List<PublicacionParcialDto>> DescubrirRecursos(int anio, string categoria)
    {
        return await _cache.GetOrCreateAsync($"recursos-portal-{categoria}-{anio}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            var busqueda = $"{Portal}?q=search/field_topics/field_topic/{categoria}&query={anio}&sort_by=changed";
            var htmlBusqueda = await Descargar(busqueda);
            var datasets = DatasetRegex().Matches(htmlBusqueda).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();
            var resultado = new List<PublicacionParcialDto>();
            foreach (var dataset in datasets)
            {
                var pagina = await Descargar($"{Portal}?q=dataset/{dataset}");
                foreach (Match archivo in CsvRegex().Matches(pagina))
                {
                    var inicioLista = pagina.LastIndexOf("<li", archivo.Index, StringComparison.OrdinalIgnoreCase);
                    var inicio = inicioLista >= 0 ? inicioLista : Math.Max(0, archivo.Index - 900);
                    var contextoHtml = pagina[inicio..archivo.Index];
                    var contexto = LimpiarHtml(contextoHtml);
                    if (!Regex.IsMatch(contexto, $@"Año\s*{anio}\b", RegexOptions.IgnoreCase)) continue;
                    var url = WebUtility.HtmlDecode(archivo.Groups[1].Value);
                    var nombre = NombreDataset(dataset);
                    var cobertura = ExtraerCobertura(contexto, anio);
                    var contenido = await Descargar(url);
                    if (contenido.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)) continue;
                    var registros = Math.Max(0, contenido.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1);
                    resultado.Add(new PublicacionParcialDto { Nombre = nombre, Registros = registros, Cobertura = cobertura, FuenteUrl = url });
                }
            }
            return resultado.GroupBy(x => x.FuenteUrl).Select(g => g.First()).ToList();
        }) ?? [];
    }

    private async Task<string> Descargar(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 MDPTransparente/1.0");
        request.Headers.Referrer = new Uri(Portal);
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes).TrimStart('\uFEFF'); }
    }

    private static string LimpiarHtml(string html) => Regex.Replace(WebUtility.HtmlDecode(TagRegex().Replace(html, " ")), @"\s+", " ").Trim();
    private static string NombreDataset(string dataset) =>
        System.Globalization.CultureInfo.GetCultureInfo("es-AR").TextInfo.ToTitleCase(
            Uri.UnescapeDataString(dataset).Replace('-', ' ').ToLowerInvariant());
    private static string ExtraerCobertura(string contexto, int anio)
    {
        var match = Regex.Match(contexto, @"Al mes de\s+([A-Za-zÁÉÍÓÚáéíóúñÑ]+)", RegexOptions.IgnoreCase);
        return match.Success ? $"Hasta {match.Groups[1].Value} de {anio}" : $"Publicación parcial {anio}";
    }

    [GeneratedRegex("href=\"/\\?q=dataset/([^\"/#]+)\"", RegexOptions.IgnoreCase)] private static partial Regex DatasetRegex();
    [GeneratedRegex("href=\"(https://datos\\.mardelplata\\.gob\\.ar/sites/default/files/[^\"]+\\.csv)\"", RegexOptions.IgnoreCase)] private static partial Regex CsvRegex();
    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRegex();
}
