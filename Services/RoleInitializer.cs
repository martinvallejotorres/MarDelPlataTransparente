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
                "Moderador",
                "Administrador"
            };


            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role)
                    );
                }
            }
        }
    }
}