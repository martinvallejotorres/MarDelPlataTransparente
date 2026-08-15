using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosMDP.API.DTOs;
using ReclamosMDP.API.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]

    [Route(
        "api/datos-publicos"
    )]

    [AllowAnonymous]
    [EnableRateLimiting("datos-externos")]
    public class DatosPublicosController: ControllerBase
    {
        private readonly
            ComisariasService
            _comisariasService;

        private readonly SeguridadService
            _seguridadService;

        private readonly ObrasImportService
            _obrasImportService;

        private readonly ObrasRepository _obrasRepository;
        private readonly CatalogoDatosService _catalogoDatosService;
        private readonly AdministracionPublicaService _administracionPublicaService;
        private readonly MovilidadPublicaService _movilidadPublicaService;
        private readonly MedioAmbienteService _medioAmbienteService;
        private readonly SaludServiciosSocialesService _saludServiciosSocialesService;
        private readonly IWebHostEnvironment _environment;

        public DatosPublicosController(
            ComisariasService comisariasService,
            SeguridadService seguridadService,
            ObrasImportService obrasImportService,
            ObrasRepository obrasRepository,
            CatalogoDatosService catalogoDatosService,
            AdministracionPublicaService administracionPublicaService,
            MovilidadPublicaService movilidadPublicaService,
            MedioAmbienteService medioAmbienteService,
            SaludServiciosSocialesService saludServiciosSocialesService,
            IWebHostEnvironment environment
        )
        {
            _comisariasService =
                comisariasService;

            _seguridadService =
                seguridadService;

            _obrasImportService =
                 obrasImportService;
            _obrasRepository = obrasRepository;
            _catalogoDatosService = catalogoDatosService;
            _administracionPublicaService = administracionPublicaService;
            _movilidadPublicaService = movilidadPublicaService;
            _medioAmbienteService = medioAmbienteService;
            _saludServiciosSocialesService = saludServiciosSocialesService;
            _environment = environment;
        }

        [HttpGet("seguridad")]
        public async Task<IActionResult>ObtenerSeguridad()
        {
            try
            {
                var datos =
                    await
                        _seguridadService
                            .ObtenerResumen();


                return Ok(datos);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR SEGURIDAD -> "
                    + ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudieron obtener los datos de seguridad."
                    }
                );
            }
        }

        [HttpGet("comisarias")]
        public async Task<IActionResult>ObtenerComisarias()
        {
            try
            {
                var comisarias =
                    await
                        _comisariasService
                            .ObtenerComisarias();


                return Ok(
                    comisarias
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR COMISARIAS -> "
                    + ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudieron obtener las comisarías."
                    }
                );
            }
        }

        [HttpGet("obras")]
        public async Task<IActionResult> ObtenerObras([FromQuery] int anio = 2026, [FromQuery] int mes = 6){
            try
            {
                if (
                    mes < 1 ||
                    mes > 12
                )
                {
                    return BadRequest(
                        new
                        {
                            error =
                                "El mes debe estar entre 1 y 12."
                        }
                    );
                }

                if (anio < 2000 || anio > DateTime.UtcNow.Year)
                {
                    return BadRequest(new { error = "El año indicado no es válido." });
                }


                var obras =
                    await
                        _obrasImportService
                            .ObtenerObras(
                                anio,
                                mes
                            );


                return Ok(
                    obras
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR OBRAS -> "
                    + ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudieron obtener las obras."
                    }
                );
            }
        }

        [HttpGet("obras/{eventoId:int}")]
        public async Task<IActionResult>ObtenerDetalleObra(int eventoId)
        {
            try
            {
                var obra =
                    await
                        _obrasImportService
                            .ObtenerDetalleObra(
                                eventoId
                            );


                return Ok(obra);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR DETALLE OBRA -> "
                    + ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudo obtener el detalle de la obra."
                    }
                );
            }
        }


        [HttpGet("obras/{eventoId:int}/caratula-texto")]
        public async Task<IActionResult>ObtenerTextoCaratula(int eventoId)
        {
            try
            {
                var texto =
                    await
                        _obrasImportService
                            .ObtenerTextoCaratula(
                                eventoId
                            );


                return Ok(
                    new
                    {
                        eventoId,
                        texto
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR CARATULA OBRA -> "
                    + ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudo leer la carátula de la obra."
                    }
                );
            }
        }


        [HttpGet("obras/periodo/detalle")]
        public async Task<IActionResult> ObtenerObrasPeriodoDetalle([FromQuery] int anioDesde, [FromQuery] int mesDesde, [FromQuery] int anioHasta, [FromQuery] int mesHasta)
        {
            try
            {
                if (
                    mesDesde < 1 ||
                    mesDesde > 12 ||
                    mesHasta < 1 ||
                    mesHasta > 12
                )
                {
                    return BadRequest(
                        new
                        {
                            error =
                                "Los meses deben estar entre 1 y 12."
                        }
                    );
                }

                if (anioDesde < 2000 || anioDesde > DateTime.UtcNow.Year ||
                    anioHasta < 2000 || anioHasta > DateTime.UtcNow.Year)
                {
                    return BadRequest(new { error = "El rango de años no es válido." });
                }


                var desde =
                    new DateTime(
                        anioDesde,
                        mesDesde,
                        1
                    );


                var hasta =
                    new DateTime(
                        anioHasta,
                        mesHasta,
                        1
                    );


                if (desde > hasta)
                {
                    return BadRequest(
                        new
                        {
                            error =
                                "La fecha inicial no puede ser posterior a la final."
                        }
                    );
                }

                if (((hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month) >= 12)
                {
                    return BadRequest(new { error = "El período máximo permitido es de 12 meses." });
                }


                var obrasBasicas =
                    await _obrasImportService
                        .ObtenerObrasPeriodo(
                            desde,
                            hasta
                        );


                var detalles =
                    new List<ObraDetalleDto>();

                var erroresDetalle =
                    new List<Exception>();


                foreach (
                    var obra in obrasBasicas
                )
                {
                    try
                    {
                        Console.WriteLine(
                            $"DETALLE OBRA -> {obra.EventoId}"
                        );


                        var detalle =
                            await _obrasImportService
                                .ObtenerDetalleObra(
                                    obra.EventoId
                                );


                        if (detalle != null)
                        {
                            detalles.Add(
                                detalle
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        erroresDetalle.Add(ex);

                        Console.WriteLine(
                            $"ERROR DETALLE {obra.EventoId} -> {ex}"
                        );
                    }
                }

                if (erroresDetalle.Count > 0)
                {
                    throw new HttpRequestException(
                        $"Falló el detalle de {erroresDetalle.Count} obra(s).",
                        erroresDetalle[0]);
                }


                return Ok(
                    new
                    {
                        desde =
                            desde.ToString(
                                "yyyy-MM"
                            ),

                        hasta =
                            hasta.ToString(
                                "yyyy-MM"
                            ),

                        total =
                            detalles.Count,

                        obras =
                            detalles
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR OBRAS PERIODO DETALLE -> " +
                    ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudieron obtener los detalles de las obras."
                    }
                );
            }
        }


        [HttpGet("obras/{eventoId:int}/especificaciones-texto")]
        public async Task<IActionResult>ObtenerTextoEspecificaciones(int eventoId)
        {
            try
            {
                var texto =
                    await
                        _obrasImportService
                            .ObtenerTextoEspecificaciones(
                                eventoId
                            );


                return Ok(
                    new
                    {
                        eventoId,
                        texto
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR ESPECIFICACIONES OBRA -> "
                    + ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudieron leer las especificaciones técnicas."
                    }
                );
            }
        }


        [HttpGet("obras/periodo")]
        public async Task<IActionResult> ObtenerObrasPeriodo( [FromQuery] int anioDesde, [FromQuery] int mesDesde,[FromQuery] int anioHasta,[FromQuery] int mesHasta)
        {
            try
            {
                if (
                    mesDesde < 1 ||
                    mesDesde > 12 ||
                    mesHasta < 1 ||
                    mesHasta > 12
                )
                {
                    return BadRequest(
                        new
                        {
                            error =
                                "Los meses deben estar entre 1 y 12."
                        }
                    );
                }

                if (anioDesde < 2000 || anioDesde > DateTime.UtcNow.Year ||
                    anioHasta < 2000 || anioHasta > DateTime.UtcNow.Year)
                {
                    return BadRequest(new { error = "El rango de años no es válido." });
                }


                var desde =
                    new DateTime(
                        anioDesde,
                        mesDesde,
                        1
                    );


                var hasta =
                    new DateTime(
                        anioHasta,
                        mesHasta,
                        1
                    );


                if (desde > hasta)
                {
                    return BadRequest(
                        new
                        {
                            error =
                                "La fecha inicial no puede ser posterior a la final."
                        }
                    );
                }

                if (((hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month) >= 12)
                {
                    return BadRequest(new { error = "El período máximo permitido es de 12 meses." });
                }


                var obras =
                    await _obrasImportService
                        .ObtenerObrasPeriodo(
                            desde,
                            hasta
                        );


                return Ok(
                    new
                    {
                        desde =
                            desde.ToString(
                                "yyyy-MM"
                            ),

                        hasta =
                            hasta.ToString(
                                "yyyy-MM"
                            ),

                        total =
                            obras.Count,

                        obras
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "ERROR PERIODO OBRAS -> " +
                    ex
                );


                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "No se pudieron obtener las obras del período."
                    }
                );
            }
        }

        [HttpGet("obras/anio/{anio:int}")]
        public async Task<IActionResult> ObtenerObrasPorAnio(
            int anio,
            [FromQuery] bool actualizar = false)
        {
            if (actualizar && !_environment.IsDevelopment() &&
                !(User.Identity?.IsAuthenticated == true && User.IsInRole("Administrador")))
            {
                return Forbid();
            }

            if (
                anio < 2000
                ||
                anio > DateTime.UtcNow.Year
            )
            {
                return BadRequest(
                    "El año indicado no es válido."
                );
            }


            var obras =
                await _obrasImportService
                    .ObtenerObrasPorAnio(
                        anio
                        , actualizar
                    );


            return Ok(
                new
                {
                    anio,
                    cantidad = obras.Count,
                    obras
                }
            );
        }

        [HttpGet("categorias")]
        public IActionResult ObtenerCategorias()
        {
            var categorias = _catalogoDatosService.ObtenerCategorias();
            return Ok(new
            {
                cantidad = categorias.Count,
                actualizado = DateTime.UtcNow.Date,
                categorias
            });
        }

        [HttpGet("administracion-publica")]
        public async Task<IActionResult> ObtenerAdministracionPublica(
            [FromQuery] int anio = 2026,
            [FromQuery] string planta = "todas",
            [FromQuery] string? cargo = null)
        {
            if (!_administracionPublicaService.ObtenerAnios().Contains(anio))
                return BadRequest(new { error = "No hay un corte disponible para ese año." });

            if (!new[] { "todas", "P", "T", "C" }.Contains(planta, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { error = "El tipo de planta indicado no es válido." });

            try
            {
                var resumen = await _administracionPublicaService.ObtenerResumen(anio, planta, cargo);
                return Ok(new
                {
                    aniosDisponibles = _administracionPublicaService.ObtenerAnios(),
                    resumen
                });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR ADMINISTRACION PUBLICA -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "La fuente municipal de Administración Pública no respondió." });
            }
        }

        [HttpGet("administracion-publica/delegaciones")]
        public async Task<IActionResult> ObtenerDelegacionesMunicipales()
        {
            try
            {
                var geoJson = await _administracionPublicaService.ObtenerDelegacionesGeoJson();
                return Content(geoJson, "application/geo+json", System.Text.Encoding.UTF8);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR DELEGACIONES -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "No se pudo obtener el mapa de delegaciones municipales." });
            }
        }

        [HttpGet("movilidad-transporte")]
        public async Task<IActionResult> ObtenerMovilidadTransporte([FromQuery] int anio = 2025)
        {
            if (!_movilidadPublicaService.ObtenerAnios().Contains(anio))
                return BadRequest(new { error = "No hay datos de movilidad disponibles para ese año." });

            try
            {
                var resumen = await _movilidadPublicaService.ObtenerResumen(anio);
                return Ok(new
                {
                    aniosDisponibles = _movilidadPublicaService.ObtenerAnios(),
                    resumen
                });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR MOVILIDAD -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "Las fuentes municipales de movilidad no respondieron." });
            }
        }

        [HttpGet("movilidad-transporte/recorridos")]
        public async Task<IActionResult> ObtenerRecorridosColectivos()
        {
            try
            {
                var geoJson = await _movilidadPublicaService.ObtenerRecorridosGeoJson();
                return Content(geoJson, "application/geo+json", System.Text.Encoding.UTF8);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR RECORRIDOS -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "No se pudieron obtener los recorridos oficiales." });
            }
        }

        [HttpGet("movilidad-transporte/paradas")]
        public async Task<IActionResult> ObtenerParadasColectivos()
        {
            try
            {
                var geoJson = await _movilidadPublicaService.ObtenerParadasGeoJson();
                return Content(geoJson, "application/geo+json", System.Text.Encoding.UTF8);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR PARADAS -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "No se pudieron obtener las paradas oficiales." });
            }
        }

        [HttpGet("medio-ambiente")]
        public async Task<IActionResult> ObtenerMedioAmbiente([FromQuery] int anio = 2024)
        {
            if (!_medioAmbienteService.ObtenerAnios().Contains(anio))
                return BadRequest(new { error = "No hay datos ambientales disponibles para ese año." });

            try
            {
                var resumen = await _medioAmbienteService.ObtenerResumen(anio);
                return Ok(new { aniosDisponibles = _medioAmbienteService.ObtenerAnios(), resumen });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR MEDIO AMBIENTE -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "Las fuentes municipales de Medio Ambiente no respondieron." });
            }
        }

        [HttpGet("medio-ambiente/arroyos")]
        public async Task<IActionResult> ObtenerArroyos()
        {
            try { return Content(await _medioAmbienteService.ObtenerArroyosGeoJson(), "application/geo+json", System.Text.Encoding.UTF8); }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR ARROYOS -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "No se pudo obtener la red oficial de arroyos." });
            }
        }

        [HttpGet("medio-ambiente/estaciones")]
        public async Task<IActionResult> ObtenerEstacionesAmbientales()
        {
            try { return Content(await _medioAmbienteService.ObtenerEstacionesGeoJson(), "application/geo+json", System.Text.Encoding.UTF8); }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR ESTACIONES AMBIENTALES -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "No se pudieron obtener las estaciones ambientales." });
            }
        }

        [HttpGet("medio-ambiente/puntos-agua")]
        public async Task<IActionResult> ObtenerPuntosMuestreoAgua()
        {
            try { return Content(await _medioAmbienteService.ObtenerPuntosAguaGeoJson(), "application/geo+json", System.Text.Encoding.UTF8); }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR PUNTOS DE AGUA -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "No se pudieron obtener los puntos de muestreo de agua." });
            }
        }

        [HttpGet("salud-servicios-sociales")]
        public async Task<IActionResult> ObtenerSaludServiciosSociales([FromQuery] int anio = 2025)
        {
            if (!_saludServiciosSocialesService.ObtenerAnios().Contains(anio))
                return BadRequest(new { error = "No hay datos sanitarios disponibles para ese año." });
            try
            {
                var resumen = await _saludServiciosSocialesService.ObtenerResumen(anio);
                return Ok(new { aniosDisponibles = _saludServiciosSocialesService.ObtenerAnios(), resumen });
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR SALUD -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "Las fuentes municipales de Salud no respondieron." });
            }
        }

        [HttpGet("salud-servicios-sociales/centros")]
        public async Task<IActionResult> ObtenerCentrosSalud()
        {
            try { return Content(await _saludServiciosSocialesService.ObtenerCentrosGeoJson(), "application/geo+json", System.Text.Encoding.UTF8); }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR CENTROS DE SALUD -> {ex}");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "No se pudieron obtener los centros de salud oficiales." });
            }
        }

        [HttpGet("obras/comparacion-tramos")]
        public async Task<IActionResult> CompararTramos(
            [FromQuery] int anioDesde = 2024,
            [FromQuery] int? anioHasta = null)
        {
            var hasta = anioHasta ?? DateTime.UtcNow.Year;
            if (anioDesde < 2000 || hasta > DateTime.UtcNow.Year || anioDesde > hasta)
                return BadRequest(new { error = "El rango de años no es válido." });

            var comparaciones = await _obrasRepository.CompararTramos(anioDesde, hasta);
            return Ok(new { anioDesde, anioHasta = hasta, cantidad = comparaciones.Count, comparaciones });
        }

        [HttpGet("obras/tramos")]
        public async Task<IActionResult> ObtenerTramosObras(
            [FromQuery] int anioDesde = 2024,
            [FromQuery] int? anioHasta = null)
        {
            var hasta = anioHasta ?? DateTime.UtcNow.Year;
            if (anioDesde < 2000 || hasta > DateTime.UtcNow.Year || anioDesde > hasta)
                return BadRequest(new { error = "El rango de años no es válido." });

            if (hasta - anioDesde > 10)
                return BadRequest(new { error = "El período máximo permitido es de 10 años." });

            var obras = await _obrasRepository.ObtenerConTramosCompletos(anioDesde, hasta);
            return Ok(new
            {
                anioDesde,
                anioHasta = hasta,
                cantidadObras = obras.Count,
                cantidadTramos = obras.Sum(o => o.Tramos.Count),
                obras
            });
        }
    }


}
