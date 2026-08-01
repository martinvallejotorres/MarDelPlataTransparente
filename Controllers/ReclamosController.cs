using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using ReclamosMDP.API.Models;
using ReclamosMDP.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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


        // GET api/reclamos/admin
        [Authorize(Roles = "Administrador")]
        [HttpGet("admin")]
        public IActionResult SoloAdmin()
        {
            return Ok(new
            {
                mensaje = "Accediste como administrador"
            });
        }


        // GET api/reclamos/privado

        [Authorize]
        [HttpGet("privado")]
        public IActionResult Privado()
        {
            return Ok(new
            {
                mensaje = "Entraste con JWT correctamente"
            });
        }


        // GET api/reclamos
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var reclamos = await _context.Reclamos
           .Include(r => r.Usuario)
           .Include(r => r.ApoyosUsuarios)
           .Select(r => new
           {
               r.Id,
               r.Titulo,
               r.Descripcion,
               r.Tipo,
               r.Zona,
               r.Latitud,
               r.Longitud,
               r.Fecha,
               Apoyos = r.ApoyosUsuarios.Count(),

               Usuario = r.Usuario == null ? null : new
               {
                   r.Usuario.Nombre,
                   r.Usuario.Email
               }
           })
           .ToListAsync();

            return Ok(reclamos);
        }

        
        // GET Panel Flotante Trending
        [HttpGet("estadisticas")]
        public async Task<IActionResult> ObtenerEstadisticas()
        {
            var reclamos = await _context.Reclamos
                .Include(r => r.ApoyosUsuarios)
                .ToListAsync();


            var activos = reclamos
                .Count(r => r.Estado != "Solucionado");


            var votosTotales = await _context.Apoyos.CountAsync();



            var urgentes = reclamos
                .OrderByDescending(r => r.ApoyosUsuarios.Count())
                .Take(5)
                .Select(r => new
                {
                    r.Id,
                    r.Titulo,
                    r.Tipo,
                    r.Zona,
                    Apoyos = r.ApoyosUsuarios.Count(),

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


        // GET api/reclamos/{id}/historial

        [HttpGet("{id}/historial")]
        public async Task<IActionResult> ObtenerHistorial(int id)
        {
            var reclamo = await _context.Reclamos
                .Include(r => r.HistorialEstados)
                    .ThenInclude(h => h.Usuario)
                .FirstOrDefaultAsync(r => r.Id == id);


            if (reclamo == null)
            {
                return NotFound(new
                {
                    mensaje = "Reclamo no encontrado"
                });
            }


            var historial = reclamo.HistorialEstados
                .OrderByDescending(h => h.Fecha)
                .Select(h => new
                {
                    h.EstadoAnterior,
                    h.EstadoNuevo,
                    h.Fecha,

                    usuario = h.Usuario == null
                        ? "Sistema"
                        : h.Usuario.Nombre
                });


            return Ok(new
            {
                reclamoId = reclamo.Id,
                reclamo.Titulo,
                estadoActual = reclamo.Estado,
                historial
            });
        }








        //*****************************POST METHOD *****************//

        // POST api/reclamos

        [Authorize(Roles = "Usuario,Administrador")]
        [HttpPost]
        public async Task<IActionResult> Crear(Reclamo reclamo)
        {
            var usuarioId = User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

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
                reclamo.UsuarioId = usuarioId;
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

        [Authorize(Roles = "Usuario,Administrador")]
        [HttpPost("{id}/apoyar")]
        public async Task<IActionResult> ApoyarReclamo(int id)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (usuarioId == null)
            {
                return Unauthorized();
            }

            var reclamo = await _context.Reclamos.FindAsync(id);

            if (reclamo == null)
            {
                return NotFound();
            }

            var yaApoyo = await _context.Apoyos.AnyAsync(a =>
                a.ReclamoId == id &&
                a.UsuarioId == usuarioId);

            if (yaApoyo)
            {
                return Conflict(new
                {
                    mensaje = "Ya apoyaste este reclamo."
                });
            }

            var apoyo = new Apoyo
            {
                ReclamoId = id,
                UsuarioId = usuarioId
            };

            _context.Apoyos.Add(apoyo);

            await _context.SaveChangesAsync();

            var cantidadApoyos = await _context.Apoyos
                .CountAsync(a => a.ReclamoId == id);

            return Ok(new
            {
                apoyos = cantidadApoyos
            });
        }











    }
}