let medioAmbienteDatos = null;
let solicitudMedioAmbiente = 0;
let graficoResiduosAmbiente = null;
let graficoRecuperacionAmbiente = null;
let graficoPlayasAmbiente = null;
const geoJsonAmbiente = {};
const cargasGeoJsonAmbiente = {};

const arroyosAmbienteLayer = L.geoJSON(null, {
    style: { color: "#0284c7", weight: 2.5, opacity: .82 },
    onEachFeature: (feature, layer) => {
        const propiedades = feature?.properties || {};
        const nombre = propiedades.nombre || propiedades.Nombre || propiedades.arroyo || "Arroyo";
        layer.bindTooltip(`<div class="obra-tooltip"><strong>${escaparHtml(String(nombre))}</strong><span>Red hídrica oficial</span></div>`, { sticky: true });
    }
});

const puntosAguaAmbienteLayer = L.markerClusterGroup({
    chunkedLoading: true,
    disableClusteringAtZoom: 16,
    maxClusterRadius: 45,
    showCoverageOnHover: false
});

const estacionesAmbienteLayer = L.geoJSON(null, {
    pointToLayer: (feature, latlng) => L.circleMarker(latlng, {
        radius: 8, color: "#166534", fillColor: "#22c55e", fillOpacity: .86, weight: 2
    }),
    onEachFeature: (feature, layer) => {
        const estacion = feature?.properties?.estacion || "Estación ambiental";
        layer.bindTooltip(`<strong>${escaparHtml(String(estacion))}</strong><br>Monitoreo de aire y olores`);
    }
});

document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("anioMedioAmbiente")?.addEventListener("change", cargarMedioAmbiente);
    document.getElementById("activarArroyosAmbiente")?.addEventListener("click", () => activarCapaAmbiente("arroyos"));
    document.getElementById("activarPuntosAguaAmbiente")?.addEventListener("click", () => activarCapaAmbiente("puntos-agua"));
    document.getElementById("activarEstacionesAmbiente")?.addEventListener("click", () => activarCapaAmbiente("estaciones"));
});

async function cargarMedioAmbiente() {
    const panel = document.getElementById("panelCategoriaMedioAmbiente");
    if (!panel || panel.hidden) return;
    const solicitud = ++solicitudMedioAmbiente;
    const anio = document.getElementById("anioMedioAmbiente")?.value || "2024";
    panel.classList.add("cargando");
    try {
        const response = await fetch(`/api/datos-publicos/medio-ambiente?anio=${anio}`);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const resultado = await response.json();
        if (solicitud !== solicitudMedioAmbiente) return;
        medioAmbienteDatos = resultado.resumen;
        renderizarMedioAmbiente();
    }
    catch (error) {
        console.error("Error cargando Medio Ambiente:", error);
        mostrarToast?.("Datos ambientales no disponibles", "No pudimos leer las series municipales. Probá nuevamente.", "warning");
    }
    finally {
        if (solicitud === solicitudMedioAmbiente) panel.classList.remove("cargando");
    }
}

function renderizarMedioAmbiente() {
    const datos = medioAmbienteDatos;
    if (!datos) return;
    document.getElementById("residuosAmbiente").textContent = `${formatoNumeroAmbiente(datos.residuosDispuestosToneladas)} t`;
    document.getElementById("recuperadoAmbiente").textContent = datos.materialRecuperadoToneladas
        ? `${formatoNumeroAmbiente(datos.materialRecuperadoToneladas)} t` : "Sin publicación";
    document.getElementById("muestrasAguaAmbiente").textContent = datos.muestrasAgua
        ? formatoNumeroAmbiente(datos.muestrasAgua) : "Sin publicación";
    document.getElementById("playasAmbiente").textContent = datos.playasMuestreadas
        ? formatoNumeroAmbiente(datos.playasMuestreadas) : "Sin publicación";
    document.getElementById("camionesAmbiente").textContent = formatoNumeroAmbiente(datos.camionesDescargados);
    document.getElementById("tasaRecuperacionAmbiente").textContent = datos.materialIngresadoToneladas
        ? `${Number(datos.tasaRecuperacion).toLocaleString("es-AR", { maximumFractionDigits: 1 })}%` : "Sin publicación";
    document.getElementById("ecoliAmbiente").textContent = datos.muestrasAgua
        ? `${formatoNumeroAmbiente(datos.resultadosEColiDetectables)} de ${formatoNumeroAmbiente(datos.muestrasAgua)}` : "Sin publicación";
    document.getElementById("playasReferenciaAmbiente").textContent = datos.playasMuestreadas
        ? `${formatoNumeroAmbiente(datos.playasSobreReferencia)} de ${formatoNumeroAmbiente(datos.playasMuestreadas)}` : "Sin publicación";
    document.getElementById("periodoMedioAmbiente").innerHTML =
        `Cobertura publicada: <strong>${escaparHtml(datos.cobertura)}</strong>`;
    renderizarGraficosMedioAmbiente();
}

function renderizarGraficosMedioAmbiente() {
    if (!medioAmbienteDatos || typeof Chart === "undefined") return;
    const meses = medioAmbienteDatos.meses;
    graficoResiduosAmbiente = reemplazarGraficoAmbiente(graficoResiduosAmbiente, "graficoResiduosAmbiente", {
        type: "bar",
        data: {
            labels: meses.map(x => x.mes),
            datasets: [{ label: "Toneladas dispuestas", data: meses.map(x => x.residuosDispuestosToneladas), backgroundColor: "#f59e0b", borderRadius: 6 }]
        },
        options: opcionesGraficoAmbiente({ scales: { y: { beginAtZero: true, ticks: { callback: formatoCompactoAmbiente } } } })
    });

    graficoRecuperacionAmbiente = reemplazarGraficoAmbiente(graficoRecuperacionAmbiente, "graficoRecuperacionAmbiente", {
        type: "bar",
        data: {
            labels: meses.map(x => x.mes),
            datasets: [
                { label: "Ingreso a planta (t)", data: meses.map(x => x.materialIngresadoToneladas), backgroundColor: "#64748b", borderRadius: 5 },
                { label: "Recuperado (t)", data: meses.map(x => x.materialRecuperadoToneladas), backgroundColor: "#22c55e", borderRadius: 5 }
            ]
        },
        options: opcionesGraficoAmbiente({ scales: { y: { beginAtZero: true } } })
    });

    const playas = medioAmbienteDatos.playas.slice(0, 18);
    graficoPlayasAmbiente = reemplazarGraficoAmbiente(graficoPlayasAmbiente, "graficoPlayasAmbiente", {
        type: "bar",
        data: {
            labels: playas.map(x => x.playa),
            datasets: [
                { label: "Media geométrica de enterococos", data: playas.map(x => x.enterococos), backgroundColor: playas.map(x => x.superaReferencia ? "#dc2626" : "#0ea5e9"), borderRadius: 5 },
                { label: "Referencia provincial (35)", data: playas.map(() => 35), type: "line", borderColor: "#f59e0b", pointRadius: 0, borderDash: [6, 4] }
            ]
        },
        options: opcionesGraficoAmbiente({ indexAxis: "y", scales: { x: { beginAtZero: true } } })
    });
}

function reemplazarGraficoAmbiente(actual, id, configuracion) {
    actual?.destroy();
    const canvas = document.getElementById(id);
    return canvas ? new Chart(canvas, configuracion) : null;
}

function opcionesGraficoAmbiente(adicionales = {}) {
    return { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: "bottom" } }, ...adicionales };
}

function capaAmbiente(tipo) {
    if (tipo === "arroyos") return arroyosAmbienteLayer;
    if (tipo === "puntos-agua") return puntosAguaAmbienteLayer;
    return estacionesAmbienteLayer;
}

function toggleAmbiente(tipo) {
    return document.getElementById(tipo === "arroyos" ? "toggleArroyosAmbiente" :
        tipo === "puntos-agua" ? "togglePuntosAguaAmbiente" : "toggleEstacionesAmbiente");
}

async function cargarGeoJsonAmbiente(tipo) {
    if (geoJsonAmbiente[tipo]) return geoJsonAmbiente[tipo];
    if (cargasGeoJsonAmbiente[tipo]) return cargasGeoJsonAmbiente[tipo];
    cargasGeoJsonAmbiente[tipo] = fetch(`/api/datos-publicos/medio-ambiente/${tipo}`)
        .then(response => {
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            return response.json();
        })
        .then(datos => {
            geoJsonAmbiente[tipo] = datos;
            renderizarCapaAmbiente(tipo);
            return datos;
        })
        .finally(() => { delete cargasGeoJsonAmbiente[tipo]; });
    return cargasGeoJsonAmbiente[tipo];
}

function renderizarCapaAmbiente(tipo) {
    const datos = geoJsonAmbiente[tipo];
    if (!datos) return;
    const capa = capaAmbiente(tipo);
    capa.clearLayers();
    if (tipo !== "puntos-agua") {
        capa.addData(datos);
        return;
    }
    for (const feature of datos.features || []) {
        const coordenadas = feature?.geometry?.coordinates;
        if (!Array.isArray(coordenadas) || coordenadas.length < 2) continue;
        const id = feature?.properties?.id_punto || feature?.properties?.cartodb_id || "Sin identificador";
        const marcador = L.circleMarker([coordenadas[1], coordenadas[0]], {
            radius: 4.5, color: "#0369a1", fillColor: "#38bdf8", fillOpacity: .82, weight: 1.4
        });
        marcador.bindTooltip(`<strong>Punto de muestreo de agua</strong><br>ID ${escaparHtml(String(id))}`);
        capa.addLayer(marcador);
    }
}

async function activarCapaAmbiente(tipo, enfocar = true) {
    try {
        await cargarGeoJsonAmbiente(tipo);
        const capa = capaAmbiente(tipo);
        if (!map.hasLayer(capa)) capa.addTo(map);
        const toggle = toggleAmbiente(tipo);
        if (toggle) toggle.checked = true;
        if (enfocar) enfocarCapaAmbiente(capa);
    }
    catch (error) {
        console.error(`Error cargando capa ambiental ${tipo}:`, error);
        mostrarToast?.("Capa ambiental no disponible", "La fuente cartográfica municipal no respondió.", "warning");
    }
}

function enfocarCapaAmbiente(capa) {
    document.getElementById("map")?.scrollIntoView({ behavior: "smooth", block: "center" });
    setTimeout(() => {
        map.invalidateSize();
        const limites = capa.getBounds?.();
        if (limites?.isValid()) map.flyToBounds(limites, { padding: [35, 35], maxZoom: 14, duration: 1 });
    }, 450);
}

document.addEventListener("change", async event => {
    const tipos = {
        toggleArroyosAmbiente: "arroyos",
        togglePuntosAguaAmbiente: "puntos-agua",
        toggleEstacionesAmbiente: "estaciones"
    };
    const tipo = tipos[event.target.id];
    if (!tipo) return;
    const capa = capaAmbiente(tipo);
    if (event.target.checked) await activarCapaAmbiente(tipo, false);
    else if (map.hasLayer(capa)) map.removeLayer(capa);
});

function formatoNumeroAmbiente(valor) {
    return Number(valor || 0).toLocaleString("es-AR", { maximumFractionDigits: 1 });
}

function formatoCompactoAmbiente(valor) {
    return new Intl.NumberFormat("es-AR", { notation: "compact", maximumFractionDigits: 1 }).format(Number(valor || 0));
}
