using CsvHelper;
using CsvHelper.Configuration;
using ReclamosMDP.API.DTOs;
using System.Globalization;
using System.Net.Http.Json;

namespace ReclamosMDP.API.Services
{
    public class ComisariasService
    {
        private readonly HttpClient _httpClient;

        public ComisariasService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MarDelPlataTransparente/1.0"
            );
        }


        public async Task<List<ComisariaDto>> ObtenerComisarias()
        {
            // Por ahora vamos a colocar acá la URL directa
            // del CSV oficial de Datos Abiertos PBA.
            //
            // Después lo mejoramos para obtenerla automáticamente
            // desde la API de CKAN.

            var url =
                await ObtenerUrlCsvActual();

            var csvTexto =
                await _httpClient.GetStringAsync(url);


            using var reader =
                new StringReader(csvTexto);


            var config =
                new CsvConfiguration(
                    CultureInfo.InvariantCulture
                )
                {
                    HasHeaderRecord = true,

                    MissingFieldFound = null,

                    HeaderValidated = null,

                    BadDataFound = null,

                    Delimiter = ","
                };


            using var csv =
                new CsvReader(
                    reader,
                    config
                );


            var registros =
                csv.GetRecords<dynamic>()
                    .ToList();


            var comisarias =
                new List<ComisariaDto>();


            foreach (var registro in registros)
            {
                var datos =
                    (IDictionary<string, object>)registro;


                string municipio =
                    ObtenerValor(
                        datos,
                        "municipio_nombre"
                    );


                if (!EsGeneralPueyrredon(municipio))
                {
                    continue;
                }


                string latitudTexto =
                    ObtenerValor(
                        datos,
                        "latitud"
                    );


                string longitudTexto =
                    ObtenerValor(
                        datos,
                        "longitud"
                    );


                if (
                    !double.TryParse(
                        latitudTexto,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double latitud
                    )
                    ||
                    !double.TryParse(
                        longitudTexto,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double longitud
                    )
                )
                {
                    continue;
                }


                comisarias.Add(
                    new ComisariaDto
                    {
                        Nombre =
                            ObtenerValor(
                                datos,
                                "dependencia"
                            ),

                        Direccion =
                            ObtenerValor(
                                datos,
                                "direccion"
                            ),

                        Telefono =
                            ObtenerValor(
                                datos,
                                "telefono"
                            ),

                        Municipio =
                            municipio,

                        Latitud =
                            latitud,

                        Longitud =
                            longitud
                    }
                );
            }


            return comisarias;
        }

        private async Task<string> ObtenerUrlCsvActual()
        {
            const string datasetId =
                "bf79faeb-cb8a-4444-bbbe-5dc39479aa4a";

            string apiUrl =
                $"https://catalogo.datos.gba.gob.ar/api/3/action/package_show?id={datasetId}";


            var respuesta =
                await _httpClient.GetAsync(apiUrl);


            respuesta.EnsureSuccessStatusCode();


            var json =
                await respuesta.Content.ReadFromJsonAsync<
                    System.Text.Json.JsonElement
                >();


            if (!json.GetProperty("success").GetBoolean())
            {
                throw new Exception(
                    "CKAN no pudo devolver el dataset de comisarías."
                );
            }


            var recursos =
                json
                    .GetProperty("result")
                    .GetProperty("resources");


            foreach (var recurso in recursos.EnumerateArray())
            {
                string formato =
                    recurso.TryGetProperty(
                        "format",
                        out var formatElement
                    )
                        ? formatElement.GetString() ?? ""
                        : "";


                string mimeType =
                    recurso.TryGetProperty(
                        "mimetype",
                        out var mimeElement
                    )
                        ? mimeElement.GetString() ?? ""
                        : "";


                bool esCsv =
                    formato.Equals(
                        "CSV",
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    mimeType.Contains(
                        "csv",
                        StringComparison.OrdinalIgnoreCase
                    );


                if (!esCsv)
                {
                    continue;
                }


                string? url =
                    recurso.GetProperty("url").GetString();


                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }


            throw new Exception(
                "No se encontró un recurso CSV para el dataset de comisarías."
            );
        }

        private static bool EsGeneralPueyrredon(
            string municipio
        )
        {
            if (
                string.IsNullOrWhiteSpace(
                    municipio
                )
            )
            {
                return false;
            }


            municipio =
                municipio
                    .Trim()
                    .ToUpperInvariant();


            return
                municipio.Contains(
                    "PUEYRREDON"
                )
                ||
                municipio.Contains(
                    "PUEYRREDÓN"
                );
        }



        private static string ObtenerValor(
            IDictionary<string, object> datos,
            string columna
        )
        {
            var clave =
                datos.Keys.FirstOrDefault(
                    k =>
                        string.Equals(
                            Normalizar(k),
                            Normalizar(columna),
                            StringComparison
                                .OrdinalIgnoreCase
                        )
                );


            if (clave == null)
            {
                return "";
            }


            return
                datos[clave]?.ToString()?.Trim()
                ?? "";
        }



        private static string Normalizar(
            string texto
        )
        {
            return texto
                .Trim()
                .ToLowerInvariant()
                .Replace("ó", "o")
                .Replace("í", "i")
                .Replace("é", "e")
                .Replace("á", "a")
                .Replace("ú", "u");
        }
    }
}
