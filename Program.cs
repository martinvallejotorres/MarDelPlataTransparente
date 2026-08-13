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
using System.Threading.RateLimiting;

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
builder.Services.AddScoped<ObrasRepository>();
builder.Services.AddSingleton<CatalogoDatosService>();
builder.Services.AddHttpClient<AdministracionPublicaService>();
builder.Services.AddHttpClient<MovilidadPublicaService>();
builder.Services.AddHttpClient<MedioAmbienteService>();

builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(30));
});


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

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
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
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(
                        "reclamos_auth",
                        out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };

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

    }
);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("datos-externos", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});


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


if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net https://unpkg.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://unpkg.com https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
        "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com data:; " +
        "img-src 'self' data: blob: https://*.openstreetmap.org https://server.arcgisonline.com https://placehold.co; " +
        "connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";

    await next();
});

app.UseStaticFiles();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "El endpoint solicitado no existe."
        });
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "index.html"));
});

await app.RunAsync();
