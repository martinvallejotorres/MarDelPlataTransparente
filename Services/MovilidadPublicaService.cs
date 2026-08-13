using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Caching.Memory;
using ReclamosMDP.API.DTOs;

namespace ReclamosMDP.API.Services;

public class MovilidadPublicaService
{
    private const string Portal = "https://datos.mardelplata.gob.ar/";
    private const string Dataset = "https://datos.mardelplata.gob.ar/?q=search/field_topic/movilidad-y-transporte-1";
    private const string RecorridosGeoJson = "https://datos.mardelplata.gob.ar/sites/default/files/recorridos.geojson";
    private const string ParadasGeoJson = "https://datos.mardelplata.gob.ar/sites/default/files/paradas.geojson";
    private const string LineasCsv = "https://datos.mardelplata.gob.ar/sites/default/files/lineas-transporte-urbano.csv";

    private static readonly IReadOnlyDictionary<int, RecursosMovilidad> Recursos =
        new Dictionary<int, RecursosMovilidad>
        {
            [2023] = CrearRecursos(2023, "frecuencia-por-linea_2023.csv", "flota-modelo-y-empresa-2023.csv"),
            [2024] = CrearRecursos(2024, "frecuencias_2024.csv", "flota-modelo-y-empresa-2024.csv"),
            [2025] = CrearRecursos(2025, "frecuencias_2025.csv", "flota-modelo-y-empresa-2025.csv")
        };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public MovilidadPublicaService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public IReadOnlyCollection<int> ObtenerAnios() => Recursos.Keys.OrderDescending().ToArray();

    public async Task<MovilidadPublicaDto> ObtenerResumen(int anio)
    {
        if (!Recursos.TryGetValue(anio, out var recursos))
            throw new ArgumentOutOfRangeException(nameof(anio), "No hay datos de movilidad para ese año.");

        return await _cache.GetOrCreateAsync($"movilidad-resumen-{anio}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);

            var pasajerosTask = LeerCsv(recursos.Pasajeros);
            var recaudacionTask = LeerCsv(recursos.Recaudacion);
            var recorridosTask = LeerCsv(recursos.Recorridos);
            var frecuenciasTask = LeerCsv(recursos.Frecuencias);
            var flotaTask = LeerCsv(recursos.Flota);
            var lineasTask = LeerCsv(LineasCsv);
            await Task.WhenAll(pasajerosTask, recaudacionTask, recorridosTask, frecuenciasTask, flotaTask, lineasTask);

            var meses = CrearMeses(await pasajerosTask, await recaudacionTask, await recorridosTask);
            var lineas = (await lineasTask)
                .Select(f => new LineaTransporteDto
                {
                    Empresa = Valor(f, "Empresa"),
                    Linea = Valor(f, "Línea", "Linea")
                })
                .Where(x => x.Linea.Length > 0)
                .OrderBy(x => x.Linea, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var frecuencias = CrearFrecuencias(await frecuenciasTask);
            var flotaPorEmpresa = CrearFlota(await flotaTask);

            return new MovilidadPublicaDto
            {
                Anio = anio,
                Periodo = $"Enero–diciembre {anio}",
                PasajerosTotal = meses.Sum(x => x.Pasajeros),
                RecaudacionTotal = meses.Sum(x => x.Recaudacion),
                KilometrosTotal = meses.Sum(x => x.Kilometros),
                IpkPromedio = meses.Where(x => x.Ipk > 0).Select(x => x.Ipk).DefaultIfEmpty().Average(),
                FlotaTotal = flotaPorEmpresa.Sum(x => x.Unidades),
                CantidadLineas = lineas.Select(x => x.Linea).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Meses = meses,
                Frecuencias = frecuencias,
                FlotaPorEmpresa = flotaPorEmpresa,
                Lineas = lineas,
                FuenteUrl = Dataset
            };
        }) ?? new MovilidadPublicaDto { Anio = anio, FuenteUrl = Dataset };
    }

    public Task<string> ObtenerRecorridosGeoJson() => ObtenerGeoJson("movilidad-recorridos", RecorridosGeoJson);
    public Task<string> ObtenerParadasGeoJson() => ObtenerGeoJson("movilidad-paradas", ParadasGeoJson);

    private async Task<string> ObtenerGeoJson(string cacheKey, string url)
    {
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await DescargarTexto(url);
        }) ?? "{\"type\":\"FeatureCollection\",\"features\":[]}";
    }

    private async Task<List<Dictionary<string, string>>> LeerCsv(string url)
    {
        var texto = await DescargarTexto(url);
        using var reader = new StringReader(texto);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        });

        var filas = new List<Dictionary<string, string>>();
        if (!await csv.ReadAsync()) return filas;
        csv.ReadHeader();
        var encabezados = csv.HeaderRecord ?? [];
        while (await csv.ReadAsync())
        {
            var fila = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var encabezado in encabezados)
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
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes).TrimStart('\uFEFF');
        }
    }

    private static List<MesMovilidadDto> CrearMeses(
        List<Dictionary<string, string>> pasajeros,
        List<Dictionary<string, string>> recaudacion,
        List<Dictionary<string, string>> recorridos)
    {
        var meses = pasajeros.Select(f => new MesMovilidadDto
        {
            Mes = Valor(f, "MES", "Mes"),
            BoletoPlano = Entero(Valor(f, "BOLETO PLANO")),
            BoletoDiferencial = Entero(Valor(f, "BOLETO DIF", "BOLETO DIFERENCIAL")),
            BoletoSuburbano = Entero(Valor(f, "BOL SUBURBA", "BOLETO SUBURBANO")),
            GratuitosSube = Entero(Valor(f, "GRATUITOS SUBE", "GRATUITOS"))
        }).Where(x => x.Mes.Length > 0).ToList();

        foreach (var mes in meses)
        {
            var rec = recaudacion.FirstOrDefault(f => MismoMes(Valor(f, "MES", "Mes", ""), mes.Mes));
            if (rec != null)
                mes.Recaudacion = rec.Where(x => !x.Key.Equals("MES", StringComparison.OrdinalIgnoreCase) && x.Key.Length > 0)
                    .Sum(x => DecimalEntero(x.Value));

            var recorrido = recorridos.FirstOrDefault(f => MismoMes(Valor(f, "MES", "Mes"), mes.Mes));
            if (recorrido != null)
            {
                mes.Kilometros = Entero(Valor(recorrido, "KM", "KILOMETROS"));
                mes.Ipk = DecimalLocal(Valor(recorrido, "IPK"));
            }
        }
        return meses;
    }

    private static List<FrecuenciaLineaDto> CrearFrecuencias(List<Dictionary<string, string>> filas)
    {
        if (filas.Count == 0) return [];
        var columnaHora = filas[0].Keys.FirstOrDefault(k => k.Contains("Hora", StringComparison.OrdinalIgnoreCase));
        return filas[0].Keys
            .Where(k => k != columnaHora && k.Length > 0)
            .Select(linea => new FrecuenciaLineaDto
            {
                Linea = linea,
                ServiciosDiarios = filas.Sum(f => (int)Entero(Valor(f, linea)))
            })
            .OrderByDescending(x => x.ServiciosDiarios)
            .ThenBy(x => x.Linea)
            .ToList();
    }

    private static List<FlotaEmpresaDto> CrearFlota(List<Dictionary<string, string>> filas)
    {
        if (filas.Count == 0) return [];
        var ignorar = new[] { "MODELO", "TOTAL" };
        return filas[0].Keys
            .Where(k => !ignorar.Contains(k, StringComparer.OrdinalIgnoreCase) && k.Length > 0)
            .Select(empresa => new FlotaEmpresaDto
            {
                Empresa = empresa,
                Unidades = filas.Sum(f => (int)Entero(Valor(f, empresa)))
            })
            .Where(x => x.Unidades > 0)
            .OrderByDescending(x => x.Unidades)
            .ToList();
    }

    private static RecursosMovilidad CrearRecursos(int anio, string frecuencia, string flota) => new(
        $"{Portal}sites/default/files/pasajeros-transporte-urbano-{anio}.csv",
        anio == 2025
            ? $"{Portal}sites/default/files/Recaudaci%C3%B3n-transporte-urbano.A%C3%B1o%20{anio}_0.csv"
            : $"{Portal}sites/default/files/Recaudaci%C3%B3n-transporte-urbano.A%C3%B1o%20{anio}.csv",
        $"{Portal}sites/default/files/Recorrido-transporte-urbano.%20A%C3%B1o%20{anio}.csv",
        $"{Portal}sites/default/files/{frecuencia}",
        $"{Portal}sites/default/files/{flota}");

    private static string Valor(Dictionary<string, string> fila, params string[] claves)
    {
        foreach (var clave in claves)
            if (fila.TryGetValue(clave, out var valor)) return valor;
        return "";
    }

    private static long Entero(string valor) =>
        long.TryParse(valor.Replace(".", "").Replace(" ", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            ? numero : 0;

    private static decimal DecimalEntero(string valor) => Entero(valor);
    private static decimal DecimalLocal(string valor)
    {
        if (!decimal.TryParse(valor, NumberStyles.Any, CultureInfo.GetCultureInfo("es-AR"), out var numero))
            return 0;
        // Algunos meses históricos publicaron 181/184/176 en vez de 1,81/1,84/1,76.
        return numero > 20 ? numero / 100 : numero;
    }
    private static bool MismoMes(string a, string b) => a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record RecursosMovilidad(
        string Pasajeros,
        string Recaudacion,
        string Recorridos,
        string Frecuencias,
        string Flota);
}
