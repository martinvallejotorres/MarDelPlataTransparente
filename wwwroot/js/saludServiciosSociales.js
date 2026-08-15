let saludDatos = null;
let solicitudSalud = 0;
let graficoMesesSalud = null;
let graficoEspecialidadesSalud = null;
let graficoCentrosSalud = null;
let centrosSaludGeoJson = null;
let cargaCentrosSalud = null;

const centrosSaludLayer = L.markerClusterGroup({ chunkedLoading: true, disableClusteringAtZoom: 16, maxClusterRadius: 42, showCoverageOnHover: false });

document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("anioSalud")?.addEventListener("change", cargarSaludServiciosSociales);
    document.getElementById("centroSalud")?.addEventListener("change", renderizarSalud);
    document.getElementById("especialidadSalud")?.addEventListener("change", renderizarSalud);
    document.getElementById("activarCentrosSalud")?.addEventListener("click", async () => {
        const toggle = document.getElementById("toggleCentrosSalud");
        if (toggle && !toggle.checked) toggle.click();
        await cargarCentrosSalud();
        const limites = centrosSaludLayer.getBounds();
        if (limites.isValid()) map.fitBounds(limites.pad(.12));
    });
});

document.addEventListener("change", event => {
    if (event.target.id === "toggleCentrosSalud") alternarCentrosSalud(event.target.checked).catch(error => {
        event.target.checked = false;
        console.error("Error cargando centros de salud:", error);
        mostrarToast?.("Capa de salud no disponible", "No pudimos cargar las ubicaciones oficiales.", "warning");
    });
});

async function cargarSaludServiciosSociales() {
    const panel = document.getElementById("panelCategoriaSalud");
    if (!panel || panel.hidden) return;
    const solicitud = ++solicitudSalud;
    const anio = document.getElementById("anioSalud")?.value || "2025";
    panel.classList.add("cargando");
    try {
        const response = await fetch(`/api/datos-publicos/salud-servicios-sociales?anio=${anio}`);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const resultado = await response.json();
        if (solicitud !== solicitudSalud) return;
        saludDatos = resultado.resumen;
        completarSelectorSalud("centroSalud", saludDatos.centros, "Todos los centros", "todos");
        completarSelectorSalud("especialidadSalud", saludDatos.prestaciones, "Todas las especialidades", "todas");
        renderizarSalud();
        cargarCentrosSalud().catch(() => {});
    } catch (error) {
        console.error("Error cargando Salud y Servicios Sociales:", error);
        mostrarToast?.("Datos de salud no disponibles", "No pudimos leer la publicación municipal. Probá nuevamente.", "warning");
    } finally {
        if (solicitud === solicitudSalud) panel.classList.remove("cargando");
    }
}

function completarSelectorSalud(id, opciones, etiqueta, valorInicial) {
    const select = document.getElementById(id);
    if (!select) return;
    const anterior = select.value;
    select.replaceChildren(new Option(etiqueta, valorInicial), ...(opciones || []).map(x => new Option(x, x)));
    select.value = [...select.options].some(x => x.value === anterior) ? anterior : valorInicial;
}

function detalleSaludFiltrado() {
    const centro = document.getElementById("centroSalud")?.value || "todos";
    const prestacion = document.getElementById("especialidadSalud")?.value || "todas";
    return (saludDatos?.detalle || []).filter(x => (centro === "todos" || x.centro === centro) && (prestacion === "todas" || x.prestacion === prestacion));
}

function renderizarSalud() {
    if (!saludDatos) return;
    renderizarEstadoPublicacion("estadoPublicacionSalud", saludDatos);
    const detalle = detalleSaludFiltrado();
    const meses = Array.from({ length: 12 }, (_, i) => detalle.reduce((s, x) => s + Number(x.meses?.[i] || 0), 0));
    const total = meses.reduce((a, b) => a + b, 0);
    const centros = agruparDetalleSalud(detalle, "centro");
    const prestaciones = agruparDetalleSalud(detalle, "prestacion");
    const sinSerie = saludDatos.esParcial && !saludDatos.serieOperativaPublicada;
    document.querySelectorAll(".serie-salud-grafico").forEach(x => { x.hidden = sinSerie; });
    document.getElementById("consultasSalud").textContent = sinSerie ? "Sin publicación" : total.toLocaleString("es-AR");
    document.getElementById("centrosActividadSalud").textContent = sinSerie ? "Sin publicación" : centros.length.toLocaleString("es-AR");
    document.getElementById("especialidadesSalud").textContent = sinSerie ? "Sin publicación" : prestaciones.length.toLocaleString("es-AR");
    document.getElementById("periodoSalud").innerHTML = `Cobertura publicada: <strong>${escaparHtml(saludDatos.cobertura)}</strong>`;
    renderizarGraficosSalud(meses, centros, prestaciones);
}

function agruparDetalleSalud(detalle, campo) {
    const grupos = new Map();
    detalle.forEach(x => grupos.set(x[campo], (grupos.get(x[campo]) || 0) + (x.meses || []).reduce((a, b) => a + Number(b || 0), 0)));
    return [...grupos].map(([nombre, cantidad]) => ({ nombre, cantidad })).sort((a, b) => b.cantidad - a.cantidad);
}

function renderizarGraficosSalud(meses, centros, prestaciones) {
    if (typeof Chart === "undefined") return;
    graficoMesesSalud = reemplazarGraficoSalud(graficoMesesSalud, "graficoMesesSalud", "bar", ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"], meses, "Consultas", "#e11d48");
    graficoEspecialidadesSalud = reemplazarGraficoSalud(graficoEspecialidadesSalud, "graficoEspecialidadesSalud", "bar", prestaciones.slice(0, 12).map(x => x.nombre), prestaciones.slice(0, 12).map(x => x.cantidad), "Consultas", "#fb7185", true);
    graficoCentrosSalud = reemplazarGraficoSalud(graficoCentrosSalud, "graficoCentrosSalud", "bar", centros.slice(0, 12).map(x => x.nombre), centros.slice(0, 12).map(x => x.cantidad), "Consultas", "#0ea5e9", true);
}

function reemplazarGraficoSalud(actual, id, tipo, labels, valores, etiqueta, color, horizontal = false) {
    actual?.destroy();
    const canvas = document.getElementById(id);
    return canvas ? new Chart(canvas, { type: tipo, data: { labels, datasets: [{ label: etiqueta, data: valores, backgroundColor: color, borderRadius: 5 }] }, options: { responsive: true, maintainAspectRatio: false, indexAxis: horizontal ? "y" : "x", plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true }, y: { beginAtZero: true } } } }) : null;
}

async function cargarCentrosSalud() {
    if (centrosSaludGeoJson) return centrosSaludGeoJson;
    if (cargaCentrosSalud) return cargaCentrosSalud;
    cargaCentrosSalud = fetch("/api/datos-publicos/salud-servicios-sociales/centros").then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); }).then(datos => {
        centrosSaludGeoJson = datos;
        centrosSaludLayer.clearLayers();
        L.geoJSON(datos, { pointToLayer: (feature, latlng) => L.circleMarker(latlng, { radius: 7, color: "#be123c", fillColor: "#fb7185", fillOpacity: .9, weight: 2 }), onEachFeature: (feature, layer) => {
            const p = feature.properties || {};
            const nombre = p.nombre || p.Nombre || p.establecimiento || p.Centro || "Centro municipal de salud";
            const domicilio = p.domicilio || p.Domicilio || p.direccion || p.Direccion || [p.calle, Number(p.altura) > 0 ? p.altura : ""].filter(Boolean).join(" ") || "Domicilio no informado";
            layer.bindTooltip(`<div class="obra-tooltip"><strong>${escaparHtml(String(nombre))}</strong><span>${escaparHtml(String(domicilio))}</span></div>`, { sticky: true });
        }}).eachLayer(x => centrosSaludLayer.addLayer(x));
        document.getElementById("centrosMapaSalud").textContent = (datos.features || []).length.toLocaleString("es-AR");
        return datos;
    }).finally(() => { cargaCentrosSalud = null; });
    return cargaCentrosSalud;
}

async function alternarCentrosSalud(activo) {
    if (activo) { await cargarCentrosSalud(); centrosSaludLayer.addTo(map); }
    else if (map.hasLayer(centrosSaludLayer)) map.removeLayer(centrosSaludLayer);
}
