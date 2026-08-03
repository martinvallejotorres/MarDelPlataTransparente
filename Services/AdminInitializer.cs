using Microsoft.AspNetCore.Identity;
using ReclamosMDP.API.Models;

namespace ReclamosMDP.API.Services
{
    public static class AdminInitializer
    {
        public static async Task Initialize(
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            var userManager = serviceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var adminEmail = configuration["Admin:Email"];

            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return;
            }

            var usuario = await userManager.FindByEmailAsync(adminEmail);

            if (usuario == null)
            {
                Console.WriteLine(
                    $"No se encontró el usuario administrador: {adminEmail}"
                );

                return;
            }

            if (!await userManager.IsInRoleAsync(
                    usuario,
                    "Administrador"))
            {
                var resultado = await userManager.AddToRoleAsync(
                    usuario,
                    "Administrador"
                );

                if (!resultado.Succeeded)
                {
                    throw new Exception(
                        "No se pudo asignar el rol Administrador."
                    );
                }
            }

            Console.WriteLine(
                $"Administrador configurado: {adminEmail}"
            );
        }
    }
}