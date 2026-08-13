using ReclamosMDP.API.DTOs;

namespace ReclamosMDP.API.Services;

public class CatalogoDatosService
{
    private const string PortalDatos = "https://datos.mardelplata.gob.ar/";

    public IReadOnlyList<CategoriaDatosDto> ObtenerCategorias()
    {
        var categorias = new[]
        {
            Categoria("administracion-publica", "Administración Pública", "fa-landmark", "amarillo", "#seccionDatosPublicos"),
            Categoria("cultura-recreacion", "Cultura y Recreación", "fa-masks-theater", "rosa"),
            Categoria("economia-finanzas", "Economía y Finanzas", "fa-chart-line", "turquesa"),
            Categoria("educacion", "Educación", "fa-book-open", "amarillo"),
            Categoria("electorales", "Electorales", "fa-envelope", "rosa"),
            Categoria("genero", "Género", "fa-people-group", "turquesa"),
            Categoria("infraestructura-obras-publicas", "Infraestructura y Obras Públicas", "fa-city", "amarillo", "#seccionDatosPublicos",
                new FuenteCategoriaDto { Nombre = "Calendario de Licitaciones MGP", Url = "https://licitaciones.mardelplata.gob.ar/", Alcance = "Licitaciones de MGP y entes, pliegos y aperturas" },
                new FuenteCategoriaDto { Nombre = "Obras Sanitarias (OSSE)", Url = "https://www.osmgp.gov.ar/osse/", Alcance = "Obras de agua, cloacas y saneamiento" },
                new FuenteCategoriaDto { Nombre = "SIBOM", Url = "https://sibom.slyt.gba.gob.ar/", Alcance = "Adjudicaciones, contratos, actas y resoluciones" }),
            Categoria("medio-ambiente", "Medio Ambiente", "fa-recycle", "rosa"),
            Categoria("movilidad-transporte", "Movilidad y Transporte", "fa-bus", "turquesa"),
            Categoria("produccion", "Producción", "fa-industry", "amarillo"),
            Categoria("salud-servicios-sociales", "Salud y Servicios Sociales", "fa-kit-medical", "rosa"),
            Categoria("seguridad-justicia", "Seguridad y Justicia", "fa-shield-halved", "turquesa", "#map"),
            Categoria("servicios", "Servicios", "fa-computer", "amarillo"),
            Categoria("sociedad", "Sociedad", "fa-people-roof", "rosa"),
            Categoria("transparencia", "Transparencia", "fa-chart-column", "turquesa"),
            Categoria("transparencia-fiscal", "Transparencia Fiscal", "fa-mobile-screen", "amarillo"),
            Categoria("turismo", "Turismo", "fa-plane", "rosa"),
            Categoria("urbanismo-territorio", "Urbanismo y Territorio", "fa-map-location-dot", "turquesa")
        };

        return categorias;
    }

    private static CategoriaDatosDto Categoria(
        string id,
        string nombre,
        string icono,
        string color,
        string? seccionLocal = null,
        params FuenteCategoriaDto[] complementarias)
    {
        var fuentes = new List<FuenteCategoriaDto>
        {
            new()
            {
                Nombre = "Portal de Datos Abiertos MGP",
                Url = PortalDatos,
                Alcance = "Catálogo oficial de datos abiertos municipales",
                Principal = true
            }
        };
        fuentes.AddRange(complementarias);

        return new CategoriaDatosDto
        {
            Id = id,
            Nombre = nombre,
            Icono = icono,
            Color = color,
            SeccionLocal = seccionLocal,
            Fuentes = fuentes
        };
    }
}
