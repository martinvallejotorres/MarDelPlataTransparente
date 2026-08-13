using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using ReclamosMDP.API.Models;
using ReclamosMDP.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ReclamosMDP.API.DTOs;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;
using Microsoft.AspNetCore.RateLimiting;

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

        private static bool CoordenadaEsDelPartido(double latitud, double longitud) =>
            latitud is >= -38.20 and <= -37.70 &&
            longitud is >= -57.85 and <= -57.30;

        private static async Task<bool> EsContenidoImagenValido(IFormFile foto)
        {
            var cabecera = new byte[12];
            await using var stream = foto.OpenReadStream();
            var leidos = await stream.ReadAsync(cabecera.AsMemory(0, cabecera.Length));

            var jpeg = leidos >= 3 &&
                       cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF;
            var png = leidos >= 8 &&
                      cabecera.AsSpan(0, 8).SequenceEqual(
                          new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var webp = leidos >= 12 &&
                       System.Text.Encoding.ASCII.GetString(cabecera, 0, 4) == "RIFF" &&
                       System.Text.Encoding.ASCII.GetString(cabecera, 8, 4) == "WEBP";

            return Path.GetExtension(foto.FileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => jpeg,
                ".png" => png,
                ".webp" => webp,
                _ => false
            };
        }



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
        [AllowAnonymous]
        [HttpGet("fotos/{nombreArchivo}")]
        public IActionResult ObtenerFoto(string nombreArchivo)
        {
            var extension = Path.GetExtension(nombreArchivo);
            var nombreSinExtension = Path.GetFileNameWithoutExtension(nombreArchivo);

            if (!Guid.TryParseExact(nombreSinExtension, "D", out _) ||
                !ExtensionesImagenPermitidas.Contains(extension))
            {
                return NotFound();
            }

            var ruta = Path.Combine(
                _environment.ContentRootPath,
                "App_Data",
                "uploads",
                "reclamos",
                nombreArchivo);

            if (!System.IO.File.Exists(ruta))
            {
                return NotFound();
            }

            var tipoContenido = extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            return PhysicalFile(ruta, tipoContenido);
        }

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



        // GET api/reclamos/reverse-geocode?latitud=...&longitud=...
        [AllowAnonymous]
        [EnableRateLimiting("datos-externos")]
        [HttpGet("reverse-geocode")]
        public async Task<IActionResult> ReverseGeocode([FromQuery] string latitud, [FromQuery] string longitud)
        {
            if (
                !double.TryParse(
                    latitud,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var lat
                )
                ||
                !double.TryParse(
                    longitud,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var lon
                )
            )
            {
                return BadRequest(new
                {
                    mensaje = "Coordenadas inválidas."
                });
            }


            if (
                !CoordenadaEsDelPartido(lat, lon)
            )
            {
                return BadRequest(new
                {
                    mensaje = "Coordenadas inválidas."
                });
            }


            var direccion =
                await _geocoding.ObtenerDireccion(
                    lat,
                    lon
                );


            if (string.IsNullOrWhiteSpace(direccion))
            {
                return NotFound(new
                {
                    mensaje = "No se encontró una dirección para ese punto."
                });
            }


            return Ok(new
            {
                direccion
            });
        }


        //*****************************POST METHOD *****************//

        // POST api/reclamos

        [Authorize(Roles = "Usuario,Administrador")]
        [HttpPost]
        public async Task<IActionResult> Crear([FromForm] CrearReclamoDto dto)
        {
            dto.Titulo = dto.Titulo.Trim();
            dto.Descripcion = dto.Descripcion.Trim();
            dto.Tipo = dto.Tipo.Trim();
            dto.Direccion = dto.Direccion?.Trim();

            if (dto.Titulo.Length is < 5 or > 100 ||
                dto.Descripcion.Length is < 10 or > 1000)
            {
                return BadRequest(new
                {
                    mensaje = "El título o la descripción no tienen una longitud válida."
                });
            }

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
                    !TiposImagenPermitidos.Contains(dto.Foto.ContentType) ||
                    !await EsContenidoImagenValido(dto.Foto))
                {
                    return BadRequest(new
                    {
                        mensaje = "Formato de imagen no permitido. Usá JPG, PNG o WEBP."
                    });
                }
            }


            // ==========================
            // Obtener ubicación
            // ==========================

            double latitud;
            double longitud;

            string direccionFinal;


            var tieneLatitud =
                 !string.IsNullOrWhiteSpace(dto.Latitud);

            var tieneLongitud =
                !string.IsNullOrWhiteSpace(dto.Longitud);


            // Si vino solamente una coordenada,
            // rechazamos la petición.
            if (tieneLatitud != tieneLongitud)
            {
                return BadRequest(new
                {
                    mensaje = "La ubicación seleccionada no es válida."
                });
            }


            // ==========================
            // Opción 1:
            // ubicación seleccionada
            // directamente en el mapa
            // ==========================

            if (tieneLatitud && tieneLongitud)
            {
                if (
                    !double.TryParse(
                        dto.Latitud,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out latitud
                    )
                    ||
                    !double.TryParse(
                        dto.Longitud,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out longitud
                    )
                )
                {
                    return BadRequest(new
                    {
                        mensaje = "Las coordenadas seleccionadas no son válidas."
                    });
                }


                Console.WriteLine(
                    $"COORDENADAS RECIBIDAS -> Latitud: {latitud} | Longitud: {longitud}"
                );


                if (
                    !CoordenadaEsDelPartido(latitud, longitud)
                )
                {
                    return BadRequest(new
                    {
                        mensaje = "Las coordenadas seleccionadas no son válidas."
                    });
                }


                var direccionReverse =
                    await _geocoding.ObtenerDireccion(
                        latitud,
                        longitud
                    );


                direccionFinal =
                    !string.IsNullOrWhiteSpace(direccionReverse)
                        ? direccionReverse
                        : $"Ubicación seleccionada en mapa ({latitud:F6}, {longitud:F6})";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.Direccion))
                {
                    return BadRequest(new
                    {
                        mensaje = "Ingresá una dirección o seleccioná una ubicación en el mapa."
                    });
                }


                var coordenadas =
                    await _geocoding.ObtenerCoordenadas(
                        dto.Direccion
                    );


                if (coordenadas == null)
                {
                    return BadRequest(new
                    {
                        mensaje = "No se encontró la dirección."
                    });
                }


                latitud = coordenadas.Value.lat;
                longitud = coordenadas.Value.lon;

                if (!CoordenadaEsDelPartido(latitud, longitud))
                {
                    return BadRequest(new
                    {
                        mensaje = "La dirección debe estar dentro del Partido de General Pueyrredon."
                    });
                }

                direccionFinal =
                    dto.Direccion.Trim();
            }


            // ==========================
            // Obtener zona / barrio
            // ==========================

            var zona =
                _zonaService.ObtenerZona(
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
                    _environment.ContentRootPath,
                    "App_Data",
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
                    $"/api/reclamos/fotos/{nombreArchivo}";
            }


            // ==========================
            // Crear Reclamo
            // ==========================

            var reclamo = new Reclamo
            {
                Titulo = dto.Titulo,
                Tipo = dto.Tipo,
                Descripcion = dto.Descripcion,

                Direccion = direccionFinal,

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

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    mensaje = "Ya apoyaste este reclamo."
                });
            }

            var cantidadApoyos = await _context.Apoyos
                .CountAsync(a => a.ReclamoId == id);

            return Ok(new
            {
                apoyos = cantidadApoyos
            });
        }


    }
}
