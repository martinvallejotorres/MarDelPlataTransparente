using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using ReclamosMDP.API.Models;
using ReclamosMDP.API.Services;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReclamosController : ControllerBase
    {
        private readonly GeocodingService _geocoding;
        private readonly ReclamosDbContext _context;
        private readonly ZonaService _zonaService;

        public ReclamosController(
                GeocodingService geocoding,
                ZonaService zonaService,
                ReclamosDbContext context)
        {
            _geocoding = geocoding;
            _zonaService = zonaService;
            _context = context;
        }



        // GET api/reclamos
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var reclamos = await _context.Reclamos.ToListAsync();

            return Ok(reclamos);
        }

        // GET Panel Flotante Trending
        [HttpGet("estadisticas")]
        public async Task<IActionResult> ObtenerEstadisticas()
        {
            var reclamos = await _context.Reclamos.ToListAsync();


            var activos = reclamos
                .Count(r => r.Estado != "Solucionado");


            var votosTotales = reclamos
                .Sum(r => r.Apoyos);



            var urgentes = reclamos
                .OrderByDescending(r => r.Apoyos)
                .Take(5)
                .Select(r => new
                {
                    r.Id,
                    r.Titulo,
                    r.Tipo,
                    r.Zona,
                    r.Apoyos,
                    r.Estado
                })
                .ToList();



            return Ok(new
            {

                reclamosActivos = activos,

                votosTotales = votosTotales,

                masUrgentes = urgentes

            });
        }




        // POST api/reclamos
        [HttpPost]
        public async Task<IActionResult> Crear(Reclamo reclamo)
        {

            // Obtener coordenadas usando la dirección
            var coordenadas = await _geocoding.ObtenerCoordenadas(reclamo.Direccion);

            reclamo.Latitud = coordenadas.Value.lat;
            reclamo.Longitud = coordenadas.Value.lon;


            if (coordenadas == null)
            {
                return BadRequest(
                    "No se encontró la dirección"
                );
            }



            // Completar datos del reclamo

            reclamo.Latitud = coordenadas.Value.lat;
            reclamo.Longitud = coordenadas.Value.lon;

            reclamo.Zona = _zonaService.ObtenerZona(
                reclamo.Latitud,
                reclamo.Longitud
            );

            reclamo.Fecha = DateTime.UtcNow;

            // Guardar en PostgreSQL
            try
            {
                _context.Reclamos.Add(reclamo);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.InnerException?.Message ?? ex.Message);
            }



            return Ok(reclamo);
        }

        //POST /api/reclamos/${reclamoActual.id}/apoyar
        [HttpPost("{id}/apoyar")]
        public async Task<IActionResult> ApoyarReclamo(int id)
        {
            var reclamo = await _context.Reclamos.FindAsync(id);

            if (reclamo == null)
            {
                return NotFound();
            }

            reclamo.Apoyos++;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                apoyos = reclamo.Apoyos
            });
        }











    }
}