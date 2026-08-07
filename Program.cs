using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using ReclamosMDP.API.Services;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using Microsoft.AspNetCore.Identity;
using ReclamosMDP.API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// =========================================
// CONFIGURACIÓN
// =========================================

Console.WriteLine(
    "ENTORNO: " +
    builder.Environment.EnvironmentName
);

var connectionString =
    builder.Configuration.GetConnectionString(
        "PostgreSQL"
    );

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No se configuró la conexión PostgreSQL."
    );
}


// =========================================
// SERVICIOS
// =========================================

builder.Services.AddHttpClient<GeocodingService>();

builder.Services.AddHttpClient<ComisariasService>();

builder.Services.AddScoped<SeguridadService>();

builder.Services.AddHttpClient<ObrasImportService>();


builder.Services.AddScoped<JwtService>();

builder.Services.AddSingleton<ZonaService>();


builder.Services.AddDbContext<ReclamosDbContext>(
    options =>
        options.UseNpgsql(connectionString)
);

builder.Services.AddMemoryCache();
// =========================================
// IDENTITY
// =========================================

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(
        options =>
        {
            options.Password.RequireDigit = true;

            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;

            options.Password.RequireNonAlphanumeric = false;

            options.Password.RequiredLength = 6;

            options.User.RequireUniqueEmail = true;
        }
    )
    .AddEntityFrameworkStores<ReclamosDbContext>()
    .AddDefaultTokenProviders();


// =========================================
// JWT + GOOGLE
// =========================================

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })

    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]
                            ?? throw new InvalidOperationException(
                                "Jwt:Key no configurado."
                            )
                        )
                    ),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    })

    .AddGoogle(options =>
    {
        options.ClientId =
            builder.Configuration[
                "Authentication:Google:ClientId"
            ]
            ?? throw new InvalidOperationException(
                "Google ClientId no configurado."
            );

        options.ClientSecret =
            builder.Configuration[
                "Authentication:Google:ClientSecret"
            ]
            ?? throw new InvalidOperationException(
                "Google ClientSecret no configurado."
            );

        options.SignInScheme =
            IdentityConstants.ExternalScheme;
    });


// =========================================
// NGINX / REVERSE PROXY
// =========================================

builder.Services.Configure<ForwardedHeadersOptions>(
    options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
);


// =========================================
// CONTROLLERS
// =========================================

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization
                .ReferenceHandler.IgnoreCycles;
    });



builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo("/var/lib/reclamosmdp/dataprotection")
    )
    .SetApplicationName("MarDelPlataTransparente");


var app = builder.Build();


// =========================================
// ROLES
// =========================================

using (var scope = app.Services.CreateScope())
{
    await RoleInitializer.Initialize(
        scope.ServiceProvider
    );

    await AdminInitializer.Initialize(
        scope.ServiceProvider,
        app.Configuration
    );
}


// =========================================
// PIPELINE
// =========================================

app.UseForwardedHeaders();


if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}


app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html");

await app.RunAsync();