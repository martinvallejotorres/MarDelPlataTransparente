using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace ReclamosMDP.API.Services
{
    public class GeocodingService
    {
        private static readonly SemaphoreSlim OverpassLock = new(1, 1);
        private static DateTime _ultimaConsultaOverpassUtc = DateTime.MinValue;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;


        public GeocodingService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;

            // Nominatim requiere identificar la aplicación
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MarDelPlataTransparente/1.0"
            );
        }


        // ==========================================
        // DIRECCION -> COORDENADAS
        // ==========================================

        public async Task<(double lat, double lon)?> ObtenerCoordenadas(
            string direccion)
        {
            var busqueda =
                $"{direccion}, Mar del Plata, General Pueyrredon, Argentina";

            var cacheKey = $"geocode:{busqueda.Trim().ToLowerInvariant()}";
            if (_cache.TryGetValue($"{cacheKey}:miss", out bool _))
            {
                return null;
            }
            if (_cache.TryGetValue(cacheKey, out ValueTuple<double, double> cache))
            {
                return (cache.Item1, cache.Item2);
            }


            var url =
                "https://nominatim.openstreetmap.org/search" +
                $"?q={Uri.EscapeDataString(busqueda)}" +
                "&format=json" +
                "&limit=5" +
                "&countrycodes=ar" +
                "&viewbox=-57.75,-37.85,-57.35,-38.15" +
                "&bounded=1";


            var response =
                await _httpClient.GetAsync(url);


            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"Nominatim search error: {(int)response.StatusCode} {response.StatusCode}"
                );

                return null;
            }


            var respuesta =
                await response.Content.ReadAsStringAsync();


            var resultados =
                JsonSerializer.Deserialize<List<NominatimResult>>(
                    respuesta
                );


            if (
                resultados == null ||
                resultados.Count == 0
            )
            {
                _cache.Set($"{cacheKey}:miss", true, TimeSpan.FromDays(7));
                return null;
            }

            foreach (var resultado in resultados)
            {
                if (!double.TryParse(resultado.lat, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var latitud) ||
                    !double.TryParse(resultado.lon, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var longitud) ||
                    !double.IsFinite(latitud) || !double.IsFinite(longitud) ||
                    latitud < -38.15 || latitud > -37.85 ||
                    longitud < -57.75 || longitud > -57.35)
                    continue;

                var coordenadas = (latitud, longitud);
                _cache.Set(cacheKey, coordenadas, TimeSpan.FromDays(30));
                return coordenadas;
            }

            _cache.Set($"{cacheKey}:miss", true, TimeSpan.FromDays(7));
            return null;
        }

        public async Task<(double lat, double lon)?> ObtenerInterseccionOsm(
            string calle,
            string transversal)
        {
            var nombres = new[] { calle.Trim().ToLowerInvariant(), transversal.Trim().ToLowerInvariant() }
                .OrderBy(x => x).ToArray();
            var cacheKey = $"overpass:{nombres[0]}|{nombres[1]}";
            if (_cache.TryGetValue(cacheKey, out ValueTuple<double, double> cache))
                return (cache.Item1, cache.Item2);
            if (_cache.TryGetValue($"{cacheKey}:miss", out bool _))
                return null;

            static string Escapar(string valor) => valor
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");

            var query =
                "[out:json][timeout:20][bbox:-38.15,-57.75,-37.85,-57.35];" +
                $"way[highway][name~\"{Escapar(calle)}\",i]->.principal;" +
                $"way[name~\"{Escapar(transversal)}\",i]->.transversal;" +
                "node(w.principal)(w.transversal);out;";
            var url = "https://overpass-api.de/api/interpreter?data=" +
                      Uri.EscapeDataString(query);

            try
            {
                await OverpassLock.WaitAsync();
                var espera = TimeSpan.FromMilliseconds(2500) -
                             (DateTime.UtcNow - _ultimaConsultaOverpassUtc);
                if (espera > TimeSpan.Zero) await Task.Delay(espera);
                using var response = await _httpClient.GetAsync(url);
                _ultimaConsultaOverpassUtc = DateTime.UtcNow;
                if (!response.IsSuccessStatusCode) return null;
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                foreach (var elemento in json.RootElement.GetProperty("elements").EnumerateArray())
                {
                    if (!elemento.TryGetProperty("lat", out var latNode) ||
                        !elemento.TryGetProperty("lon", out var lonNode)) continue;
                    var coordenadas = (latNode.GetDouble(), lonNode.GetDouble());
                    _cache.Set(cacheKey, coordenadas, TimeSpan.FromDays(30));
                    return coordenadas;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                Console.WriteLine($"Overpass intersection error: {ex.Message}");
            }
            finally
            {
                if (OverpassLock.CurrentCount == 0) OverpassLock.Release();
            }

            _cache.Set($"{cacheKey}:miss", true, TimeSpan.FromDays(7));
            return null;
        }


        // ==========================================
        // COORDENADAS -> DIRECCION
        // ==========================================

        public async Task<string?> ObtenerDireccion(
            double latitud,
            double longitud)
        {
            var cacheKey = $"reverse:{latitud:F6}:{longitud:F6}";
            if (_cache.TryGetValue(cacheKey, out string? direccionCache))
            {
                return direccionCache;
            }

            var lat =
                latitud.ToString(
                    CultureInfo.InvariantCulture
                );

            var lon =
                longitud.ToString(
                    CultureInfo.InvariantCulture
                );


            var url =
                "https://nominatim.openstreetmap.org/reverse" +
                "?format=jsonv2" +
                $"&lat={lat}" +
                $"&lon={lon}" +
                "&zoom=18" +
                "&addressdetails=1";


            var response =
                await _httpClient.GetAsync(url);


            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"Nominatim reverse error: {(int)response.StatusCode} {response.StatusCode}"
                );

                return null;
            }


            var contenido =
                await response.Content.ReadAsStringAsync();


            using var json =
                JsonDocument.Parse(contenido);


            if (
                !json.RootElement.TryGetProperty(
                    "display_name",
                    out var displayName
                )
            )
            {
                return null;
            }


            var direccion = displayName.GetString();
            if (!string.IsNullOrWhiteSpace(direccion))
            {
                _cache.Set(cacheKey, direccion, TimeSpan.FromDays(30));
            }

            return direccion;
        }
    }


    public class NominatimResult
    {
        public string lat { get; set; } = "";

        public string lon { get; set; } = "";
    }
}
