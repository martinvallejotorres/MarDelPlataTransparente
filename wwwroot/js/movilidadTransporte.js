let movilidadDatosActual = null;
let movilidadCargada = false;
let solicitudMovilidad = 0;
let recorridosMovilidadGeoJson = null;
let paradasMovilidadGeoJson = null;
let cargaRecorridosMovilidad = null;
let cargaParadasMovilidad = null;
let graficoPasajerosMovilidad = null;
let graficoRecorridoMovilidad = null;
let graficoFrecuenciasMovilidad = null;
let graficoFlotaMovilidad = null;

const recorridosColectivosLayer = L.geoJSON(null, {
    style: feature => ({
        color: colorLineaMovilidad(feature?.properties?.col1 || ""),
        weight: 4,
        opacity: .82
    }),
    onEachFeature: (feature, layer) => {
        const linea = feature?.properties?.col1 || "Sin línea";
        const detalle = (feature?.properties?.col2 || "").replace(";", " · ");
        layer.bindTooltip(
            `<div class="obra-tooltip"><strong>Línea ${escaparHtml(linea)}</strong><span>${escaparHtml(detalle)}</span></div>`,
            { sticky: true }
        );
    }
});

const paradasColectivosLayer = L.markerClusterGroup({
    chunkedLoading: true,
    disableClusteringAtZoom: 17,
    maxClusterRadius: 45,
    showCoverageOnHover: false
});

document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("anioMovilidad")?.addEventListener("change", cargarMovilidadTransporte);
    document.getElementById("empresaMovilidad")?.addEventListener("change", () => {
        actualizarLineasMovilidad();
        renderizarMovilidad();
        refrescarCapasMovilidadActivas();
    });
    document.getElementById("lineaMovilidad")?.addEventListener("change", () => {
        renderizarMovilidad();
        refrescarCapasMovilidadActivas();
    });

    document.getElementById("activarRecorridosMovilidad")?.addEventListener("click", async () => {
        await activarRecorridosColectivos();
        enfocarCapaMovilidad(recorridosColectivosLayer);
    });
    document.getElementById("activarParadasMovilidad")?.addEventListener("click", async () => {
        await activarParadasColectivos();
        enfocarCapaMovilidad(paradasColectivosLayer);
    });
    document.getElementById("activarCicloviasMovilidad")?.addEventListener("click", async () => {
        try {
            await cargarTramosObras();
            activarCapaTramos();
            document.getElementById("map")?.scrollIntoView({ behavior: "smooth", block: "center" });
        }
        catch (error) {
            mostrarToast?.("Ciclovías no disponibles", "No pudimos cargar los tramos documentados.", "warning");
        }
    });
});

async function cargarMovilidadTransporte() {
    const panel = document.getElementById("panelCategoriaMovilidad");
    if (!panel || panel.hidden) return;

    const solicitud = ++solicitudMovilidad;
    const anio = document.getElementById("anioMovilidad")?.value || "2025";
    panel.classList.add("cargando");

    try {
        const response = await fetch(`/api/datos-publicos/movilidad-transporte?anio=${anio}`);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const resultado = await response.json();
        if (solicitud !== solicitudMovilidad) return;

        movilidadDatosActual = resultado.resumen;
        actualizarEmpresasMovilidad();
        actualizarLineasMovilidad();
        renderizarMovilidad();
        movilidadCargada = true;
    }
    catch (error) {
        console.error("Error cargando Movilidad y Transporte:", error);
        mostrarToast?.("Movilidad no disponible", "No pudimos leer las series municipales. Probá nuevamente.", "warning");
    }
    finally {
        if (solicitud === solicitudMovilidad) panel.classList.remove("cargando");
    }
}

function actualizarEmpresasMovilidad() {
    const select = document.getElementById("empresaMovilidad");
    if (!select || !movilidadDatosActual) return;
    const anterior = select.value;
    const empresas = [...new Set(movilidadDatosActual.lineas.map(x => x.empresa).filter(Boolean))]
        .sort((a, b) => a.localeCompare(b, "es"));
    select.innerHTML = `<option value="todas">Todas las empresas</option>` +
        empresas.map(empresa => `<option value="${escaparHtml(empresa)}">${escaparHtml(empresa)}</option>`).join("");
    select.value = empresas.includes(anterior) ? anterior : "todas";
}

function actualizarLineasMovilidad() {
    const select = document.getElementById("lineaMovilidad");
    if (!select || !movilidadDatosActual) return;
    const anterior = select.value;
    const empresa = document.getElementById("empresaMovilidad")?.value || "todas";
    const lineas = movilidadDatosActual.lineas
        .filter(x => empresa === "todas" || x.empresa === empresa)
        .map(x => x.linea)
        .filter((linea, indice, todas) => todas.indexOf(linea) === indice)
        .sort((a, b) => a.localeCompare(b, "es", { numeric: true }));
    select.innerHTML = `<option value="todas">Todas las líneas</option>` +
        lineas.map(linea => `<option value="${escaparHtml(linea)}">Línea ${escaparHtml(linea)}</option>`).join("");
    select.value = lineas.includes(anterior) ? anterior : "todas";
}

function lineasSeleccionadasMovilidad() {
    if (!movilidadDatosActual) return [];
    const empresa = document.getElementById("empresaMovilidad")?.value || "todas";
    const linea = document.getElementById("lineaMovilidad")?.value || "todas";
    return movilidadDatosActual.lineas
        .filter(x => (empresa === "todas" || x.empresa === empresa) && (linea === "todas" || x.linea === linea))
        .map(x => x.linea);
}

function renderizarMovilidad() {
    if (!movilidadDatosActual) return;
    const datos = movilidadDatosActual;
    const empresa = document.getElementById("empresaMovilidad")?.value || "todas";
    const lineas = lineasSeleccionadasMovilidad();
    const flotaFiltrada = empresa === "todas"
        ? datos.flotaPorEmpresa
        : datos.flotaPorEmpresa.filter(x => claveEmpresaMovilidad(x.empresa) === claveEmpresaMovilidad(empresa));

    document.getElementById("pasajerosMovilidad").textContent = formatoCompactoMovilidad(datos.pasajerosTotal);
    document.getElementById("kilometrosMovilidad").textContent = `${formatoCompactoMovilidad(datos.kilometrosTotal)} km`;
    document.getElementById("recaudacionMovilidad").textContent = formatoDineroCompactoMovilidad(datos.recaudacionTotal);
    document.getElementById("flotaMovilidad").textContent = flotaFiltrada.reduce((total, x) => total + x.unidades, 0).toLocaleString("es-AR");
    document.getElementById("cantidadLineasMovilidad").textContent = lineas.length.toLocaleString("es-AR");
    document.getElementById("periodoMovilidad").innerHTML =
        `Serie oficial: <strong>${escaparHtml(datos.periodo)}</strong> · IPK promedio: <strong>${Number(datos.ipkPromedio).toLocaleString("es-AR", { maximumFractionDigits: 2 })}</strong>`;

    renderizarGraficosMovilidad(lineas, flotaFiltrada);
}

function renderizarGraficosMovilidad(lineas, flotaFiltrada) {
    const datos = movilidadDatosActual;
    if (!datos || typeof Chart === "undefined") return;
    const meses = datos.meses;

    graficoPasajerosMovilidad = reemplazarGraficoMovilidad(graficoPasajerosMovilidad, "graficoPasajerosMovilidad", {
        type: "bar",
        data: {
            labels: meses.map(x => x.mes),
            datasets: [
                { label: "Boleto plano", data: meses.map(x => x.boletoPlano), backgroundColor: "#2563eb", stack: "pasajeros" },
                { label: "Diferencial", data: meses.map(x => x.boletoDiferencial), backgroundColor: "#8b5cf6", stack: "pasajeros" },
                { label: "Suburbano", data: meses.map(x => x.boletoSuburbano), backgroundColor: "#f59e0b", stack: "pasajeros" },
                { label: "Gratuitos SUBE", data: meses.map(x => x.gratuitosSube), backgroundColor: "#14b8a6", stack: "pasajeros" }
            ]
        },
        options: opcionesGraficoMovilidad({ scales: { y: { beginAtZero: true, stacked: true, ticks: { callback: formatoCompactoMovilidad } }, x: { stacked: true } } })
    });

    graficoRecorridoMovilidad = reemplazarGraficoMovilidad(graficoRecorridoMovilidad, "graficoRecorridoMovilidad", {
        type: "bar",
        data: {
            labels: meses.map(x => x.mes),
            datasets: [
                { label: "Kilómetros", data: meses.map(x => x.kilometros), backgroundColor: "#0f766e", borderRadius: 5, yAxisID: "y" },
                { label: "IPK", data: meses.map(x => x.ipk), type: "line", borderColor: "#db3b87", backgroundColor: "#db3b87", tension: .25, yAxisID: "y1" }
            ]
        },
        options: opcionesGraficoMovilidad({ scales: { y: { beginAtZero: true, ticks: { callback: formatoCompactoMovilidad } }, y1: { beginAtZero: true, position: "right", grid: { drawOnChartArea: false } } } })
    });

    const permitidas = new Set(lineas.map(normalizarLineaMovilidad));
    const frecuencias = datos.frecuencias
        .filter(x => permitidas.size === 0 || permitidas.has(normalizarLineaMovilidad(x.linea)))
        .slice(0, 15);
    graficoFrecuenciasMovilidad = reemplazarGraficoMovilidad(graficoFrecuenciasMovilidad, "graficoFrecuenciasMovilidad", {
        type: "bar",
        data: {
            labels: frecuencias.map(x => x.linea),
            datasets: [{ label: "Servicios diarios mínimos", data: frecuencias.map(x => x.serviciosDiarios), backgroundColor: "#2563eb", borderRadius: 6 }]
        },
        options: opcionesGraficoMovilidad({ indexAxis: "y", scales: { x: { beginAtZero: true, ticks: { precision: 0 } } } })
    });

    graficoFlotaMovilidad = reemplazarGraficoMovilidad(graficoFlotaMovilidad, "graficoFlotaMovilidad", {
        type: "bar",
        data: {
            labels: flotaFiltrada.map(x => x.empresa),
            datasets: [{ label: "Unidades", data: flotaFiltrada.map(x => x.unidades), backgroundColor: "#14b8a6", borderRadius: 6 }]
        },
        options: opcionesGraficoMovilidad({ indexAxis: "y", scales: { x: { beginAtZero: true, ticks: { precision: 0 } } } })
    });
}

function reemplazarGraficoMovilidad(actual, id, configuracion) {
    actual?.destroy();
    const canvas = document.getElementById(id);
    return canvas ? new Chart(canvas, configuracion) : null;
}

function opcionesGraficoMovilidad(adicionales = {}) {
    return {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { position: "bottom" } },
        ...adicionales
    };
}

async function cargarRecorridosColectivos() {
    if (recorridosMovilidadGeoJson) return;
    if (cargaRecorridosMovilidad) return cargaRecorridosMovilidad;
    cargaRecorridosMovilidad = fetch("/api/datos-publicos/movilidad-transporte/recorridos")
        .then(response => {
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            return response.json();
        })
        .then(datos => { recorridosMovilidadGeoJson = datos; })
        .finally(() => { cargaRecorridosMovilidad = null; });
    return cargaRecorridosMovilidad;
}

async function cargarParadasColectivos() {
    if (paradasMovilidadGeoJson) return;
    if (cargaParadasMovilidad) return cargaParadasMovilidad;
    cargaParadasMovilidad = fetch("/api/datos-publicos/movilidad-transporte/paradas")
        .then(response => {
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            return response.json();
        })
        .then(datos => { paradasMovilidadGeoJson = datos; })
        .finally(() => { cargaParadasMovilidad = null; });
    return cargaParadasMovilidad;
}

function renderizarRecorridosColectivos() {
    if (!recorridosMovilidadGeoJson) return;
    const filtro = filtroLineasMapaMovilidad();
    recorridosColectivosLayer.clearLayers();
    recorridosColectivosLayer.addData({
        type: "FeatureCollection",
        features: recorridosMovilidadGeoJson.features.filter(feature =>
            filtro === null || filtro.has(normalizarLineaMovilidad(feature?.properties?.col1)))
    });
}

function renderizarParadasColectivos() {
    if (!paradasMovilidadGeoJson) return;
    const filtro = filtroLineasMapaMovilidad();
    paradasColectivosLayer.clearLayers();
    paradasMovilidadGeoJson.features
        .filter(feature => filtro === null || [...filtro].some(linea =>
            normalizarLineaMovilidad(feature?.properties?.linea).includes(linea)))
        .forEach(feature => {
            const coordenadas = feature?.geometry?.coordinates;
            if (!Array.isArray(coordenadas) || coordenadas.length < 2) return;
            const linea = feature?.properties?.linea || "Sin línea informada";
            const marcador = L.circleMarker([coordenadas[1], coordenadas[0]], {
                radius: 5,
                color: "#0f766e",
                fillColor: "#14b8a6",
                fillOpacity: .85,
                weight: 1.5
            });
            marcador.bindTooltip(`<strong>Parada</strong><br>Línea ${escaparHtml(linea)}`);
            paradasColectivosLayer.addLayer(marcador);
        });
}

function filtroLineasMapaMovilidad() {
    const empresa = document.getElementById("empresaMovilidad")?.value || "todas";
    const linea = document.getElementById("lineaMovilidad")?.value || "todas";
    if (empresa === "todas" && linea === "todas") return null;
    return new Set(lineasSeleccionadasMovilidad().map(normalizarLineaMovilidad));
}

async function activarRecorridosColectivos() {
    try {
        await cargarRecorridosColectivos();
        renderizarRecorridosColectivos();
        if (!map.hasLayer(recorridosColectivosLayer)) recorridosColectivosLayer.addTo(map);
        const toggle = document.getElementById("toggleRecorridosColectivos");
        if (toggle) toggle.checked = true;
    }
    catch (error) {
        mostrarToast?.("Recorridos no disponibles", "La fuente geográfica municipal no respondió.", "warning");
    }
}

async function activarParadasColectivos() {
    try {
        await cargarParadasColectivos();
        renderizarParadasColectivos();
        if (!map.hasLayer(paradasColectivosLayer)) paradasColectivosLayer.addTo(map);
        const toggle = document.getElementById("toggleParadasColectivos");
        if (toggle) toggle.checked = true;
    }
    catch (error) {
        mostrarToast?.("Paradas no disponibles", "La fuente geográfica municipal no respondió.", "warning");
    }
}

function refrescarCapasMovilidadActivas() {
    if (map.hasLayer(recorridosColectivosLayer)) renderizarRecorridosColectivos();
    if (map.hasLayer(paradasColectivosLayer)) renderizarParadasColectivos();
}

function enfocarCapaMovilidad(capa) {
    document.getElementById("map")?.scrollIntoView({ behavior: "smooth", block: "center" });
    const capas = capa.getLayers?.() || [];
    if (!capas.length) return;
    setTimeout(() => {
        map.invalidateSize();
        const limites = capa.getBounds?.();
        if (limites?.isValid()) map.flyToBounds(limites, { padding: [35, 35], maxZoom: 15, duration: 1 });
    }, 450);
}

document.addEventListener("change", async event => {
    if (event.target.id === "toggleRecorridosColectivos") {
        if (event.target.checked) await activarRecorridosColectivos();
        else if (map.hasLayer(recorridosColectivosLayer)) map.removeLayer(recorridosColectivosLayer);
    }
    if (event.target.id === "toggleParadasColectivos") {
        if (event.target.checked) await activarParadasColectivos();
        else if (map.hasLayer(paradasColectivosLayer)) map.removeLayer(paradasColectivosLayer);
    }
});

function normalizarLineaMovilidad(valor) {
    return String(valor || "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toUpperCase().replace(/[^A-Z0-9]/g, "");
}

function claveEmpresaMovilidad(valor) {
    const texto = String(valor || "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toUpperCase();
    if (texto.includes("12 DE OCTUBRE")) return "12 DE OCTUBRE";
    if (texto.includes("PERALTA RAMOS")) return "PERALTA RAMOS";
    if (texto.includes("BATAN")) return "BATAN";
    if (texto.includes("LIBERTADOR")) return "LIBERTADOR";
    return texto.replace(/[^A-Z0-9]/g, "");
}

function colorLineaMovilidad(linea) {
    const colores = ["#2563eb", "#0f766e", "#db3b87", "#7c3aed", "#ea580c", "#0891b2", "#65a30d"];
    const hash = [...String(linea)].reduce((total, letra) => total + letra.charCodeAt(0), 0);
    return colores[hash % colores.length];
}

function formatoCompactoMovilidad(valor) {
    return new Intl.NumberFormat("es-AR", { notation: "compact", maximumFractionDigits: 1 }).format(Number(valor || 0));
}

function formatoDineroCompactoMovilidad(valor) {
    return new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS", notation: "compact", maximumFractionDigits: 1 }).format(Number(valor || 0));
}
