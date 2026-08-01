using ReclamosMDP.API.Services;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using Microsoft.AspNetCore.Identity;
using ReclamosMDP.API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("ENTORNO: " + builder.Environment.EnvironmentName);

var pruebaConexion = builder.Configuration
    .GetConnectionString("PostgreSQL");

Console.WriteLine(
    "CONEXION: " + (pruebaConexion ?? "NULL")
);

builder.Services.AddHttpClient<GeocodingService>();

builder.Services.AddScoped<JwtService>();

builder.Services.AddDbContext<ReclamosDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL")
    )
);


// Identity configuration

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ReclamosDbContext>()
    .AddDefaultTokenProviders();

// JWT Authentication configuration

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                builder.Configuration["Jwt:Key"]!
            ))
    };
});



builder.Services.AddSingleton<ZonaService>();

//builder.Services.AddControllers();

builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Pipeline

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    await RoleInitializer.Initialize(scope.ServiceProvider);
}

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles =
    {
        "Ciudadano",
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

app.UseHttpsRedirection();


app.UseStaticFiles(); // ← importante para wwwroot

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html"); // ← opcional, útil para frontend

await app.RunAsync();