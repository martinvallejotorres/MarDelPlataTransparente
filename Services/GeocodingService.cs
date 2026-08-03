using System.Globalization;
using System.Text.Json;

namespace ReclamosMDP.API.Services
{
    public class GeocodingService
    {
        private readonly HttpClient _httpClient;


        public GeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient;

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
                $"{direccion}, Mar del Plata, Argentina";


            var url =
                "https://nominatim.openstreetmap.org/search" +
                $"?q={Uri.EscapeDataString(busqueda)}" +
                "&format=json" +
                "&limit=1";


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
                return null;
            }


            return (
                double.Parse(
                    resultados[0].lat,
                    CultureInfo.InvariantCulture
                ),

                double.Parse(
                    resultados[0].lon,
                    CultureInfo.InvariantCulture
                )
            );
        }


        // ==========================================
        // COORDENADAS -> DIRECCION
        // ==========================================

        public async Task<string?> ObtenerDireccion(
            double latitud,
            double longitud)
        {
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


            Console.WriteLine(
                $"NOMINATIM REVERSE -> {contenido}"
            );


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


            return displayName.GetString();
        }
    }


    public class NominatimResult
    {
        public string lat { get; set; } = "";

        public string lon { get; set; } = "";
    }
}