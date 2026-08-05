using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReclamosMDP.API.Services;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]

    [Route(
        "api/datos-publicos"
    )]

    [AllowAnonymous]

    public class DatosPublicosController
        : ControllerBase
    {
        private readonly
            ComisariasService
            _comisariasService;

        private readonly SeguridadService
            _seguridadService;


        public DatosPublicosController(
            ComisariasService comisariasService,
            SeguridadService seguridadService
        )
        {
            _comisariasService =
                comisariasService;

            _seguridadService =
                seguridadService;
        }

        [HttpGet("seguridad")]

        public async Task<IActionResult>
    ObtenerSeguridad()
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

        public async Task<IActionResult>
            ObtenerComisarias()
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
    }
}