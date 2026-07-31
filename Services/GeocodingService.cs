using System.Text.Json;
using System.Globalization;

namespace ReclamosMDP.API.Services
{
    public class GeocodingService
    {
        private readonly HttpClient _httpClient;


        public GeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }


        public async Task<(double lat, double lon)?> ObtenerCoordenadas(string direccion)
        {
            var url =
    $"https://nominatim.openstreetmap.org/search?q={direccion}, Mar del Plata, Argentina&format=json&limit=1";


            _httpClient.DefaultRequestHeaders.UserAgent.Clear();

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "ReclamosMDP/1.0"
            );


            var respuesta = await _httpClient.GetStringAsync(url);


            var resultados = JsonSerializer.Deserialize<List<NominatimResult>>(respuesta);


            if (resultados == null || resultados.Count == 0)
                return null;


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
    }


    public class NominatimResult
    {
        public string lat { get; set; }

        public string lon { get; set; }
    }
}