using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using ReclamosMDP.API.DTOs;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using UglyToad.PdfPig;
using Microsoft.Extensions.Caching.Memory;


namespace ReclamosMDP.API.Services
{
    public class ObrasImportService
    {
        private const string BaseDocumentosMunicipales =
        "https://appsb.mardelplata.gob.ar";

        private readonly HttpClient _httpClient;

        private readonly GeocodingService _geocodingService;

        private readonly IMemoryCache _cache;

        public ObrasImportService(HttpClient httpClient, GeocodingService geocodingService, IMemoryCache cache)
        {
            _httpClient = httpClient;

            _httpClient
                .DefaultRequestHeaders
                .UserAgent
                .ParseAdd(
                    "MarDelPlataTransparente/1.0"
                );

            _geocodingService =
              geocodingService;

            _cache = cache;
        }

        private async Task<ObraDetalleDto>ObtenerDetalleObraBase(int eventoId)
        {
            string url =
                $"https://appsb.mardelplata.gob.ar/Consultas/Licitaciones/GetLic.asp?Evnt={eventoId}";


            string html =
                await _httpClient
                    .GetStringAsync(
                        url
                    );


            var documento =
                new HtmlDocument();


            documento.LoadHtml(
                html
            );


            string textoPagina =
                HtmlEntity
                    .DeEntitize(
                        documento
                            .DocumentNode
                            .InnerText
                    );


            textoPagina =
                LimpiarTexto(
                    textoPagina
                );


            var detalle =
                new ObraDetalleDto
                {
                    EventoId =
                        eventoId,

                    FuenteUrl =
                        url,

                    Nombre =
                        ExtraerNombre(
                            documento,
                            textoPagina
                        ),

                    Organismo =
                        DetectarOrganismo(
                            textoPagina
                        ),

                    Plazo =
                        ExtraerCampo(
                            textoPagina,
                            "Plazo"
                        ),

                    UbicacionTexto =
                        ExtraerCampo(
                            textoPagina,
                            "Ubicación"
                        )
                };


            detalle.Documentos =
                ExtraerDocumentos(
                    documento
                );


            return detalle;
        }

        public async Task<List<ObraImportDto>> ObtenerObrasPeriodo(
    DateTime desde,
    DateTime hasta
)
        {
            var cacheKey =
                $"obras-periodo-{desde:yyyy-MM}-{hasta:yyyy-MM}";


            if (
                _cache.TryGetValue(
                    cacheKey,
                    out List<ObraImportDto>? obrasCache
                )
                &&
                obrasCache != null
            )
            {
                return obrasCache;
            }


            var obras =
                new List<ObraImportDto>();


            var fechaActual =
                new DateTime(
                    desde.Year,
                    desde.Month,
                    1
                );


            var fechaFinal =
                new DateTime(
                    hasta.Year,
                    hasta.Month,
                    1
                );


            while (fechaActual <= fechaFinal)
            {
                try
                {
                    var obrasMes =
                        await ObtenerObras(
                            fechaActual.Year,
                            fechaActual.Month
                        );


                    obras.AddRange(
                        obrasMes
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"ERROR OBRAS {fechaActual.Month}/{fechaActual.Year} -> {ex.Message}"
                    );
                }


                fechaActual =
                    fechaActual.AddMonths(1);
            }


            var resultado =
                obras
                    .GroupBy(
                        obra => obra.EventoId
                    )
                    .Select(
                        grupo => grupo.First()
                    )
                    .OrderByDescending(
                        obra => obra.EventoId
                    )
                    .ToList();


            _cache.Set(
                cacheKey,
                resultado,
                TimeSpan.FromHours(12)
            );


            return resultado;
        }

        public async Task<List<ObraImportDto>>ObtenerObras(int anio, int mes)
        {
            string url =
                $"https://licitaciones.mardelplata.gob.ar/Calendario.php?nMonth={mes}&nYear={anio}";


            string html =
                await _httpClient
                    .GetStringAsync(url);


            var documento =
                new HtmlDocument();


            documento.LoadHtml(
                html
            );


            var obras =
                new List<ObraImportDto>();


            var enlaces =
                documento.DocumentNode
                    .SelectNodes(
                        "//a[contains(@href,'GetLic') or contains(@href,'Evnt=')]"
                    );


            if (enlaces == null)
            {
                return obras;
            }


            foreach (var enlace in enlaces)
            {
                string href =
                    enlace.GetAttributeValue(
                        "href",
                        ""
                    );


                if (
                    string.IsNullOrWhiteSpace(
                        href
                    )
                )
                {
                    continue;
                }


                int? eventoId =
                    ObtenerEventoId(
                        href
                    );


                if (!eventoId.HasValue)
                {
                    continue;
                }


                string nombre =
                    HtmlEntity
                        .DeEntitize(
                            enlace.InnerText
                        )
                        .Trim();


                if (
                    string.IsNullOrWhiteSpace(
                        nombre
                    )
                )
                {
                    continue;
                }


                if (!PareceObra(nombre))
                {
                    continue;
                }


                string fuenteUrl =
                    CrearUrlAbsoluta(
                        href
                    );


                obras.Add(
                    new ObraImportDto
                    {
                        EventoId =
                            eventoId.Value,

                        Nombre =
                            nombre,

                        Organismo =
                            DetectarOrganismo(
                                nombre
                            ),

                        Fecha =
                            "",

                        FuenteUrl =
                            fuenteUrl
                    }
                );
            }


            return obras
                .GroupBy(
                    x => x.EventoId
                )
                .Select(
                    x => x.First()
                )
                .ToList();
        }

        public async Task<ObraDetalleDto> ObtenerDetalleObra(int eventoId)
        {
            var detalle =
                await ObtenerDetalleObraBase(
                    eventoId
                );


            // ==========================================
            // ESTADO SEGÚN DOCUMENTACIÓN DISPONIBLE
            // ==========================================

            detalle.Estado =
                DetectarEstado(
                    detalle.Documentos
                );


            // ==========================================
            // DOCUMENTO PRINCIPAL
            // ==========================================

            var documentoPrincipal =
                BuscarDocumentoPrincipal(
                    detalle.Documentos
                );


            if (documentoPrincipal != null)
            {
                var textoPrincipal =
                    await ObtenerTextoPdf(
                        documentoPrincipal.Url,
                        eventoId
                    );


                var nombrePdf =
                    ExtraerNombreObra(
                        textoPrincipal
                    );


                if (!string.IsNullOrWhiteSpace(nombrePdf))
                {
                    detalle.Nombre =
                        nombrePdf;
                }


                detalle.Expediente =
                    ExtraerExpediente(
                        textoPrincipal
                    );


                detalle.PresupuestoOficial =
                    ExtraerPresupuestoOficial(
                        textoPrincipal
                    );


                detalle.GarantiaOferta =
                    ExtraerGarantiaOferta(
                        textoPrincipal
                    );


                detalle.FechaApertura =
                    ExtraerFechaApertura(
                        textoPrincipal
                    );


                detalle.Licitacion =
                    ExtraerLicitacion(
                        textoPrincipal
                    );

                detalle.Ubicaciones =
                    ExtraerUbicacionesPuntuales(
                        textoPrincipal
                    );

                if (
                    detalle.Ubicaciones.Count > 0
                )
                {
                    await GeocodificarUbicaciones(
                        detalle.Ubicaciones
                    );
                }
            }


            // ==========================================
            // ACTA DE APERTURA
            // Fallback para fecha / licitación
            // ==========================================

            var actaApertura =
                detalle.Documentos
                    .FirstOrDefault(
                        d =>
                            d.Nombre.Contains(
                                "ACTA DE APERTURA",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (actaApertura != null)
            {
                var textoActa =
                    await ObtenerTextoPdf(
                        actaApertura.Url,
                        eventoId
                    );


                if (detalle.FechaApertura == null)
                {
                    detalle.FechaApertura =
                        ExtraerFechaApertura(
                            textoActa
                        );
                }


                if (
                    string.IsNullOrWhiteSpace(
                        detalle.Licitacion
                    )
                )
                {
                    detalle.Licitacion =
                        ExtraerLicitacion(
                            textoActa
                        );
                }


                if (
                    string.IsNullOrWhiteSpace(
                        detalle.Expediente
                    )
                )
                {
                    detalle.Expediente =
                        ExtraerExpediente(
                            textoActa
                        );
                }
            }


            // ==========================================
            // DOCUMENTO DE PRESUPUESTO
            // ==========================================

            var documentoPresupuesto =
                detalle.Documentos
                    .FirstOrDefault(
                        d =>
                            d.Nombre.Contains(
                                "PRESUPUESTO",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (
                documentoPresupuesto != null
                &&
                detalle.PresupuestoOficial == null
            )
            {
                var textoPresupuesto =
                    await ObtenerTextoPdf(
                        documentoPresupuesto.Url,
                        eventoId
                    );


                detalle.PresupuestoOficial =
                    ExtraerPresupuestoOficial(
                        textoPresupuesto
                    );
            }


            // ==========================================
            // ESPECIFICACIONES TÉCNICAS
            // ==========================================

            var especificaciones =
                detalle.Documentos
                    .FirstOrDefault(
                        d =>
                            d.Nombre.Contains(
                                "ESP TECNICAS",
                                StringComparison.OrdinalIgnoreCase
                            )
                            ||
                            d.Nombre.Contains(
                                "ESPECIFICACIONES",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (especificaciones != null)
            {
                var textoEspecificaciones =
                    await ObtenerTextoPdf(
                        especificaciones.Url,
                        eventoId
                    );


                detalle.UbicacionTexto =
                    ExtraerUbicacionEspecificaciones(
                        textoEspecificaciones
                    );


                detalle.SuperficieM2 =
                    ExtraerSuperficieM2(
                        textoEspecificaciones
                    );


                detalle.FrentesTrabajo =
                    ExtraerFrentesTrabajo(
                        textoEspecificaciones
                    );


                detalle.TipoGeometria =
                    DetectarTipoGeometria(
                        detalle.UbicacionTexto
                    );
            }


            // ==========================================
            // FALLBACK DE UBICACIÓN PARA PLIEGOS MGP
            // ==========================================

            if (
                string.IsNullOrWhiteSpace(
                    detalle.UbicacionTexto
                )
                &&
                documentoPrincipal != null
            )
            {
                var textoPrincipal =
                    await ObtenerTextoPdf(
                        documentoPrincipal.Url,
                        eventoId
                    );


                detalle.UbicacionTexto =
                    ExtraerUbicacionEspecificaciones(
                        textoPrincipal
                    );


                if (
                    !string.IsNullOrWhiteSpace(
                        detalle.UbicacionTexto
                    )
                )
                {
                    detalle.TipoGeometria =
                        DetectarTipoGeometria(
                            detalle.UbicacionTexto
                        );
                }
            }


            var cacheKey =
             $"obra-detalle-{eventoId}";


            if (
                _cache.TryGetValue(
                    cacheKey,
                    out ObraDetalleDto? obraCache
                )
                &&
                obraCache != null
            )
            {
                return obraCache;
            }
            _cache.Set(
                cacheKey,
                detalle,
                TimeSpan.FromHours(12)
            );

            return detalle;
        }

        private static string DetectarEstado( List<DocumentoObraDto> documentos)
        {
            var tieneActaApertura =
                documentos.Any(
                    x =>
                        x.Nombre.Contains(
                            "ACTA DE APERTURA",
                            StringComparison.OrdinalIgnoreCase
                        )
                );


            if (tieneActaApertura)
            {
                return "Apertura realizada";
            }


            return "Licitada";
        }

        private async Task GeocodificarUbicaciones(List<UbicacionObraDto> ubicaciones)
        {
            foreach (var ubicacion in ubicaciones)
            {
                var direccion =
                    PrepararDireccionGeocoding(
                        ubicacion.Descripcion
                    );


                if (
                    string.IsNullOrWhiteSpace(
                        direccion
                    )
                )
                {
                    continue;
                }


                try
                {
                    var resultado =
                        await _geocodingService
                            .ObtenerCoordenadas(
                                direccion
                            );


                    if (resultado == null)
                    {
                        continue;
                    }

                    var lat =
                        resultado.Value.lat;

                    var lon =
                        resultado.Value.lon;

                    if (
                        !CoordenadaEsDeMarDelPlata(
                            lat,
                            lon
                        )
                    )
                    {
                        Console.WriteLine(
                            $"COORDENADA DESCARTADA -> {direccion} ({lat}, {lon})"
                        );

                        continue;
                    }

                    ubicacion.Latitud =
                        lat;

                    ubicacion.Longitud =
                        lon;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"ERROR GEOCODIFICANDO '{direccion}' -> {ex.Message}"
                    );
                }


                // Nominatim no debe recibir muchas
                // consultas seguidas.
                await Task.Delay(
                    1100
                );
            }
        }

        private static bool CoordenadaEsDeMarDelPlata(double lat, double lon)
        {
            return
                lat >= -38.15 &&
                lat <= -37.85 &&
                lon >= -57.75 &&
                lon <= -57.35;
        }

        private static string PrepararDireccionGeocoding(string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion))
            {
                return "";
            }

            var direccion =
                descripcion
                    .Trim()
                    .TrimEnd('.');


            direccion =
                direccion.Replace(
                    "Calle ",
                    "",
                    StringComparison.OrdinalIgnoreCase
                );


            direccion =
                direccion.Replace(
                    "cruce hacia ",
                    "",
                    StringComparison.OrdinalIgnoreCase
                );


            return direccion;
        }
        private static string ExtraerNombreObra(string texto)
        {
            var match =
                Regex.Match(
                    texto,
                    @"(?:LICITACI[ÓO]N\s+(?:P[ÚU]BLICA|PRIVADA)|CONCURSO\s+DE\s+PRECIOS|CONTRATACI[ÓO]N\s+DIRECTA).*?[“""]\s*(.+?)\s*[”""]",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline
                );

            if (!match.Success)
            {
                return "";
            }

            return LimpiarTexto(
                match.Groups[1].Value
            );
        }

        private static string DetectarTipoGeometria(string ubicacion)
        {
            if (string.IsNullOrWhiteSpace(ubicacion))
            {
                return "SinGeometriaPrecisa";
            }

            string texto =
                ubicacion.ToLowerInvariant();

            if (
                texto.Contains("ejido urbano")
                ||
                texto.Contains("partido de")
                ||
                texto.Contains("barrio")
                ||
                texto.Contains("zona")
            )
            {
                return "Zona";
            }

            if (
                texto.Contains("entre")
                ||
                texto.Contains("desde")
                ||
                texto.Contains("hasta")
            )
            {
                return "Tramo";
            }

            return "Punto";
        }

        public async Task<string> ObtenerTextoCaratula(int eventoId)
        {
            var detalle =
                await ObtenerDetalleObraBase(
                    eventoId
                );


            var caratula =
                detalle.Documentos
                    .FirstOrDefault(
                        d =>
                            d.Nombre.Contains(
                                "CARATULA",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (caratula == null)
            {
                throw new Exception(
                    "No se encontró la carátula de la licitación."
                );
            }


            return await ObtenerTextoPdf(
                caratula.Url,
                eventoId
            );
        }

        private static bool PareceObra(string texto)
        {
            texto = texto.ToLowerInvariant();

            // Palabras que claramente indican compras
            // o provisión de materiales/equipamiento.
            string[] excluir =
            {
                "adquisicion",
                "adquisición",
                "compra",
                "provisión",
                "provision",
                "vehiculo",
                "vehículo",
                "cemento",
                "arena",
                "piedra",
                "caños",
                "caños",
                "elementos de desgaste",
                "papel",

                // Servicios / mantenimiento que no
                // queremos mostrar como obra pública
                "ascensor",
                "ascensores",
                "enterratorio",
                "enterramiento",
                "excavación mecanizada de pozos",
                "excavacion mecanizada de pozos"
            };

            if (excluir.Any(palabra => texto.Contains(palabra)))
            {
                return false;
            }

            // Términos que sí representan una intervención
            // física sobre la ciudad.
            string[] incluir =
            {
                "bacheo",
                "pavimentacion",
                "pavimentación",
                "repavimentacion",
                "repavimentación",
                "fresado",
                "recapado",

                "construccion",
                "construcción",
                "reconstruccion",
                "reconstrucción",

                "cordon cuneta",
                "cordón cuneta",

                "vereda",
                "veredas",

                "rampa",
                "rampas",

                "ciclovia",
                "ciclovía",

                "plaza",
                "parque",

                "desagüe",
                "desague",

                "infraestructura vial"
            };

            return incluir.Any(
                palabra => texto.Contains(palabra)
            );
        }

        private async Task<string> ObtenerTextoPdf(string urlPdf, int eventoId)
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    urlPdf
                );


            request.Headers.Referrer =
                new Uri(
                    $"https://appsb.mardelplata.gob.ar/Consultas/Licitaciones/GetLic.asp?Evnt={eventoId}"
                );


            request.Headers.Accept.ParseAdd(
                "application/pdf"
            );


            request.Headers.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 Chrome/150.0 Safari/537.36"
            );


            using var response =
                await _httpClient.SendAsync(
                    request
                );


            response.EnsureSuccessStatusCode();


            byte[] pdfBytes =
                await response.Content
                    .ReadAsByteArrayAsync();


            bool esPdf =
                pdfBytes.Length >= 5 &&
                pdfBytes[0] == (byte)'%' &&
                pdfBytes[1] == (byte)'P' &&
                pdfBytes[2] == (byte)'D' &&
                pdfBytes[3] == (byte)'F' &&
                pdfBytes[4] == (byte)'-';


            if (!esPdf)
            {
                throw new Exception(
                    $"El recurso {urlPdf} no devolvió un PDF válido."
                );
            }


            using var memoria =
                new MemoryStream(
                    pdfBytes
                );


            using var pdf =
                PdfDocument.Open(
                    memoria
                );


            var paginas =
                new List<string>();


            foreach (var pagina in pdf.GetPages())
            {
                paginas.Add(
                    pagina.Text
                );
            }


            return string.Join(
                Environment.NewLine,
                paginas
            );
        }

        private static string LimpiarTexto(string texto)
        {
            return string.Join(
                " ",
                texto
                    .Split(
                        new[]
                        {
                    '\r',
                    '\n',
                    '\t'
                        },
                        StringSplitOptions
                            .RemoveEmptyEntries
                    )
                    .Select(
                        x => x.Trim()
                    )
                    .Where(
                        x =>
                            !string
                                .IsNullOrWhiteSpace(x)
                    )
            );
        }

        private static string ExtraerNombre(HtmlDocument documento, string texto)
        {
            var titulo =
                documento
                    .DocumentNode
                    .SelectSingleNode("//h1")
                ??
                documento
                    .DocumentNode
                    .SelectSingleNode("//h2")
                ??
                documento
                    .DocumentNode
                    .SelectSingleNode("//h3");


            if (titulo != null)
            {
                return HtmlEntity
                    .DeEntitize(
                        titulo.InnerText
                    )
                    .Trim();
            }


            return texto.Length > 150
                ? texto.Substring(0, 150)
                : texto;
        }

        private static string ExtraerCampo(string texto, string campo)
        {
            int indice =
                texto.IndexOf(
                    campo,
                    StringComparison
                        .OrdinalIgnoreCase
                );


            if (indice < 0)
            {
                return "";
            }


            string restante =
                texto.Substring(
                    indice + campo.Length
                );


            restante =
                restante
                    .TrimStart(
                        ':',
                        '-',
                        ' '
                    );


            int fin =
                restante.IndexOf("  ");


            if (fin > 0)
            {
                restante =
                    restante.Substring(
                        0,
                        fin
                    );
            }


            if (restante.Length > 150)
            {
                restante =
                    restante.Substring(
                        0,
                        150
                    );
            }


            return restante.Trim();
        }

        private List<DocumentoObraDto> ExtraerDocumentos(HtmlDocument documento)
        {
            var documentos =
                new List<DocumentoObraDto>();

            var enlaces =
                documento.DocumentNode
                    .SelectNodes("//a[@href]");

            if (enlaces == null)
                return documentos;


            foreach (var enlace in enlaces)
            {
                var href =
                    enlace.GetAttributeValue(
                        "href",
                        ""
                    );

                if (
                    string.IsNullOrWhiteSpace(href)
                    ||
                    !href.Contains(
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }


                string urlFinal;


                if (
                    href.StartsWith(
                        "http://",
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    href.StartsWith(
                        "https://",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    urlFinal = href;
                }
                else
                {
                    urlFinal =
                        new Uri(
                            new Uri(
                                BaseDocumentosMunicipales
                            ),
                            href
                        ).ToString();
                }

                    var nombre =
                    enlace.InnerText
                        .Trim();


                documentos.Add(
                    new DocumentoObraDto
                    {
                        Nombre = nombre,
                        Url = urlFinal
                    }
                );


                Console.WriteLine(
                    $"DOCUMENTO -> href: {href}"
                );

                Console.WriteLine(
                    $"DOCUMENTO -> url final: {urlFinal}"
                );
            }


            return documentos;
        }

        private static decimal? ExtraerPresupuestoOficial(string texto)
        {
            var match = Regex.Match(
                texto,
                @"PRESUPUESTO\s+OFICIAL:\s*\$\s*([\d\.\,]+)",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                return null;
            }

            string valor = match.Groups[1].Value
                .Replace(".", "")
                .Replace(",", ".");

            if (
                decimal.TryParse(
                    valor,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal resultado
                )
            )
            {
                return resultado;
            }

            return null;
        }

        private static decimal? ExtraerGarantiaOferta(string texto)
        {
            var match = Regex.Match(
                texto,
                @"GARANTIA\s+DE\s+OFERTA:\s*\$\s*([\d\.\,]+)",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                return null;
            }

            string valor = match.Groups[1].Value
                .Replace(".", "")
                .Replace(",", ".");

            if (
                decimal.TryParse(
                    valor,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal resultado
                )
            )
            {
                return resultado;
            }

            return null;
        }

        private static string ExtraerLicitacion(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return "";
            }


            var licitacion =
                Regex.Match(
                    texto,
                    @"LICITACI[ÓO]N\s+(P[ÚU]BLICA|PRIVADA)\s+(?:N[º°]?\s*)?(\d+)\s*/\s*(\d{2,4})",
                    RegexOptions.IgnoreCase
                );


            if (licitacion.Success)
            {
                var tipo =
                    licitacion.Groups[1].Value;

                var numero =
                    licitacion.Groups[2].Value;

                var anio =
                    licitacion.Groups[3].Value;


                var nombreTipo =
                    tipo.Contains(
                        "PRIVADA",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? "Licitación Privada"
                        : "Licitación Pública";


                return
                    $"{nombreTipo} {numero}/{anio}";
            }


            var directa =
                Regex.Match(
                    texto,
                    @"CONTRATACI[ÓO]N\s+DIRECTA\s+(?:N[º°]?\s*)?(\d+)\s*/\s*(\d{2,4})",
                    RegexOptions.IgnoreCase
                );


            if (directa.Success)
            {
                return
                    $"Contratación Directa " +
                    $"{directa.Groups[1].Value}/" +
                    $"{directa.Groups[2].Value}";
            }


            return "";
        }

        private static string ExtraerExpediente(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return "";
            }


            // EMVIAL: 24/C/2026
            var matchEmvial =
                Regex.Match(
                    texto,
                    @"EXPEDIENTE\s+(?:N[º°]?\s*)?(\d+\s*/\s*[A-Z]\s*/\s*\d{4})",
                    RegexOptions.IgnoreCase
                );


            if (matchEmvial.Success)
            {
                return LimpiarTexto(
                    matchEmvial.Groups[1].Value
                );
            }


            // MGP: Expediente Nº 326 Dígito 4 Año 2025
            var matchMgp =
                Regex.Match(
                    texto,
                    @"EXPEDIENTE\s+(?:N[º°]?\s*)?(\d+)\s+D[ÍI]GITO\s+(\d+)\s+A[ÑN]O\s+(\d{4})",
                    RegexOptions.IgnoreCase
                );


            if (matchMgp.Success)
            {
                return
                    $"{matchMgp.Groups[1].Value}/" +
                    $"{matchMgp.Groups[2].Value}/" +
                    $"{matchMgp.Groups[3].Value}";
            }


            return "";
        }


        private static DateTime? ExtraerFechaApertura(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return null;
            }


            // ==========================================
            // FORMATO MGP
            // 24 de Julio de 2026 – 10:00 horas
            // ==========================================

            var matchMgp =
                Regex.Match(
                    texto,
                    @"(\d{1,2})\s+de\s+([A-Za-zÁÉÍÓÚáéíóúÑñ]+)\s+de\s+(\d{4}).{0,20}?(\d{1,2}):(\d{2})",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline
                );


            if (matchMgp.Success)
            {
                var dia =
                    int.Parse(
                        matchMgp.Groups[1].Value
                    );


                var mes =
                    ObtenerNumeroMes(
                        matchMgp.Groups[2].Value
                    );


                var anio =
                    int.Parse(
                        matchMgp.Groups[3].Value
                    );


                var hora =
                    int.Parse(
                        matchMgp.Groups[4].Value
                    );


                var minuto =
                    int.Parse(
                        matchMgp.Groups[5].Value
                    );


                if (mes > 0)
                {
                    return new DateTime(
                        anio,
                        mes,
                        dia,
                        hora,
                        minuto,
                        0
                    );
                }
            }


            // ==========================================
            // FORMATO NUMÉRICO / EMVIAL
            // ==========================================

            var matchNumerico =
                Regex.Match(
                    texto,
                    @"(\d{1,2})[/-](\d{1,2})[/-](\d{4}).{0,20}?(\d{1,2}):(\d{2})",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline
                );


            if (matchNumerico.Success)
            {
                return new DateTime(
                    int.Parse(
                        matchNumerico.Groups[3].Value
                    ),

                    int.Parse(
                        matchNumerico.Groups[2].Value
                    ),

                    int.Parse(
                        matchNumerico.Groups[1].Value
                    ),

                    int.Parse(
                        matchNumerico.Groups[4].Value
                    ),

                    int.Parse(
                        matchNumerico.Groups[5].Value
                    ),

                    0
                );
            }


            return null;
        }

        private static int ObtenerNumeroMes(string mes)
        {
            mes =
               mes
                   .Trim()
                   .ToLowerInvariant();

            return mes switch
            {
                "enero" => 1,
                "febrero" => 2,
                "marzo" => 3,
                "abril" => 4,
                "mayo" => 5,
                "junio" => 6,
                "julio" => 7,
                "agosto" => 8,
                "septiembre" => 9,
                "setiembre" => 9,
                "octubre" => 10,
                "noviembre" => 11,
                "diciembre" => 12,

                _ => 0
            };
        }

        private static string DetectarOrganismo(string nombre)
        {
            if (
                nombre.Contains(
                    "EMVIAL",
                    StringComparison
                        .OrdinalIgnoreCase
                )
            )
            {
                return "EMVIAL";
            }


            return "Municipalidad de General Pueyrredon";
        }

        private static int? ObtenerEventoId(string href)
        {
            int indice =
                href.IndexOf(
                    "Evnt=",
                    StringComparison
                        .OrdinalIgnoreCase
                );


            if (indice < 0)
            {
                return null;
            }


            string valor =
                href.Substring(
                    indice + 5
                );


            int fin =
                valor.IndexOf('&');


            if (fin >= 0)
            {
                valor =
                    valor.Substring(
                        0,
                        fin
                    );
            }


            if (
                int.TryParse(
                    valor,
                    out int id
                )
            )
            {
                return id;
            }


            return null;
        }

        private static DocumentoObraDto? BuscarDocumentoPrincipal(List<DocumentoObraDto> documentos)
        {
            return documentos.FirstOrDefault(
                       x =>
                           x.Nombre.Contains(
                               "CARATULA",
                               StringComparison.OrdinalIgnoreCase
                           )
                   )
                   ??
                   documentos.FirstOrDefault(
                       x =>
                           x.Nombre.Contains(
                               "PLIEGO DE BASES",
                               StringComparison.OrdinalIgnoreCase
                           )
                   )
                   ??
                   documentos.FirstOrDefault(
                       x =>
                           x.Nombre.Contains(
                               "PLIEGO",
                               StringComparison.OrdinalIgnoreCase
                           )
                   );
        }

        private static string CrearUrlAbsoluta(string href)
        {
            if (
                Uri.TryCreate(
                    href,
                    UriKind.Absolute,
                    out var absoluta
                )
            )
            {
                return absoluta.ToString();
            }


            if (
                href.Contains(
                    "GetLic",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                int? eventoId =
                    ObtenerEventoId(href);

                if (eventoId.HasValue)
                {
                    return
                        $"https://appsb.mardelplata.gob.ar/Consultas/Licitaciones/GetLic.asp?Evnt={eventoId.Value}";
                }
            }


            return new Uri(
                new Uri(
                    "https://licitaciones.mardelplata.gob.ar/"
                ),
                href
            ).ToString();
        }

        public async Task<string> ObtenerTextoEspecificaciones(int eventoId)
        {
            var detalle =
                await ObtenerDetalleObraBase(
                    eventoId
                );


            var especificaciones =
                detalle.Documentos
                    .FirstOrDefault(
                        d =>
                            d.Nombre.Contains(
                                "ESP TECNICAS",
                                StringComparison.OrdinalIgnoreCase
                            )
                            ||
                            d.Nombre.Contains(
                                "ESPECIFICACIONES",
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (especificaciones == null)
            {
                throw new Exception(
                    "No se encontraron las especificaciones técnicas."
                );
            }


            return await ObtenerTextoPdf(
                especificaciones.Url,
                eventoId
            );
        }

        private static string ExtraerUbicacionEspecificaciones(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return "";
            }


            var match =
                Regex.Match(
                    texto,
                    @"UBICACI[ÓO]N\s*:\s*(.+?)(?=\s*(?:ART[ÍI]CULO\s+(?:N[º°]?\s*)?\d+|PRESUPUESTO\s+OFICIAL|FECHA\s+Y\s+HORA|PLIEGO\s+SIN\s+CARGO|CONTRATACI[ÓO]N\s+DIRECTA|EXPEDIENTE\s+N|OBJETO\s*:))",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline
                );


            if (match.Success)
            {
                return LimpiarTexto(
                    match.Groups[1].Value
                );
            }


            // Fallback: primera línea después de UBICACIÓN
            var fallback =
                Regex.Match(
                    texto,
                    @"UBICACI[ÓO]N\s*:\s*([^\r\n]+)",
                    RegexOptions.IgnoreCase
                );


            if (fallback.Success)
            {
                return LimpiarTexto(
                    fallback.Groups[1].Value
                );
            }


            return "";
        }

        private static List<UbicacionObraDto> ExtraerUbicacionesPuntuales(string texto)
        {
            var ubicaciones =
       new List<UbicacionObraDto>();

            if (string.IsNullOrWhiteSpace(texto))
            {
                return ubicaciones;
            }


            var matchLocalizacion =
                Regex.Match(
                    texto,
                    @"LOCALIZACI[ÓO]N(.+?)(?=ESPECIFICACIONES\s+TECNICAS|GENERALIDADES)",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline
                );


            if (!matchLocalizacion.Success)
            {
                return ubicaciones;
            }


            var bloque =
                matchLocalizacion
                    .Groups[1]
                    .Value;


            var matches =
                Regex.Matches(
                    bloque,
                    @"-\s*(.+?)(?=\s*-\s*|$)",
                    RegexOptions.Singleline
                );


            foreach (Match match in matches)
            {
                var descripcion =
                    LimpiarTexto(
                        match.Groups[1].Value
                    );


                // ==========================================
                // CORTAR BASURA QUE VIENE DESPUÉS
                // ==========================================

                descripcion =
                    Regex.Replace(
                        descripcion,
                        @"\s*Expediente\s+N[º°]?.*$",
                        "",
                        RegexOptions.IgnoreCase
                    );


                descripcion =
                    descripcion.Trim();


                // ==========================================
                // DESCARTAR TEXTO QUE NO ES UBICACIÓN
                // ==========================================

                if (
                    string.IsNullOrWhiteSpace(
                        descripcion
                    )
                )
                {
                    continue;
                }


                if (
                    descripcion.Contains(
                        "CONTRATACION",
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    descripcion.Contains(
                        "CONTRATACIÓN",
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    descripcion.Contains(
                        "CUERPO",
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    descripcion.Contains(
                        "EXPEDIENTE",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }


                ubicaciones.Add(
                    new UbicacionObraDto
                    {
                        Descripcion =
                            descripcion
                    }
                );
            }


            return ubicaciones;
        }

        private static double? ExtraerSuperficieM2(string texto)
        {
            var match = Regex.Match(
                texto,
                @"total\s+de\s+([\d\.\,]+)\s+metros\s+cuadrados",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                return null;
            }

            string valor =
                match.Groups[1].Value
                    .Replace(".", "")
                    .Replace(",", ".");

            if (
                double.TryParse(
                    valor,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double resultado
                )
            )
            {
                return resultado;
            }

            return null;
        }

        private static int? ExtraerFrentesTrabajo(string texto)
        {
            var match = Regex.Match(
                texto,
                @"dividido\s+en\s+\(?(\d+)\)?(?:\s+\w+)?\s+frentes?\s+de\s+trabajo",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                return null;
            }

            return int.TryParse(
                match.Groups[1].Value,
                out int resultado
            )
                ? resultado
                : null;
        }

    }
}