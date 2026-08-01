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


        [Authorize]
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
                return NotFound();
            }


            var apoyos = await _context.Apoyos
                .CountAsync(a => a.UsuarioId == usuarioId);


            var roles = await _userManager.GetRolesAsync(usuario);


            return Ok(new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,

                roles,

                apoyosRealizados = apoyos
            });
        }
    }
}