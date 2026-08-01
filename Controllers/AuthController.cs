using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ReclamosMDP.API.Models;
using ReclamosMDP.API.DTOs;
using ReclamosMDP.API.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ReclamosMDP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly SignInManager<ApplicationUser> _signInManager;

        private readonly JwtService _jwtService;

        public AuthController(UserManager<ApplicationUser> userManager,SignInManager<ApplicationUser> signInManager,JwtService jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            var existe = await _userManager.FindByEmailAsync(model.Email);

            if (existe != null)
            {
                return BadRequest("Ya existe una cuenta con ese email");
            }

            var usuario = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Nombre = model.Nombre
            };

            var resultado = await _userManager.CreateAsync(
                usuario,
                model.Password
            );

            if (!resultado.Succeeded)
            {
                return BadRequest(resultado.Errors);
            }

            // Asignar rol por defecto
            await _userManager.AddToRoleAsync(usuario, "Usuario");

            return Ok(new
            {
                mensaje = "Usuario registrado correctamente",
                usuario = new
                {
                    usuario.Id,
                    usuario.Nombre,
                    usuario.Email
                }
            });
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto model)
        {
            var usuario = await _userManager.FindByEmailAsync(model.Email);

            if (usuario == null)
            {
                return Unauthorized("Email o contraseña incorrectos.");
            }

            var resultado = await _signInManager.CheckPasswordSignInAsync(
                usuario,
                model.Password,
                false
            );

            if (!resultado.Succeeded)
            {
                return Unauthorized("Email o contraseña incorrectos.");
            }

            var token = await _jwtService.GenerarToken(usuario);
            var roles = await _userManager.GetRolesAsync(usuario);

            return Ok(new
            {
                token,
                usuario = new
                {
                    usuario.Id,
                    usuario.Nombre,
                    usuario.Email,
                    roles
                }
            });
        }





        [HttpPost("hacer-admin/{email}")]
        public async Task<IActionResult> HacerAdmin(string email)
        {
            var usuario = await _userManager.FindByEmailAsync(email);

            if (usuario == null)
            {
                return NotFound();
            }

            await _userManager.AddToRoleAsync(
                usuario,
                "Administrador"
            );

            return Ok("Usuario convertido en administrador");
        }




        [Authorize]
        [HttpGet("roles")]
        public async Task<IActionResult> Roles()
        {
            var roles = await _userManager.GetRolesAsync(
                await _userManager.GetUserAsync(User)
            );

            return Ok(new
            {
                email = User.FindFirstValue(ClaimTypes.Email),
                roles
            });
        }



        [Authorize]
        [HttpGet("mis-claims")]
        public IActionResult MisClaims()
        {
            return Ok(
                User.Claims.Select(c => new
                {
                    c.Type,
                    c.Value
                })
            );
        }


    }
}