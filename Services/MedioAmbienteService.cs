using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Caching.Memory;
using ReclamosMDP.API.DTOs;

namespace ReclamosMDP.API.Services;

public class MedioAmbienteService
{
    private const string Portal = "https://datos.mardelplata.gob.ar/";
    private const string Dataset = "https://datos.mardelplata.gob.ar/?q=search/field_topic/medio-ambiente-27";
    private const string Residuos2024Y2025 = Portal + "sites/default/files/Residuos%20dispuestos-2024-2025.csv";
    private const string Recuperados2024 = Portal + "sites/default/files/Entradas-y-material%20recuperado-por-mes-2024.csv";
    private const string Agua2024 = Portal + "sites/default/files/Monitoreo%20-red-de-agua-a%C3%B1o-2024.csv";
    private const string Playas2024 = Portal + "sites/default/files/niveles-enterococos-2024.csv";
    private const string ArroyosGeoJson = Portal + "sites/default/files/arroyos.geojson";
    private const string EstacionesGeoJson = Portal + "sites/default/files/estaciones_monitoreo_aire.geojson";
    private const string PuntosAguaGeoJson = Portal + "sites/default/files/puntos-muestreo-agua.geojson";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public MedioAmbienteService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public IReadOnlyCollection<int> ObtenerAnios() => [2025, 2024];

    public async Task<MedioAmbienteDto> ObtenerResumen(int anio)
    {
        if (!ObtenerAnios().Contains(anio))
            throw new ArgumentOutOfRangeException(nameof(anio), "No hay datos ambientales para ese año.");

        return await _cache.GetOrCreateAsync($"medio-ambiente-{anio}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            var residuosTask = LeerCsv(Residuos2024Y2025);
            var recuperadosTask = anio == 2024 ? LeerCsv(Recuperados2024) : Task.FromResult(new List<Dictionary<string, string>>());
            var aguaTask = anio == 2024 ? LeerCsv(Agua2024) : Task.FromResult(new List<Dictionary<string, string>>());
            var playasTask = anio == 2024 ? LeerCsv(Playas2024) : Task.FromResult(new List<Dictionary<string, string>>());
            await Task.WhenAll(residuosTask, recuperadosTask, aguaTask, playasTask);

            var meses = CrearMeses(anio, await residuosTask, await recuperadosTask);
            var agua = await aguaTask;
            var playas = CrearPlayas(await playasTask);
            var muestrasConResultado = agua.Where(f => ValorContiene(f, "Escherichia").Length > 0).ToList();
            var recuperado = meses.Sum(x => x.MaterialRecuperadoToneladas);
            var ingresado = meses.Sum(x => x.MaterialIngresadoToneladas);

            return new MedioAmbienteDto
            {
                Anio = anio,
                Cobertura = anio == 2025
                    ? "Enero–abril 2025 · la fuente aún no publicó recuperación, agua ni playas para este año"
                    : "Mayo–diciembre 2024 para disposición · enero–diciembre para recuperación, agua y playas",
                ResiduosDispuestosToneladas = meses.Sum(x => x.ResiduosDispuestosToneladas),
                CamionesDescargados = meses.Sum(x => x.CamionesDescargados),
                MaterialIngresadoToneladas = ingresado,
                MaterialRecuperadoToneladas = recuperado,
                TasaRecuperacion = ingresado > 0 ? recuperado / ingresado * 100 : 0,
                MuestrasAgua = muestrasConResultado.Count,
                ResultadosEColiDetectables = muestrasConResultado.Count(f => EsEColiDetectable(ValorContiene(f, "Escherichia"))),
                PlayasMuestreadas = playas.Count,
                PlayasSobreReferencia = playas.Count(x => x.SuperaReferencia),
                Meses = meses,
                Playas = playas,
                FuenteUrl = Dataset
            };
        }) ?? new MedioAmbienteDto { Anio = anio, FuenteUrl = Dataset };
    }

    public Task<string> ObtenerArroyosGeoJson() => ObtenerGeoJson("ambiente-arroyos", ArroyosGeoJson);
    public Task<string> ObtenerEstacionesGeoJson() => ObtenerGeoJson("ambiente-estaciones", EstacionesGeoJson);
    public Task<string> ObtenerPuntosAguaGeoJson() => ObtenerGeoJson("ambiente-puntos-agua", PuntosAguaGeoJson);

    private async Task<string> ObtenerGeoJson(string clave, string url) =>
        await _cache.GetOrCreateAsync(clave, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await DescargarTexto(url);
        }) ?? "{\"type\":\"FeatureCollection\",\"features\":[]}";

    private static List<MesAmbienteDto> CrearMeses(int anio, List<Dictionary<string, string>> residuos,
        List<Dictionary<string, string>> recuperados)
    {
        var sufijo = anio.ToString()[2..];
        var meses = residuos
            .Where(f => Valor(f, "Periodo").EndsWith(sufijo, StringComparison.OrdinalIgnoreCase))
            .Select(f => new MesAmbienteDto
            {
                Mes = NombreMes(Valor(f, "Periodo")),
                ResiduosDispuestosToneladas = NumeroEnteroLocal(Valor(f, "Total (Tn)")),
                CamionesDescargados = (long)NumeroEnteroLocal(Valor(f, "Camiones Descargados"))
            }).ToList();

        foreach (var fila in recuperados)
        {
            var mes = meses.FirstOrDefault(x => x.Mes.Equals(NombreMes(Valor(fila, "Periodo")), StringComparison.OrdinalIgnoreCase));
            if (mes == null)
            {
                mes = new MesAmbienteDto { Mes = NombreMes(Valor(fila, "Periodo")) };
                meses.Add(mes);
            }
            mes.MaterialIngresadoToneladas = DecimalLocal(Valor(fila, "Entradas (Tn)"));
            mes.MaterialRecuperadoToneladas = DecimalLocal(Valor(fila, "Recuperado (Tn)"));
        }

        var orden = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
        return meses.Where(x => x.Mes.Length > 0).OrderBy(x => Array.IndexOf(orden, x.Mes)).ToList();
    }

    private static List<PlayaCalidadDto> CrearPlayas(List<Dictionary<string, string>> filas) => filas
        .Select(f => new PlayaCalidadDto
        {
            Playa = Valor(f, "Playa"),
            Enterococos = DecimalLocal(ValorContiene(f, "Log media geom enterococo"))
        })
        .Where(x => x.Playa.Length > 0)
        .Select(x => { x.SuperaReferencia = x.Enterococos > 35; return x; })
        .OrderByDescending(x => x.Enterococos)
        .ToList();

    private async Task<List<Dictionary<string, string>>> LeerCsv(string url)
    {
        var texto = await DescargarTexto(url);
        using var reader = new StringReader(texto);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";", BadDataFound = null, MissingFieldFound = null, HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        });
        var filas = new List<Dictionary<string, string>>();
        if (!await csv.ReadAsync()) return filas;
        csv.ReadHeader();
        var encabezados = csv.HeaderRecord ?? [];
        while (await csv.ReadAsync())
        {
            var fila = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var encabezado in encabezados.Where(x => !string.IsNullOrWhiteSpace(x)))
                fila[encabezado.Trim()] = csv.GetField(encabezado)?.Trim() ?? "";
            filas.Add(fila);
        }
        return filas;
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

    private static string Valor(Dictionary<string, string> fila, string clave) => fila.TryGetValue(clave, out var valor) ? valor : "";
    private static string ValorContiene(Dictionary<string, string> fila, string texto) =>
        fila.FirstOrDefault(x => x.Key.Contains(texto, StringComparison.OrdinalIgnoreCase)).Value ?? "";
    private static decimal DecimalLocal(string valor) => decimal.TryParse(valor.Replace(" ", ""), NumberStyles.Any,
        CultureInfo.GetCultureInfo("es-AR"), out var numero) ? numero : 0;
    private static decimal NumeroEnteroLocal(string valor) => decimal.TryParse(valor.Replace(".", "").Replace(" ", ""),
        NumberStyles.Any, CultureInfo.InvariantCulture, out var numero) ? numero : 0;
    private static bool EsEColiDetectable(string valor) => valor.Length > 0 && !valor.Trim().StartsWith("<", StringComparison.Ordinal);
    private static string NombreMes(string periodo)
    {
        if (periodo.Length < 3) return "";
        var mes = periodo[..3].ToLowerInvariant();
        return char.ToUpperInvariant(mes[0]) + mes[1..];
    }
}
