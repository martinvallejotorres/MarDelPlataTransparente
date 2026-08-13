using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using ReclamosMDP.API.DTOs;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using UglyToad.PdfPig;
using Microsoft.Extensions.Caching.Memory;
using System.Text;


namespace ReclamosMDP.API.Services
{
    public class ObrasImportService
    {
        private const string BaseDocumentosMunicipales =
        "https://appsb.mardelplata.gob.ar";

        private readonly HttpClient _httpClient;

        private readonly GeocodingService _geocodingService;

        private readonly IMemoryCache _cache;

        private readonly ObrasRepository _repository;

        public ObrasImportService(HttpClient httpClient, GeocodingService geocodingService, IMemoryCache cache, ObrasRepository repository)
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
            _repository = repository;
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

        public async Task<List<ObraImportDto>> ObtenerObrasPeriodo(DateTime desde, DateTime hasta)
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

            var errores =
                new List<Exception>();


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
                    errores.Add(ex);

                    Console.WriteLine(
                        $"ERROR OBRAS {fechaActual.Month}/{fechaActual.Year} -> {ex.Message}"
                    );
                }


                fechaActual =
                    fechaActual.AddMonths(1);
            }

            if (errores.Count > 0)
            {
                throw new HttpRequestException(
                    $"La fuente municipal falló en {errores.Count} mes(es) del período solicitado.",
                    errores[0]);
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
                            mes.ToString(CultureInfo.InvariantCulture),

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


        public async Task<ObraDetalleDto> ObtenerDetalleObra(
            int eventoId,
            int? anioFuente = null,
            int? mesFuente = null,
            bool forzarActualizacion = false)
        {
            // ==========================================
            // CACHE
            // ==========================================

            var cacheKey =
                $"obra-detalle-{eventoId}";


            if (!forzarActualizacion &&
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

            if (!forzarActualizacion)
            {
                var persistida = await _repository.Obtener(eventoId);
                if (persistida != null)
                {
                    _cache.Set(cacheKey, persistida, TimeSpan.FromHours(12));
                    return persistida;
                }
            }


            // ==========================================
            // DETALLE BASE
            // ==========================================

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

            detalle.EventosRelacionados = new List<int> { eventoId };


            // ==========================================
            // TEXTOS PARA ANALIZAR
            // ==========================================

            string textoPrincipal = "";

            string textoParaTramos = "";


            // ==========================================
            // DOCUMENTO PRINCIPAL
            // ==========================================

            var documentoPrincipal =
                BuscarDocumentoPrincipal(
                    detalle.Documentos
                );


            if (documentoPrincipal != null)
            {
                textoPrincipal =
                    await ObtenerTextoPdf(
                        documentoPrincipal.Url,
                        eventoId
                    );


                // Guardamos también este texto
                // para buscar tramos más adelante.

                textoParaTramos +=
                    textoPrincipal;


                var nombrePdf =
                    ExtraerNombreObra(
                        textoPrincipal
                    );


                if (
                    !string.IsNullOrWhiteSpace(
                        nombrePdf
                    )
                )
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
                            ||
                            d.Nombre.Contains(
                                "ACTA APERTURA",
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


                if (
                    detalle.FechaApertura == null
                )
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


                // IMPORTANTE:
                // sumamos también las especificaciones
                // para encontrar tramos como:
                //
                // Brown entre X e Y
                // Gandhi entre X e Y
                // etc.

                textoParaTramos +=
                    "\n" +
                    textoEspecificaciones;


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
            // EXTRAER TRAMOS
            // ==========================================

            detalle.Tramos =
                ExtraerTramos(
                    textoParaTramos
                );

            if (detalle.Tramos.Count > 0)
            {
                await GeocodificarTramos(
                    detalle.Tramos
                );
            }


            // Si encontramos tramos,
            // la geometría ya no es simplemente "Zona".

            if (
                detalle.Tramos.Count == 1
            )
            {
                detalle.TipoGeometria =
                    "Tramo";
            }
            else if (
                detalle.Tramos.Count > 1
            )
            {
                detalle.TipoGeometria =
                    "MultiTramo";
            }


            // ==========================================
            // FALLBACK DE UBICACIÓN PARA PLIEGOS MGP
            // ==========================================

            if (
                string.IsNullOrWhiteSpace(
                    detalle.UbicacionTexto
                )
                &&
                !string.IsNullOrWhiteSpace(
                    textoPrincipal
                )
            )
            {
                detalle.UbicacionTexto =
                    ExtraerUbicacionEspecificaciones(
                        textoPrincipal
                    );


                if (
                    !string.IsNullOrWhiteSpace(
                        detalle.UbicacionTexto
                    )
                    &&
                    detalle.Tramos.Count == 0
                )
                {
                    detalle.TipoGeometria =
                        DetectarTipoGeometria(
                            detalle.UbicacionTexto
                        );
                }
            }


            detalle.Nombre = LimpiarNombreVisible(detalle.Nombre);

            if (string.IsNullOrWhiteSpace(detalle.Licitacion))
                detalle.Licitacion = ExtraerLicitacion(detalle.Nombre);

            var anioPersistencia = anioFuente ?? detalle.FechaApertura?.Year;
            if (anioPersistencia.HasValue &&
                PareceObra(detalle.Nombre) &&
                (!detalle.FechaApertura.HasValue || detalle.FechaApertura.Value.Year == anioPersistencia.Value))
            {
                detalle.AnioFuente = anioPersistencia.Value;
                await _repository.Guardar(detalle, anioPersistencia.Value, mesFuente);
            }

            // ==========================================
            // GUARDAR CACHE
            // ==========================================

            _cache.Set(
                cacheKey,
                detalle,
                TimeSpan.FromHours(12)
            );


            return detalle;
        }

        private async Task GeocodificarTramos(
     List<TramoObraDto> tramos
 )
        {
            foreach (var tramo in tramos)
            {
                var inicio =
                    await BuscarInterseccion(
                        tramo.Calle,
                        tramo.Desde
                    );


                if (inicio.HasValue)
                {
                    tramo.LatitudInicio =
                        inicio.Value.lat;

                    tramo.LongitudInicio =
                        inicio.Value.lon;
                }


                var fin =
                    await BuscarInterseccion(
                        tramo.Calle,
                        tramo.Hasta
                    );


                if (fin.HasValue)
                {
                    tramo.LatitudFin =
                        fin.Value.lat;

                    tramo.LongitudFin =
                        fin.Value.lon;
                }
            }
        }

        private static string NormalizarNombreCalle( string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return "";
            }


            var resultado =
                nombre
                    .Trim()
                    .TrimEnd('.');


            resultado =
                Regex.Replace(
                    resultado,
                    @"^(calle|avenida|av\.?)\s+",
                    "",
                    RegexOptions.IgnoreCase
                );


            resultado =
                Regex.Replace(
                    resultado,
                    @"\s+",
                    " "
                );


            return resultado.Trim();
        }

        private async Task<(double lat, double lon)?> BuscarInterseccion(string calle, string esquina)
        {
            calle =
                NormalizarNombreCalle(
                    calle
                );

            esquina =
                NormalizarNombreCalle(
                    esquina
                );

            var intentos =
                new[]
                {
                    $"{calle} y {esquina}",
                    $"{calle} esquina {esquina}",
                    $"{calle} & {esquina}"
                };

            foreach (var intento in intentos)
            {
                var resultado =
                    await _geocodingService
                        .ObtenerCoordenadas(
                            intento
                        );


                if (
                    resultado.HasValue
                    &&
                    CoordenadaEsDeMarDelPlata(
                        resultado.Value.lat,
                        resultado.Value.lon
                    )
                )
                {
                    return resultado;
                }


                await Task.Delay(1100);
            }

            var calleOsm = PrepararNombreOsm(calle);
            var esquinaOsm = PrepararNombreOsm(esquina);
            var interseccionOsm = await _geocodingService
                .ObtenerInterseccionOsm(calleOsm, esquinaOsm);
            if (interseccionOsm.HasValue &&
                CoordenadaEsDeMarDelPlata(interseccionOsm.Value.lat, interseccionOsm.Value.lon))
                return interseccionOsm;


            return null;
        }

        private static string PrepararNombreOsm(string nombre)
        {
            var normalizado = NormalizarParaBusqueda(nombre);
            if (normalizado is "h. irigoyen" or "h irigoyen" or "hipolito irigoyen")
                return "Hipólito (Yrigoyen|Irigoyen)|H\\.? Irigoyen";
            if (normalizado == "brown") return "Brown";
            if (normalizado == "roca") return "Roca";
            if (normalizado == "beltran") return "Beltrán|Beltran";
            if (normalizado.Contains("arroyo la tapera")) return "La Tapera";
            return Regex.Escape(nombre.Trim());
        }

        public async Task<List<ObraDetalleDto>> ObtenerObrasPorAnio(int anio, bool forzarActualizacion = false)
        {
            var cacheKey =
                $"obras-detalle-anio-{anio}";


            if (!forzarActualizacion && await _repository.EstaSincronizado(anio))
            {
                var persistidas = (await _repository.ObtenerPorAnio(anio))
                    .Where(o => PareceObra(o.Nombre))
                    .ToList();
                var limpias = DeduplicarLlamados(persistidas);
                await _repository.EliminarFueraDeSincronizacion(
                    anio,
                    limpias.Select(o => o.EventoId).ToArray());
                return limpias;
            }

            if (!forzarActualizacion &&
                _cache.TryGetValue(
                    cacheKey,
                    out List<ObraDetalleDto>? cache
                )
                &&
                cache != null
            )
            {
                return cache;
            }


            var desde =
                new DateTime(
                    anio,
                    1,
                    1
                );


            var hasta =
                new DateTime(
                    anio,
                    12,
                    1
                );


            var obras =
                await ObtenerObrasPeriodo(
                    desde,
                    hasta
                );


            var resultado =
                new List<ObraDetalleDto>();

            var errores = new List<Exception>();


            foreach (var obra in obras)
            {
                try
                {
                    var detalle =
                        await ObtenerDetalleObra(
                            obra.EventoId,
                            anio,
                            int.TryParse(obra.Fecha, out var mes) ? mes : null,
                            forzarActualizacion
                        );

                    if (!PareceObra(detalle.Nombre) ||
                        (detalle.FechaApertura.HasValue && detalle.FechaApertura.Value.Year != anio))
                    {
                        continue;
                    }


                    resultado.Add(
                        detalle
                    );
                }
                catch (Exception ex)
                {
                    errores.Add(ex);
                    Console.WriteLine(
                        $"ERROR DETALLE OBRA {obra.EventoId}: {ex.Message}"
                    );
                }
            }

            if (errores.Count > 0)
                Console.WriteLine($"SINCRONIZACION OBRAS {anio}: se omitieron {errores.Count} detalles inaccesibles.");

            resultado = DeduplicarLlamados(resultado);

            await _repository.EliminarFueraDeSincronizacion(
                anio,
                resultado.Select(o => o.EventoId).ToArray());

            foreach (var obra in resultado)
            {
                await _repository.Guardar(obra, anio);
            }

            await _repository.MarcarSincronizado(anio, resultado.Count);


            _cache.Set(
                cacheKey,
                resultado,
                TimeSpan.FromHours(12)
            );


            return resultado;
        }

        private static string DetectarEstado( List<DocumentoObraDto> documentos)
        {
            var nombres = string.Join(" ", documentos.Select(d => d.Nombre));

            if (Regex.IsMatch(nombres,
                    @"(recepci[oó]n\s+definitiva|finalizaci[oó]n|final\s+de\s+obra|acta\s+de\s+recepci[oó]n)",
                    RegexOptions.IgnoreCase))
                return "Finalizada";

            if (Regex.IsMatch(nombres,
                    @"(acta\s+de\s+inicio|inicio\s+de\s+obra|contrato\s+de\s+obra)",
                    RegexOptions.IgnoreCase))
                return "En ejecución";

            if (Regex.IsMatch(nombres,
                    @"(adjudicaci[oó]n|decreto\s+de\s+adjudicaci[oó]n)",
                    RegexOptions.IgnoreCase))
                return "Adjudicada";

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

        private static string ObtenerClaveTramo(TramoObraDto tramo)
        {
            var calle =
                tramo.Calle
                    .Trim()
                    .ToLowerInvariant();


            var desde =
                tramo.Desde
                    .Trim()
                    .ToLowerInvariant();


            var hasta =
                tramo.Hasta
                    .Trim()
                    .ToLowerInvariant();


            var extremos =
                new[]
                {
            desde,
            hasta
                }
                .OrderBy(
                    x => x
                )
                .ToArray();


            return
                $"{calle}|{extremos[0]}|{extremos[1]}";
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
            var match = Regex.Match(texto,
                @"(?:LICITACI[ÓO]N\s+(?:P[ÚU]BLICA|PRIVADA)|CONCURSO\s+DE\s+PRECIOS|CONTRATACI[ÓO]N\s+DIRECTA).*?[“""]\s*(.+?)\s*[”""]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success)
            {
                match = Regex.Match(texto,
                    @"(?:OBJETO|DENOMINACI[ÓO]N\s+DE\s+LA\s+OBRA|OBRA)\s*:\s*[“""]?\s*(.+?)\s*(?=[”""]|EXPEDIENTE|PRESUPUESTO\s+OFICIAL|PLAZO|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            if (!match.Success)
            {
                return "";
            }

            return LimpiarTexto(
                match.Groups[1].Value
            );
        }

        private static string LimpiarNombreVisible(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Obra pública";

            var limpio = HtmlEntity.DeEntitize(nombre).Trim();
            var reemplazos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Licitaci�n"] = "Licitación",
                ["P�blica"] = "Pública",
                ["Contrataci�n"] = "Contratación",
                ["Construcci�n"] = "Construcción",
                ["Pavimentaci�n"] = "Pavimentación",
                ["Adquisici�n"] = "Adquisición"
            };

            foreach (var reemplazo in reemplazos)
                limpio = limpio.Replace(reemplazo.Key, reemplazo.Value, StringComparison.OrdinalIgnoreCase);

            if (limpio.StartsWith("Detalles Licitación", StringComparison.OrdinalIgnoreCase))
            {
                var objeto = Regex.Match(limpio,
                    @"(BACHEO|PAVIMENTACI[ÓO]N|REPAVIMENTACI[ÓO]N|CONSTRUCCI[ÓO]N|RECONSTRUCCI[ÓO]N|CICLOV[ÍI]A).+?(?=\s+D[ií]a\s+Hora|$)",
                    RegexOptions.IgnoreCase);
                if (objeto.Success) limpio = objeto.Value;
            }

            limpio = Regex.Replace(limpio, @"\s+", " ");
            return limpio.Trim(' ', '-', ':');
        }

        private static List<ObraDetalleDto> DeduplicarLlamados(IEnumerable<ObraDetalleDto> obras)
        {
            return obras
                .GroupBy(ClaveContratacion, StringComparer.OrdinalIgnoreCase)
                .Select(grupo =>
                {
                    var items = grupo.ToList();
                    var principal = items
                        .OrderByDescending(PuntajeDetalle)
                        .ThenByDescending(o => o.EventoId)
                        .First();

                    principal.EventosRelacionados = items
                        .SelectMany(o => o.EventosRelacionados.Count > 0
                            ? o.EventosRelacionados.AsEnumerable()
                            : new[] { o.EventoId }.AsEnumerable())
                        .Append(principal.EventoId)
                        .Distinct()
                        .OrderBy(id => id)
                        .ToList();

                    principal.Documentos = items.SelectMany(o => o.Documentos)
                        .GroupBy(d => d.Url, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First()).ToList();
                    principal.Tramos = items.SelectMany(o => o.Tramos)
                        .GroupBy(ObtenerClaveTramo, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.OrderByDescending(t =>
                            t.LatitudInicio.HasValue && t.LatitudFin.HasValue).First())
                        .ToList();
                    principal.Ubicaciones = items.SelectMany(o => o.Ubicaciones)
                        .GroupBy(u => u.Descripcion, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First()).ToList();

                    return principal;
                })
                .OrderByDescending(o => o.FechaApertura)
                .ThenByDescending(o => o.EventoId)
                .ToList();
        }

        private static string ClaveContratacion(ObraDetalleDto obra)
        {
            var organismo = NormalizarClave(obra.Organismo);
            var expediente = NormalizarClave(obra.Expediente);
            var licitacion = NormalizarClave(obra.Licitacion);
            if (licitacion.Length > 0) return $"licitacion|{organismo}|{licitacion}";
            if (expediente.Length > 0) return $"expediente|{organismo}|{expediente}";
            return $"evento|{obra.EventoId}";
        }

        private static int PuntajeDetalle(ObraDetalleDto obra) =>
            (obra.Nombre.Contains("Detalle", StringComparison.OrdinalIgnoreCase) ? 0 : 20) +
            obra.Documentos.Count + obra.Tramos.Count * 3 + obra.Ubicaciones.Count * 2 +
            (obra.PresupuestoOficial.HasValue ? 3 : 0) +
            (!string.IsNullOrWhiteSpace(obra.Expediente) ? 2 : 0);

        private static string NormalizarClave(string texto)
        {
            var normalizado = (texto ?? "").Normalize(NormalizationForm.FormD);
            var sinAcentos = new string(normalizado
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
            return Regex.Replace(sinAcentos.ToLowerInvariant(), @"[^a-z0-9]+", "").Trim();
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
            texto = NormalizarParaBusqueda(texto);
            var incluir = new[]
            {
                "bacheo", "pavimentacion", "repavimentacion", "fresado", "recapado",
                "construccion", "reconstruccion", "refaccion", "remodelacion",
                "puesta en valor", "ampliacion", "cordon cuneta", "vereda", "rampa",
                "ciclovia", "plaza", "parque industrial", "equipamiento comunitario",
                "desague", "infraestructura vial", "red vial", "obra publica", "obras publicas"
            };

            if (!incluir.Any(texto.Contains)) return false;

            var esCompra = Regex.IsMatch(texto,
                @"\b(adquisicion|adqusicion|compra|adq\.?|alquiler|provision)\b");
            var bienes = new[]
            {
                "material", "cemento", "arena", "piedra", "cano", "vehiculo",
                "maquinaria", "repuesto", "elemento de desgaste", "papel",
                "pintura", "luminaria", "mezcla asfaltica", "hormigon elaborado",
                "aceite", "lubricante"
            };
            var incluyeEjecucion = new[]
            {
                "instalacion", "ejecucion", "construccion", "reconstruccion",
                "reparacion", "refaccion", "pavimentacion", "bacheo", "fresado"
            }.Any(texto.Contains);

            if (texto.Contains("materiales de construccion") ||
                texto.Contains("articulos de construccion") ||
                (esCompra && (texto.Contains("mezcla asfaltica") ||
                              texto.Contains("hormigon elaborado"))))
                return false;

            return !(esCompra && bienes.Any(texto.Contains) && !incluyeEjecucion);
        }

        private static string NormalizarParaBusqueda(string texto)
        {
            var normalizado = (texto ?? "").Normalize(NormalizationForm.FormD);
            return new string(normalizado
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    .ToArray())
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant();
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

        private static List<TramoObraDto> ExtraerTramos(
     string texto
 )
        {
            var tramos =
                new List<TramoObraDto>();


            if (string.IsNullOrWhiteSpace(texto))
            {
                return tramos;
            }


            // ==========================================
            // PATRÓN 1
            // "Calle Gandhi entre Arroyo La Tapera
            //  y Calle Beltrán"
            //
            // "Funes entre Roca y San Lorenzo"
            //
            // "en el tramo de calle Funes entre..."
            // ==========================================

            var patronCalle =
                @"(?:en\s+el\s+tramo\s+de\s+)?" +
                @"(?:calle\s+)?" +
                @"(?<calle>[A-ZÁÉÍÓÚÑ][A-ZÁÉÍÓÚÑ0-9\.\s]{1,45}?)" +
                @"\s+entre\s+" +
                @"(?<desde>[A-ZÁÉÍÓÚÑ0-9\.\s]{1,40}?)" +
                @"\s+y\s+" +
                @"(?:calle\s+)?" +
                @"(?<hasta>[A-ZÁÉÍÓÚÑ0-9\.\s]{1,40}?)" +
                @"(?=;|\.|\n|$|•)";


            AgregarTramosDesdeRegex(
                texto,
                patronCalle,
                tramos
            );


            // ==========================================
            // PATRÓN 2
            //
            // "Eje Brown: entre H. Irigoyen
            //  y 20 de Septiembre"
            // ==========================================

            var patronEje =
                @"(?:eje)\s+" +
                @"(?<calle>[A-ZÁÉÍÓÚÑ0-9\.\s]{1,40}?)" +
                @"\s*:\s*" +
                @"entre\s+" +
                @"(?<desde>[A-ZÁÉÍÓÚÑ0-9\.\s]{1,40}?)" +
                @"\s+y\s+" +
                @"(?<hasta>[A-ZÁÉÍÓÚÑ0-9\.\s]{1,40}?)" +
                @"(?=;|\.|\n|$|•)";


            AgregarTramosDesdeRegex(
                texto,
                patronEje,
                tramos
            );


            // ==========================================
            // ELIMINAR DUPLICADOS
            // ==========================================

            return tramos
             .GroupBy(
                 ObtenerClaveTramo,
                 StringComparer.OrdinalIgnoreCase
             )
             .Select(
                 grupo =>
                     grupo.First()
             )
             .ToList();
        }

        private static void AgregarTramosDesdeRegex(
    string texto,
    string patron,
    List<TramoObraDto> tramos
)
        {
            var matches =
                Regex.Matches(
                    texto,
                    patron,
                    RegexOptions.IgnoreCase
                    |
                    RegexOptions.Multiline
                );


            foreach (Match match in matches)
            {
                var calle =
                    LimpiarTextoTramo(
                        match.Groups["calle"].Value
                    );


                var desde =
                    LimpiarTextoTramo(
                        match.Groups["desde"].Value
                    );


                var hasta =
                    LimpiarTextoTramo(
                        match.Groups["hasta"].Value
                    );


                if (
                    !TramoPareceValido(
                        calle,
                        desde,
                        hasta
                    )
                )
                {
                    continue;
                }


                tramos.Add(
                    new TramoObraDto
                    {
                        Calle =
                            calle,

                        Desde =
                            desde,

                        Hasta =
                            hasta,

                        Descripcion =
                            $"{calle} entre {desde} y {hasta}"
                    }
                );
            }
        }

        private static bool TramoPareceValido(
    string calle,
    string desde,
    string hasta
)
        {
            if (
                string.IsNullOrWhiteSpace(calle)
                ||
                string.IsNullOrWhiteSpace(desde)
                ||
                string.IsNullOrWhiteSpace(hasta)
            )
            {
                return false;
            }


            // Evitamos párrafos completos.
            if (
                calle.Length > 50
                ||
                desde.Length > 45
                ||
                hasta.Length > 45
            )
            {
                return false;
            }


            var texto =
                $"{calle} {desde} {hasta}"
                    .ToLowerInvariant();


            // Palabras que indican que NO estamos
            // hablando de calles/tramos urbanos.
            var palabrasInvalidas =
                new[]
                {
            "porcentaje",
            "pavimento de hormigón a construir",
            "material",
            "resistencia",
            "espesor",
            "pintura",
            "reflector",
            "tacha",
            "delineador",
            "mortero",
            "polietileno",
            "sustrato",
            "bicicletas y los autos",
            "línea visual",
            "linea visual",
            "rayo incidente",
            "índice de plasticidad",
            "indice de plasticidad",
            "base deberán",
            "base deberan",
            "dicha ciclovía",
            "dicha ciclovia",
            "bastón",
            "baston",
            "el cordón",
            "el cordon",
            "éste",
            "este y la calzada"
                };


            if (
                palabrasInvalidas.Any(
                    palabra =>
                        texto.Contains(
                            palabra
                        )
                )
            )
            {
                return false;
            }


            // Si los extremos son solamente números
            // tipo "entre 2 y 10", tampoco es un tramo.
            if (
                double.TryParse(
                    desde,
                    out _
                )
                &&
                double.TryParse(
                    hasta,
                    out _
                )
            )
            {
                return false;
            }


            return true;
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
                    @"LICITACI[ÓO]N\s+(P[ÚU]BLICA|PRIVADA)(?:\s+EMVIAL)?\s+(?:N[º°]?\s*)?(\d+)\s*[/\-]\s*(\d{2,4})",
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

                if (anio.Length == 2)
                {
                    anio = $"20{anio}";
                }


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

        private static string LimpiarTextoTramo(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return "";
            }


            var resultado =
                texto
                    .Trim()
                    .Trim(
                        '.',
                        ';',
                        ':',
                        '-',
                        '•'
                    );


            resultado =
                Regex.Replace(
                    resultado,
                    @"\s+",
                    " "
                );


            resultado =
                Regex.Replace(
                    resultado,
                    @"^(calle|av\.?|avenida)\s+",
                    "",
                    RegexOptions.IgnoreCase
                );

            resultado =
                Regex.Replace(
                    resultado,
                    @"^(calle\s+)",
                    "",
                    RegexOptions.IgnoreCase
                );


            resultado =
                Regex.Replace(
                    resultado,
                    @"^(eje\s+)",
                    "",
                    RegexOptions.IgnoreCase
                );


            // Si el texto contiene "calle",
            // nos quedamos con lo que viene después
            // de la última aparición de "calle".

            var indiceCalle =
                resultado.LastIndexOf(
                    "calle ",
                    StringComparison.OrdinalIgnoreCase
                );

            if (indiceCalle >= 0)
            {
                resultado =
                    resultado[
                        (indiceCalle + 6)..
                    ];
            }


            return resultado.Trim();
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
