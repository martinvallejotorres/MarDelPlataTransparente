using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using ReclamosMDP.API.Models;
using ReclamosMDP.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ReclamosMDP.API.DTOs;
using Microsoft.AspNetCore.Hosting;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReclamosController : ControllerBase
    {
        private readonly GeocodingService _geocoding;
        private readonly ReclamosDbContext _context;
        private readonly ZonaService _zonaService;
        private readonly IWebHostEnvironment _environment;


        private static readonly HashSet<string> CategoriasValidas = new()
        {
            "Baches",
            "Basura",
            "Agua",
            "Alumbrado",
            "Espacios Verdes",
            "Tránsito",
            "Árboles caídos",
            "Otros"
        };

        private static readonly HashSet<string> ExtensionesImagenPermitidas =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

        private static readonly HashSet<string> TiposImagenPermitidos =
            new(StringComparer.OrdinalIgnoreCase)
            {
        "image/jpeg",
        "image/png",
        "image/webp"
            };



        public ReclamosController(
                GeocodingService geocoding,
                ZonaService zonaService,
                ReclamosDbContext context,
                IWebHostEnvironment environment)
        {
            _geocoding = geocoding;
            _zonaService = zonaService;
            _context = context;
            _environment = environment;

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
               r.Direccion,
               r.Zona,
               r.Latitud,
               r.Longitud,
               r.Fecha,
               r.Estado,
               r.FotoUrl,
               Apoyos = r.ApoyosUsuarios.Count(),

               Usuario = r.Usuario == null ? null : new
               {
                   r.Usuario.Nombre
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
                .Where(r => r.Estado != "Rechazado")
                .OrderByDescending(r => r.ApoyosUsuarios.Count())
                .Take(3)
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



            var enRevision = reclamos.Count(r => r.Estado == "En revisión");

            var solucionados = reclamos.Count(r => r.Estado == "Solucionado");

            return Ok(new
            {
                reclamosActivos = activos,

                votosTotales = votosTotales,

                enRevision = enRevision,

                solucionados = solucionados,

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


        // GET api/reclamos/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var reclamos = await _context.Reclamos
                .Include(r => r.ApoyosUsuarios)
                .ToListAsync();

            var categorias = reclamos
                .GroupBy(r =>
                    !string.IsNullOrWhiteSpace(r.Tipo) &&
                    CategoriasValidas.Contains(r.Tipo)
                        ? r.Tipo
                        : "Otros"
                )
                .Select(g => new
                {
                    categoria = g.Key,
                    cantidad = g.Count()
                })
                .OrderByDescending(x => x.cantidad)
                .ToList();

            var estados = reclamos
                .GroupBy(r =>
                    string.IsNullOrWhiteSpace(r.Estado)
                        ? "Recibido"
                        : r.Estado
                )
                .Select(g => new
                {
                    estado = g.Key,
                    cantidad = g.Count()
                })
                .ToList();

            var barrios = reclamos
                .Where(r => !string.IsNullOrWhiteSpace(r.Zona))
                .GroupBy(r => r.Zona)
                .Select(g => new
                {
                    barrio = g.Key,
                    cantidad = g.Count()
                })
                .OrderByDescending(x => x.cantidad)
                .Take(10)
                .ToList();

            var totalReclamos = reclamos.Count;

            var totalApoyos = reclamos.Sum(r => r.ApoyosUsuarios.Count);

            var totalBarrios = reclamos
                .Where(r => !string.IsNullOrWhiteSpace(r.Zona))
                .Select(r => r.Zona)
                .Distinct()
                .Count();

            var reclamosHoy = reclamos.Count(r =>
                r.Fecha.Date == DateTime.UtcNow.Date);


            var masApoyados = reclamos
            .Where(r => r.Estado != "Rechazado")
            .OrderByDescending(r => r.ApoyosUsuarios.Count)
            .Take(6)
            .Select(r => new
            {
                r.Id,
                r.Titulo,
                r.Tipo,
                r.Zona,
                r.Direccion,
                r.Latitud,
                r.Longitud,
                r.Estado,
                r.FotoUrl,

                apoyos = r.ApoyosUsuarios.Count
            })
            .ToList();


            var desde = DateTime.UtcNow.Date.AddDays(-29);
            var hasta = DateTime.UtcNow.Date;

            var reclamosPorDia = reclamos
                .Where(r => r.Fecha.Date >= desde)
                .GroupBy(r => r.Fecha.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );

            var evolucion30Dias = Enumerable
                .Range(0, 30)
                .Select(i =>
                {
                    var fecha = desde.AddDays(i);

                    return new
                    {
                        fecha = fecha.ToString("yyyy-MM-dd"),
                        cantidad = reclamosPorDia.GetValueOrDefault(fecha, 0)
                    };
                })
                .ToList();



            return Ok(new
            {
                totalReclamos,
                totalApoyos,
                totalBarrios,
                reclamosHoy,

                categorias,
                estados,
                barrios,
                masApoyados,
                evolucion30Dias
            });
        }





        //*****************************POST METHOD *****************//

        // POST api/reclamos

        [Authorize(Roles = "Usuario,Administrador")]
        [HttpPost]
        public async Task<IActionResult> Crear([FromForm] CrearReclamoDto dto)
        {
            var usuarioId = User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

            if (usuarioId == null)
            {
                return Unauthorized();
            }


            // ==========================
            // Validar categoría
            // ==========================

            if (!CategoriasValidas.Contains(dto.Tipo))
            {
                dto.Tipo = "Otros";
            }


            // ==========================
            // Validar imagen
            // ==========================

            if (dto.Foto != null && dto.Foto.Length > 0)
            {
                const long tamañoMaximo =
                    5 * 1024 * 1024; // 5 MB

                var extension = Path
                    .GetExtension(dto.Foto.FileName)
                    .ToLowerInvariant();


                if (dto.Foto.Length > tamañoMaximo)
                {
                    return BadRequest(new
                    {
                        mensaje = "La imagen no puede superar los 5 MB."
                    });
                }


                if (!ExtensionesImagenPermitidas.Contains(extension) ||
                    !TiposImagenPermitidos.Contains(dto.Foto.ContentType))
                {
                    return BadRequest(new
                    {
                        mensaje = "Formato de imagen no permitido. Usá JPG, PNG o WEBP."
                    });
                }
            }


            // ==========================
            // Geocodificación
            // ==========================

            var coordenadas =
                await _geocoding.ObtenerCoordenadas(dto.Direccion);

            if (coordenadas == null)
            {
                return BadRequest(new
                {
                    mensaje = "No se encontró la dirección."
                });
            }


            var latitud = coordenadas.Value.lat;
            var longitud = coordenadas.Value.lon;

            var zona = _zonaService.ObtenerZona(
                latitud,
                longitud
            );


            // ==========================
            // Guardar imagen
            // ==========================

            string? fotoUrl = null;

            if (dto.Foto != null && dto.Foto.Length > 0)
            {
                var carpeta = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "reclamos"
                );


                if (!Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }


                var extension = Path
                    .GetExtension(dto.Foto.FileName)
                    .ToLowerInvariant();


                var nombreArchivo =
                    $"{Guid.NewGuid()}{extension}";


                var rutaCompleta = Path.Combine(
                    carpeta,
                    nombreArchivo
                );


                await using (var stream = new FileStream(
                    rutaCompleta,
                    FileMode.Create))
                {
                    await dto.Foto.CopyToAsync(stream);
                }


                fotoUrl =
                    $"/uploads/reclamos/{nombreArchivo}";
            }


            // ==========================
            // Crear Reclamo
            // ==========================

            var reclamo = new Reclamo
            {
                Titulo = dto.Titulo,
                Tipo = dto.Tipo,
                Descripcion = dto.Descripcion,
                Direccion = dto.Direccion,

                UsuarioId = usuarioId,

                Latitud = latitud,
                Longitud = longitud,
                Zona = zona,

                Fecha = DateTime.UtcNow,
                Estado = "Recibido",

                FotoUrl = fotoUrl
            };


            // ==========================
            // Guardar PostgreSQL
            // ==========================

            _context.Reclamos.Add(reclamo);

            await _context.SaveChangesAsync();


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
                return NotFound(new
                {
                    mensaje = "Reclamo no encontrado."
                });
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