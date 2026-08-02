using Microsoft.AspNetCore.Identity;

namespace ReclamosMDP.API.Services
{
    public class RoleInitializer
    {
        public static async Task Initialize(
            IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles =
            {
                "Usuario",
                "Administrador"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var resultado = await roleManager.CreateAsync(
                        new IdentityRole(role)
                    );

                    if (!resultado.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"No se pudo crear el rol {role}."
                        );
                    }
                }
            }
        }
    }
}