using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosMDP.API.DTOs;
using ReclamosMDP.API.Services;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]

    [Route(
        "api/datos-publicos"
    )]

    [AllowAnonymous]
    public class DatosPublicosController: ControllerBase
    {
        private readonly
            ComisariasService
            _comisariasService;

        private readonly SeguridadService
            _seguridadService;

        private readonly ObrasImportService
            _obrasImportService;

        public DatosPublicosController(
            ComisariasService comisariasService,
            SeguridadService seguridadService,
            ObrasImportService obrasImportService
        )
        {
            _comisariasService =
                comisariasService;

            _seguridadService =
                seguridadService;

            _obrasImportService =
                 obrasImportService;
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
                    500,
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
                    500,
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
                    500,
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
                    500,
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
                    500,
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


                var obrasBasicas =
                    await _obrasImportService
                        .ObtenerObrasPeriodo(
                            desde,
                            hasta
                        );


                var detalles =
                    new List<ObraDetalleDto>();


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
                        Console.WriteLine(
                            $"ERROR DETALLE {obra.EventoId} -> {ex}"
                        );
                    }
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
                    500,
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
                    500,
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
                    500,
                    new
                    {
                        error =
                            "No se pudieron obtener las obras del período."
                    }
                );
            }
        }

    }


}