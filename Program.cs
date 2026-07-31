using ReclamosMDP.API.Services;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("ENTORNO: " + builder.Environment.EnvironmentName);

var pruebaConexion = builder.Configuration
    .GetConnectionString("PostgreSQL");

Console.WriteLine(
    "CONEXION: " + (pruebaConexion ?? "NULL")
);

builder.Services.AddHttpClient<GeocodingService>();

builder.Services.AddDbContext<ReclamosDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL")
    )
);

builder.Services.AddSingleton<ZonaService>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseStaticFiles(); // ← importante para wwwroot

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html"); // ← opcional, útil para frontend

app.Run();