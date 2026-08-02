using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using ReclamosMDP.API.Models;
using System.Security.Claims;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ReclamosDbContext _context;

        public UsuariosController(
            UserManager<ApplicationUser> userManager,
            ReclamosDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }


        [HttpGet("me")]
        public async Task<IActionResult> MiPerfil()
        {
            var usuarioId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (usuarioId == null)
            {
                return Unauthorized();
            }

            var usuario = await _userManager.FindByIdAsync(
                usuarioId
            );

            if (usuario == null)
            {
                return NotFound(new
                {
                    mensaje = "Usuario no encontrado."
                });
            }

            var apoyos = await _context.Apoyos
                .CountAsync(a => a.UsuarioId == usuarioId);

            var roles = await _userManager
                .GetRolesAsync(usuario);

            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                roles,

                apoyosRealizados = apoyos
            });
        }


        [HttpGet("mis-reclamos")]
        public async Task<IActionResult> MisReclamos()
        {
            var usuarioId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (usuarioId == null)
            {
                return Unauthorized();
            }

            var reclamos = await _context.Reclamos
                .Include(r => r.ApoyosUsuarios)
                .Where(r => r.UsuarioId == usuarioId)
                .OrderByDescending(r => r.Fecha)
                .Select(r => new
                {
                    r.Id,
                    r.Titulo,
                    r.Tipo,
                    r.Descripcion,
                    r.Direccion,
                    r.Zona,
                    r.Estado,
                    r.Fecha,
                    r.FotoUrl,

                    apoyos = r.ApoyosUsuarios.Count()
                })
                .ToListAsync();

            return Ok(reclamos);
        }
    }
}