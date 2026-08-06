using HtmlAgilityPack;
using ReclamosMDP.API.DTOs;
using System.Xml;
using UglyToad.PdfPig;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ReclamosMDP.API.Services
{
    public class ObrasImportService
    {
        private const string BaseDocumentosMunicipales =
        "https://appsb.mardelplata.gob.ar";

        private readonly HttpClient _httpClient;

        public ObrasImportService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            _httpClient
                .DefaultRequestHeaders
                .UserAgent
                .ParseAdd(
                    "MarDelPlataTransparente/1.0"
                );
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

        public async Task<ObraDetalleDto>ObtenerDetalleObra(int eventoId)
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


            if (caratula != null)
            {
                string textoCaratula =
                    await ObtenerTextoPdf(
                        caratula.Url,
                        eventoId
                    );


                detalle.PresupuestoOficial =
                    ExtraerPresupuestoOficial(
                        textoCaratula
                    );


                detalle.GarantiaOferta =
                    ExtraerGarantiaOferta(
                        textoCaratula
                    );


                detalle.Licitacion =
                    ExtraerLicitacion(
                        textoCaratula
                    );


                detalle.Expediente =
                    ExtraerExpediente(
                        textoCaratula
                    );


                detalle.FechaApertura =
                    ExtraerFechaApertura(
                        textoCaratula
                    );

                detalle.Nombre =
                    ExtraerNombreObra(
                        textoCaratula
                    );
            }

            var especificaciones =
                detalle.Documentos
             .FirstOrDefault(d =>
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
                string textoEspecificaciones =
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

            return detalle;
        }

        private static string ExtraerNombreObra(string texto)
        {
            var match = Regex.Match(
                texto,
                @"LICITACI[ÓO]N\s+P[ÚU]BLICA\s+N[º°]?\s*\d+\s*/\s*\d{4}\s+[“""](.+?)[”""]",
                RegexOptions.IgnoreCase
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
                "provision",
                "provisión",
                "vehiculo",
                "vehículo",
                "cemento",
                "arena",
                "piedra",
                "caños",
                "caños",
                "elementos de desgaste",
                "papel"
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
                "reparacion",
                "reparación",
                "ciclovia",
                "ciclovía",
                "cordon cuneta",
                "cordón cuneta",
                "veredas"
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
            var match = Regex.Match(
                texto,
                @"LICITACI[ÓO]N\s+P[ÚU]BLICA\s+N[º°]?\s*(\d+)\s*/\s*(\d{4})",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                return "";
            }

            return
                $"Licitación Pública {match.Groups[1].Value}/{match.Groups[2].Value}";
        }

        private static string ExtraerExpediente(string texto)
        {
            var match = Regex.Match(
                texto,
                @"EXPEDIENTE\s+N[º°]?\s*(\d+)\s*[/–\-]?\s*([A-Z])\s*[/–\-]?\s*(\d{4})",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                return "";
            }

            return
                $"{match.Groups[1].Value}/{match.Groups[2].Value}/{match.Groups[3].Value}";
        }

        private static DateTime? ExtraerFechaApertura(string texto)
        {
            var match = Regex.Match(
                texto,
                @"APERTURA\s+DE\s+PROPUESTAS:\s*(\d{1,2})\s+DE\s+([A-ZÁÉÍÓÚ]+)\s+DE\s+(\d{4})\s*[–\-]\s*(\d{1,2}):(\d{2})",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
            {
                return null;
            }

            int dia =
                int.Parse(match.Groups[1].Value);

            int anio =
                int.Parse(match.Groups[3].Value);

            int hora =
                int.Parse(match.Groups[4].Value);

            int minuto =
                int.Parse(match.Groups[5].Value);

            int mes =
                ObtenerNumeroMes(
                    match.Groups[2].Value
                );

            if (mes == 0)
            {
                return null;
            }

            return new DateTime(
                anio,
                mes,
                dia,
                hora,
                minuto,
                0,
                DateTimeKind.Unspecified
            );
        }

        private static int ObtenerNumeroMes(string mes)
        {
            mes =
                mes
                    .Trim()
                    .ToUpperInvariant()
                    .Replace("Á", "A")
                    .Replace("É", "E")
                    .Replace("Í", "I")
                    .Replace("Ó", "O")
                    .Replace("Ú", "U");

            return mes switch
            {
                "ENERO" => 1,
                "FEBRERO" => 2,
                "MARZO" => 3,
                "ABRIL" => 4,
                "MAYO" => 5,
                "JUNIO" => 6,
                "JULIO" => 7,
                "AGOSTO" => 8,
                "SEPTIEMBRE" => 9,
                "OCTUBRE" => 10,
                "NOVIEMBRE" => 11,
                "DICIEMBRE" => 12,

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
            var match = Regex.Match(
                texto,
                @"UBICACI[ÓO]N:\s*(.+?)(?:ART[ÍI]CULO|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (!match.Success)
            {
                return "";
            }

            return LimpiarTexto(
                match.Groups[1].Value
            ).Trim(' ', '-', '.');
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