let administracionCargada = false;
let solicitudAdministracion = 0;
let debounceCargoAdministracion = null;
let graficoTendenciaAdministracion = null;
let graficoPlantaAdministracion = null;
let graficoCargosAdministracion = null;
let graficoHorasAdministracion = null;

const delegacionesMunicipalesLayer = L.geoJSON(null, {
    style: feature => ({
        color: feature?.properties?.strokecolo || "#0f766e",
        fillColor: feature?.properties?.fillcolor || "#14b8a6",
        fillOpacity: .24,
        weight: 2
    }),
    onEachFeature: (feature, layer) => {
        const nombre = feature?.properties?.col1 || "Delegación municipal";
        layer.bindTooltip(`<strong>${escaparHtml(nombre)}</strong>`, { sticky: true });
    }
});

let delegacionesCargadas = false;
let delegacionesCargando = null;

document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("anioAdministracion")?.addEventListener("change", cargarAdministracionPublica);
    document.getElementById("plantaAdministracion")?.addEventListener("change", cargarAdministracionPublica);
    document.getElementById("cargoAdministracion")?.addEventListener("input", () => {
        clearTimeout(debounceCargoAdministracion);
        debounceCargoAdministracion = setTimeout(cargarAdministracionPublica, 350);
    });

    document.getElementById("activarDelegacionesAdministracion")?.addEventListener("click", async () => {
        await activarDelegacionesMunicipales();
        document.getElementById("map")?.scrollIntoView({ behavior: "smooth", block: "center" });
    });
});

async function cargarAdministracionPublica() {
    const panel = document.getElementById("panelCategoriaAdministracion");
    if (!panel || panel.hidden) return;

    const solicitud = ++solicitudAdministracion;
    const anio = document.getElementById("anioAdministracion")?.value || "2026";
    const planta = document.getElementById("plantaAdministracion")?.value || "todas";
    const cargo = document.getElementById("cargoAdministracion")?.value?.trim() || "";
    panel.classList.add("cargando");

    try {
        const parametros = new URLSearchParams({ anio, planta });
        if (cargo) parametros.set("cargo", cargo);
        const response = await fetch(`/api/datos-publicos/administracion-publica?${parametros}`);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const resultado = await response.json();
        if (solicitud !== solicitudAdministracion) return;

        actualizarResumenAdministracion(resultado.resumen);
        renderizarGraficosAdministracion(resultado.resumen);
        administracionCargada = true;
    }
    catch (error) {
        console.error("Error cargando Administración Pública:", error);
        mostrarToast?.(
            "Administración Pública no disponible",
            "No pudimos leer el corte oficial de personal. Probá nuevamente.",
            "warning"
        );
    }
    finally {
        if (solicitud === solicitudAdministracion) panel.classList.remove("cargando");
    }
}

function actualizarResumenAdministracion(datos) {
    document.getElementById("totalAgentesAdministracion").textContent = datos.totalAgentes.toLocaleString("es-AR");
    document.getElementById("totalDesignacionesAdministracion").textContent = datos.totalDesignaciones.toLocaleString("es-AR");
    document.getElementById("permanentesAdministracion").textContent = datos.plantaPermanente.toLocaleString("es-AR");
    document.getElementById("temporariosAdministracion").textContent = datos.plantaTemporaria.toLocaleString("es-AR");
    document.getElementById("periodoAdministracion").innerHTML =
        `Corte: <strong>${escaparHtml(datos.periodo)}</strong> · Organismo: <strong>${escaparHtml(datos.organismo)}</strong>`;
}

function renderizarGraficosAdministracion(datos) {
    graficoTendenciaAdministracion = reemplazarGrafico(
        graficoTendenciaAdministracion,
        "graficoTendenciaAdministracion",
        {
            type: "line",
            data: {
                labels: datos.tendencia.map(x => x.anio),
                datasets: [
                    { label: "Agentes", data: datos.tendencia.map(x => x.agentes), borderColor: "#2563eb", backgroundColor: "#2563eb", tension: .25 },
                    { label: "Designaciones", data: datos.tendencia.map(x => x.designaciones), borderColor: "#db3b87", backgroundColor: "#db3b87", tension: .25 }
                ]
            },
            options: opcionesGraficoAdministracion()
        }
    );

    graficoPlantaAdministracion = reemplazarGrafico(
        graficoPlantaAdministracion,
        "graficoPlantaAdministracion",
        {
            type: "doughnut",
            data: {
                labels: datos.porPlanta.map(x => x.etiqueta),
                datasets: [{ data: datos.porPlanta.map(x => x.cantidad), backgroundColor: ["#2563eb", "#db3b87", "#14b8a6", "#f59e0b"] }]
            },
            options: opcionesGraficoAdministracion()
        }
    );

    graficoCargosAdministracion = crearGraficoBarrasHorizontal(
        graficoCargosAdministracion,
        "graficoCargosAdministracion",
        datos.cargosFrecuentes,
        "Designaciones",
        "#2563eb"
    );

    graficoHorasAdministracion = crearGraficoBarrasHorizontal(
        graficoHorasAdministracion,
        "graficoHorasAdministracion",
        datos.porCargaHoraria,
        "Registros",
        "#14b8a6"
    );
}

function crearGraficoBarrasHorizontal(actual, id, serie, etiqueta, color) {
    return reemplazarGrafico(actual, id, {
        type: "bar",
        data: {
            labels: serie.map(x => x.etiqueta),
            datasets: [{ label: etiqueta, data: serie.map(x => x.cantidad), backgroundColor: color, borderRadius: 6 }]
        },
        options: { ...opcionesGraficoAdministracion(), indexAxis: "y" }
    });
}

function reemplazarGrafico(actual, id, configuracion) {
    actual?.destroy();
    const canvas = document.getElementById(id);
    return canvas ? new Chart(canvas, configuracion) : null;
}

function opcionesGraficoAdministracion() {
    return {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { position: "bottom" } },
        scales: { y: { beginAtZero: true, ticks: { precision: 0 } } }
    };
}

async function cargarDelegacionesMunicipales() {
    if (delegacionesCargadas) return;
    if (delegacionesCargando) return delegacionesCargando;
    delegacionesCargando = (async () => {
        try {
            const response = await fetch("/api/datos-publicos/administracion-publica/delegaciones");
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            delegacionesMunicipalesLayer.addData(await response.json());
            delegacionesCargadas = true;
        }
        finally {
            delegacionesCargando = null;
        }
    })();
    return delegacionesCargando;
}

async function activarDelegacionesMunicipales() {
    await cargarDelegacionesMunicipales();
    if (!map.hasLayer(delegacionesMunicipalesLayer)) delegacionesMunicipalesLayer.addTo(map);
    const toggle = document.getElementById("toggleDelegacionesMunicipales");
    if (toggle) toggle.checked = true;
    if (delegacionesMunicipalesLayer.getLayers().length) {
        map.flyToBounds(delegacionesMunicipalesLayer.getBounds(), { padding: [25, 25], duration: 1 });
    }
}

document.addEventListener("change", async event => {
    if (event.target.id !== "toggleDelegacionesMunicipales") return;
    if (event.target.checked) {
        try {
            await cargarDelegacionesMunicipales();
            delegacionesMunicipalesLayer.addTo(map);
        }
        catch (error) {
            event.target.checked = false;
            mostrarToast?.("Delegaciones no disponibles", "La fuente geográfica municipal no respondió.", "warning");
        }
    }
    else if (map.hasLayer(delegacionesMunicipalesLayer)) {
        map.removeLayer(delegacionesMunicipalesLayer);
    }
});
