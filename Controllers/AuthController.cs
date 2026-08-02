using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ReclamosMDP.API.Models;
using ReclamosMDP.API.DTOs;
using ReclamosMDP.API.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Text.Json;


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


        private static string CrearRespuestaGoogle(
     object? resultado,
     string? error)
        {
            var payload = JsonSerializer.Serialize(new
            {
                tipo = "google-auth",
                resultado,
                error
            });

                    return $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset=""utf-8"">
                        <title>Autenticación</title>
                    </head>

                    <body>

                    <script>

                        if (window.opener) {{
                            window.opener.postMessage(
                                {payload},
                                window.location.origin
                            );
                        }}

                        window.close();

                    </script>

                    </body>
                    </html>";
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

            var resultadoRol = await _userManager.AddToRoleAsync(usuario, "Usuario");

            if (!resultadoRol.Succeeded)
            {
                return BadRequest(resultadoRol.Errors);
            }

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




        [Authorize(Roles = "Administrador")]
        [HttpPost("hacer-admin/{email}")]
        public async Task<IActionResult> HacerAdmin(string email)
        {
            var usuario = await _userManager.FindByEmailAsync(email);

            if (usuario == null)
            {
                return NotFound(new
                {
                    mensaje = "Usuario no encontrado."
                });
            }

            var yaEsAdmin = await _userManager.IsInRoleAsync(
                usuario,
                "Administrador"
            );

            if (yaEsAdmin)
            {
                return Conflict(new
                {
                    mensaje = "El usuario ya es administrador."
                });
            }

            var resultado = await _userManager.AddToRoleAsync(
                usuario,
                "Administrador"
            );

            if (!resultado.Succeeded)
            {
                return BadRequest(resultado.Errors);
            }

            return Ok(new
            {
                mensaje = "Usuario convertido en administrador."
            });
        }


        //=========================== Opcional ==================
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



        //=========================== Google Login ==================

        [HttpGet("google")]
        public IActionResult GoogleLogin()
        {
            var redirectUrl = Url.Action(
                nameof(GoogleCallback),
                "Auth"
            );

            var propiedades =
                _signInManager.ConfigureExternalAuthenticationProperties(
                    "Google",
                    redirectUrl
                );

            return Challenge(
                propiedades,
                "Google"
            );
        }


        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var info =
                await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                return Content(
                    CrearRespuestaGoogle(
                        null,
                        "No se pudo obtener la información de Google."
                    ),
                    "text/html"
                );
            }


            // Primero intentamos encontrar al usuario
            // por su cuenta externa de Google.

            var usuario = await _userManager.FindByLoginAsync(
                info.LoginProvider,
                info.ProviderKey
            );


            // Si todavía no está vinculada la cuenta Google,
            // buscamos por email.

            if (usuario == null)
            {
                var email = info.Principal.FindFirstValue(
                    ClaimTypes.Email
                );

                if (string.IsNullOrWhiteSpace(email))
                {
                    return Content(
                        CrearRespuestaGoogle(
                            null,
                            "Google no proporcionó un email válido."
                        ),
                        "text/html"
                    );
                }


                usuario = await _userManager.FindByEmailAsync(email);


                // Si tampoco existe el usuario,
                // creamos una nueva cuenta.

                if (usuario == null)
                {
                    var nombre =
                        info.Principal.FindFirstValue(ClaimTypes.Name)
                        ?? email.Split('@')[0];


                    usuario = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        Nombre = nombre
                    };


                    var resultado =
                        await _userManager.CreateAsync(usuario);

                    if (!resultado.Succeeded)
                    {
                        return Content(
                            CrearRespuestaGoogle(
                                null,
                                "No se pudo crear la cuenta."
                            ),
                            "text/html"
                        );
                    }


                    var resultadoRol =
                        await _userManager.AddToRoleAsync(
                            usuario,
                            "Usuario"
                        );

                    if (!resultadoRol.Succeeded)
                    {
                        return Content(
                            CrearRespuestaGoogle(
                                null,
                                "No se pudo asignar el rol al usuario."
                            ),
                            "text/html"
                        );
                    }
                }


                // Vinculamos Google con la cuenta existente/nueva.

                var resultadoLogin =
                    await _userManager.AddLoginAsync(
                        usuario,
                        info
                    );

                if (!resultadoLogin.Succeeded)
                {
                    return Content(
                        CrearRespuestaGoogle(
                            null,
                            "No se pudo vincular la cuenta de Google."
                        ),
                        "text/html"
                    );
                }
            }


            var roles =
                await _userManager.GetRolesAsync(usuario);

            var token =
                await _jwtService.GenerarToken(usuario);


            var resultadoGoogle = new
            {
                token,

                usuario = new
                {
                    id = usuario.Id,
                    nombre = usuario.Nombre,
                    email = usuario.Email,
                    roles
                }
            };


            await HttpContext.SignOutAsync(
                IdentityConstants.ExternalScheme
            );


            return Content(
                CrearRespuestaGoogle(resultadoGoogle, null),
                "text/html"
            );
        }




    }
}