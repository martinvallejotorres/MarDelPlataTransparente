using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReclamosMDP.API.Data;
using ReclamosMDP.API.DTOs;
using ReclamosMDP.API.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ReclamosMDP.API.Services;

public class ObrasRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ReclamosDbContext _db;

    public ObrasRepository(ReclamosDbContext db) => _db = db;

    public async Task<ObraDetalleDto?> Obtener(int eventoId)
    {
        var entidad = await _db.ObrasPublicas.AsNoTracking()
            .Include(o => o.Tramos)
            .SingleOrDefaultAsync(o => o.EventoId == eventoId);
        return entidad == null ? null : ADto(entidad);
    }

    public async Task<List<ObraDetalleDto>> ObtenerPorAnio(int anio)
    {
        var entidades = await _db.ObrasPublicas.AsNoTracking()
            .Include(o => o.Tramos)
            .Where(o => o.AnioFuente == anio)
            .OrderByDescending(o => o.FechaApertura)
            .ThenByDescending(o => o.EventoId)
            .ToListAsync();
        return entidades.Select(ADto).ToList();
    }

    public async Task<List<ObraDetalleDto>> ObtenerConTramosCompletos(
        int anioDesde,
        int anioHasta)
    {
        var entidades = await _db.ObrasPublicas.AsNoTracking()
            .Include(o => o.Tramos)
            .Where(o => o.AnioFuente >= anioDesde &&
                        o.AnioFuente <= anioHasta &&
                        o.Tramos.Any(t =>
                            t.LatitudInicio != null &&
                            t.LongitudInicio != null &&
                            t.LatitudFin != null &&
                            t.LongitudFin != null))
            .OrderByDescending(o => o.AnioFuente)
            .ThenByDescending(o => o.FechaApertura)
            .ToListAsync();

        return entidades.Select(entidad =>
        {
            var dto = ADto(entidad);
            dto.Tramos = dto.Tramos.Where(t =>
                t.LatitudInicio.HasValue &&
                t.LongitudInicio.HasValue &&
                t.LatitudFin.HasValue &&
                t.LongitudFin.HasValue).ToList();
            return dto;
        }).ToList();
    }

    public Task<bool> EstaSincronizado(int anio) =>
        _db.ObrasAniosSincronizados.AnyAsync(s => s.Anio == anio);

    public async Task MarcarSincronizado(int anio, int cantidad)
    {
        var estado = await _db.ObrasAniosSincronizados.FindAsync(anio);
        if (estado == null)
        {
            estado = new ObrasAnioSincronizacion { Anio = anio };
            _db.ObrasAniosSincronizados.Add(estado);
        }
        estado.Cantidad = cantidad;
        estado.CompletadaUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task EliminarFueraDeSincronizacion(int anio, IReadOnlyCollection<int> eventosVigentes)
    {
        var obsoletas = await _db.ObrasPublicas
            .Where(o => o.AnioFuente == anio && !eventosVigentes.Contains(o.EventoId))
            .ToListAsync();
        if (obsoletas.Count == 0) return;
        _db.ObrasPublicas.RemoveRange(obsoletas);
        await _db.SaveChangesAsync();
    }

    public async Task<List<ComparacionTramoDto>> CompararTramos(int anioDesde, int anioHasta)
    {
        var tramos = await _db.ObrasTramos.AsNoTracking()
            .Include(t => t.Obra)
            .Where(t => t.Obra.AnioFuente >= anioDesde && t.Obra.AnioFuente <= anioHasta)
            .ToListAsync();

        return tramos.GroupBy(t => $"{Normalizar(t.Calle)}|{OrdenarExtremos(t.Desde, t.Hasta)}")
            .Select(g => new ComparacionTramoDto
            {
                Calle = g.First().Calle,
                Desde = g.First().Desde,
                Hasta = g.First().Hasta,
                Anios = g.Select(t => t.Obra.AnioFuente).Distinct().OrderBy(a => a).ToList(),
                Eventos = g.Select(t => t.ObraEventoId).Distinct().OrderBy(id => id).ToList()
            })
            .Where(c => c.Anios.Count > 1)
            .OrderBy(c => c.Calle)
            .ToList();
    }

    private static string OrdenarExtremos(string desde, string hasta) =>
        string.Join("|", new[] { Normalizar(desde), Normalizar(hasta) }.OrderBy(x => x));

    private static string Normalizar(string texto)
    {
        var value = (texto ?? "").Normalize(NormalizationForm.FormD);
        value = new string(value.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "");
    }

    public async Task Guardar(ObraDetalleDto dto, int anioFuente, int? mesFuente = null)
    {
        var entidad = await _db.ObrasPublicas
            .Include(o => o.Tramos)
            .SingleOrDefaultAsync(o => o.EventoId == dto.EventoId);

        if (entidad == null)
        {
            entidad = new ObraPublica { EventoId = dto.EventoId };
            _db.ObrasPublicas.Add(entidad);
        }

        entidad.AnioFuente = anioFuente;
        entidad.MesFuente = mesFuente;
        entidad.Nombre = dto.Nombre;
        entidad.Organismo = dto.Organismo;
        entidad.Expediente = dto.Expediente;
        entidad.Licitacion = dto.Licitacion;
        entidad.UbicacionTexto = dto.UbicacionTexto;
        entidad.Estado = dto.Estado;
        entidad.FuenteUrl = dto.FuenteUrl;
        entidad.Plazo = dto.Plazo;
        entidad.TipoGeometria = dto.TipoGeometria;
        entidad.PresupuestoOficial = dto.PresupuestoOficial;
        entidad.GarantiaOferta = dto.GarantiaOferta;
        entidad.FechaApertura = dto.FechaApertura.HasValue
            ? DateTime.SpecifyKind(dto.FechaApertura.Value, DateTimeKind.Utc)
            : null;
        entidad.SuperficieM2 = dto.SuperficieM2;
        entidad.FrentesTrabajo = dto.FrentesTrabajo;
        entidad.DocumentosJson = JsonSerializer.Serialize(dto.Documentos, JsonOptions);
        entidad.UbicacionesJson = JsonSerializer.Serialize(dto.Ubicaciones, JsonOptions);
        entidad.EventosRelacionadosJson = JsonSerializer.Serialize(dto.EventosRelacionados, JsonOptions);
        entidad.ActualizadaUtc = DateTime.UtcNow;

        _db.ObrasTramos.RemoveRange(entidad.Tramos);
        entidad.Tramos = dto.Tramos.Select(t => new ObraTramo
        {
            ObraEventoId = dto.EventoId,
            Descripcion = t.Descripcion,
            Calle = t.Calle,
            Desde = t.Desde,
            Hasta = t.Hasta,
            LatitudInicio = t.LatitudInicio,
            LongitudInicio = t.LongitudInicio,
            LatitudFin = t.LatitudFin,
            LongitudFin = t.LongitudFin
        }).ToList();

        await _db.SaveChangesAsync();
    }

    private static ObraDetalleDto ADto(ObraPublica o) => new()
    {
        EventoId = o.EventoId,
        Nombre = LimpiarNombre(o.Nombre),
        Organismo = o.Organismo,
        Expediente = o.Expediente,
        Licitacion = string.IsNullOrWhiteSpace(o.Licitacion)
            ? ExtraerLicitacionNombre(o.Nombre)
            : o.Licitacion,
        UbicacionTexto = o.UbicacionTexto,
        Estado = o.Estado,
        FuenteUrl = o.FuenteUrl,
        Plazo = o.Plazo,
        TipoGeometria = o.TipoGeometria,
        PresupuestoOficial = o.PresupuestoOficial,
        GarantiaOferta = o.GarantiaOferta,
        FechaApertura = o.FechaApertura,
        SuperficieM2 = o.SuperficieM2,
        FrentesTrabajo = o.FrentesTrabajo,
        Documentos = Deserializar<List<DocumentoObraDto>>(o.DocumentosJson) ?? new(),
        Ubicaciones = Deserializar<List<UbicacionObraDto>>(o.UbicacionesJson) ?? new(),
        EventosRelacionados = Deserializar<List<int>>(o.EventosRelacionadosJson) ?? new(),
        AnioFuente = o.AnioFuente,
        Tramos = o.Tramos.Select(t => new TramoObraDto
        {
            Descripcion = t.Descripcion,
            Calle = t.Calle,
            Desde = t.Desde,
            Hasta = t.Hasta,
            LatitudInicio = t.LatitudInicio,
            LongitudInicio = t.LongitudInicio,
            LatitudFin = t.LatitudFin,
            LongitudFin = t.LongitudFin
        }).ToList()
    };

    private static T? Deserializar<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return default; }
    }

    private static string LimpiarNombre(string nombre)
    {
        if (!nombre.StartsWith("Detalles Licitación", StringComparison.OrdinalIgnoreCase))
            return nombre;

        var objeto = Regex.Match(nombre,
            @"(BACHEO|PAVIMENTACI[ÓO]N|REPAVIMENTACI[ÓO]N|CONSTRUCCI[ÓO]N|RECONSTRUCCI[ÓO]N|CICLOV[ÍI]A).+?(?=\s+D[ií]a\s+Hora|$)",
            RegexOptions.IgnoreCase);
        return objeto.Success ? objeto.Value.Trim() : nombre;
    }

    private static string ExtraerLicitacionNombre(string nombre)
    {
        var match = Regex.Match(nombre,
            @"LICITACI[ÓO]N\s+(P[ÚU]BLICA|PRIVADA)(?:\s+EMVIAL)?\s+(?:NRO\.?\s*)?(\d+)\s*[/\-]\s*(\d{2,4})",
            RegexOptions.IgnoreCase);
        if (!match.Success) return "";
        var anio = match.Groups[3].Value;
        if (anio.Length == 2) anio = $"20{anio}";
        var tipo = match.Groups[1].Value.Contains("PRIVADA", StringComparison.OrdinalIgnoreCase)
            ? "Licitación Privada"
            : "Licitación Pública";
        return $"{tipo} {match.Groups[2].Value}/{anio}";
    }
}
