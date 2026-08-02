using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using ReclamosMDP.API.DTOs;
using ReclamosMDP.API.Models;
using System.Security.Claims;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Administrador")]
    public class AdminController : ControllerBase
    {

        private readonly ReclamosDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;


        public AdminController(ReclamosDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Panel()
        {
            return Ok(new
            {
                mensaje = "Bienvenido al panel administrador"
            });
        }

        // GET api/admin/reclamos
        [HttpGet("reclamos")]
        public async Task<IActionResult> ObtenerReclamos()
        {

            var reclamos = await _context.Reclamos
                .Include(r => r.ApoyosUsuarios)
                .OrderByDescending(r => r.Fecha)
                .Select(r => new
                {
                    r.Id,
                    r.Titulo,
                    r.Tipo,
                    r.Direccion,
                    r.Zona,
                    r.Estado,
                    r.Fecha,
                    r.FotoUrl,

                    apoyos = r.ApoyosUsuarios.Count(),

                    r.AdministradorId
                })
                .ToListAsync();

            return Ok(reclamos);
        }


        // GET api/admin/reclamos/{id}

        [HttpGet("reclamos/{id}")]
        public async Task<IActionResult> ObtenerDetalleReclamo(int id)
        {
            var reclamo = await _context.Reclamos
                .Include(r => r.Usuario)
                .Include(r => r.Administrador)
                .Include(r => r.ApoyosUsuarios)
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


            return Ok(new
            {
                reclamo.Id,
                reclamo.Titulo,
                reclamo.Tipo,
                reclamo.Descripcion,
                reclamo.Direccion,
                reclamo.Zona,
                reclamo.Estado,
                reclamo.Fecha,

                apoyos = reclamo.ApoyosUsuarios.Count(),

                creadoPor = new
                {
                    reclamo.Usuario?.Id,
                    reclamo.Usuario?.Nombre,
                    reclamo.Usuario?.Email
                },


                asignadoA = reclamo.Administrador == null ? null : new
                {
                    reclamo.Administrador.Id,
                    reclamo.Administrador.Nombre,
                    reclamo.Administrador.Email
                },


                historial = reclamo.HistorialEstados
                    .OrderByDescending(h => h.Fecha)
                    .Select(h => new
                    {
                        h.EstadoAnterior,
                        h.EstadoNuevo,
                        h.Fecha,

                        usuario = h.Usuario.Email
                    })
            });
        }

        //**************************POST METHOD****************************//



        // PUT api/admin/reclamos/{id}/estado

        [HttpPut("reclamos/{id}/estado")]
        public async Task<IActionResult> CambiarEstado(int id,CambiarEstadoDto dto)
        {
            var reclamo = await _context.Reclamos
                .FirstOrDefaultAsync(r => r.Id == id);


            if (reclamo == null)
            {
                return NotFound(new
                {
                    mensaje = "Reclamo no encontrado"
                });
            }

            // VALIDAR ESTADO
            if (!ReclamoEstados.Todos.Contains(dto.Estado))
            {
                return BadRequest(new
                {
                    mensaje = "Estado no válido",
                    estadosPermitidos = ReclamoEstados.Todos
                });
            }


            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);


            if (usuarioId == null)
            {
                return Unauthorized();
            }

            if (reclamo.Estado == dto.Estado)
            {
                return Conflict(new
                {
                    mensaje = "El reclamo ya tiene ese estado."
                });
            }


            var historial = new HistorialEstado
            {
                ReclamoId = reclamo.Id,
                EstadoAnterior = reclamo.Estado,
                EstadoNuevo = dto.Estado,
                UsuarioId = usuarioId
            };


            _context.HistorialEstados.Add(historial);


            reclamo.Estado = dto.Estado;

            await _context.SaveChangesAsync();


            return Ok(new
            {
                mensaje = "Estado actualizado correctamente",
                reclamo = new
                {
                    reclamo.Id,
                    reclamo.Titulo,
                    reclamo.Estado
                }
            });
        }


        // POST api/admin/reclamos/{id}/asignar/{usuarioId}

        [HttpPost("reclamos/{id}/asignar/{usuarioId}")]
        public async Task<IActionResult> AsignarReclamo(
     int id,
     string usuarioId)
        {
            // Buscar reclamo
            var reclamo = await _context.Reclamos
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reclamo == null)
            {
                return NotFound(new
                {
                    mensaje = "Reclamo no encontrado."
                });
            }


            // Buscar usuario
            var usuario = await _userManager
                .FindByIdAsync(usuarioId);

            if (usuario == null)
            {
                return BadRequest(new
                {
                    mensaje = "Administrador inexistente."
                });
            }


            // Comprobar que realmente sea administrador
            var esAdmin = await _userManager.IsInRoleAsync(
                usuario,
                "Administrador"
            );

            if (!esAdmin)
            {
                return BadRequest(new
                {
                    mensaje = "El usuario no tiene rol de administrador."
                });
            }


            // Evitar asignarlo nuevamente al mismo administrador
            if (reclamo.AdministradorId == usuarioId)
            {
                return Conflict(new
                {
                    mensaje = "El reclamo ya está asignado a ese administrador."
                });
            }


            // Asignar administrador
            reclamo.AdministradorId = usuarioId;

            await _context.SaveChangesAsync();


            return Ok(new
            {
                mensaje = "Reclamo asignado correctamente.",

                reclamo = reclamo.Id,

                administrador = new
                {
                    usuario.Id,
                    usuario.Nombre,
                    usuario.Email
                }
            });
        }



    }
}