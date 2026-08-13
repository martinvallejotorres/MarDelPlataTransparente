let obrasHistorial = [];
let filtroTextoObras = "";
let filtroOrganismoObras = "todos";
let filtroEstadoObras = "todos";
let solicitudHistorialActual = 0;
let serieHistoricaObras = {};
let cargaSerieHistoricaObras = null;
let graficoTendenciaObras = null;
let graficoEstadosObras = null;
let graficoOrganismosObras = null;
let graficoCoberturaObras = null;

document.addEventListener("DOMContentLoaded", () =>{

    const selector =
        document.getElementById(
            "anioObras"
        );


    if (!selector) {
        return;
    }


    selector.addEventListener(
        "change",
        () => {
            cargarObrasPorAnio(
                selector.value
            );
        }
    );


    cargarObrasPorAnio(
        selector.value
    );


    const buscador =
        document.getElementById(
            "buscarObraHistorial"
        );

    const organismo =
        document.getElementById(
            "filtroOrganismoObras"
        );

    const estado =
        document.getElementById(
            "filtroEstadoObras"
        );


    buscador?.addEventListener(
        "input",
        () => {

            filtroTextoObras =
                buscador.value
                    .trim()
                    .toLowerCase();

            renderizarObrasAnio();
        }
    );


    organismo?.addEventListener(
        "change",
        () => {

            filtroOrganismoObras =
                organismo.value;

            renderizarObrasAnio();
        }
    );


    estado?.addEventListener(
        "change",
        () => {

            filtroEstadoObras =
                estado.value;

            renderizarObrasAnio();
        }
    );

    document.getElementById("mostrarObrasFiltradasMapa")?.addEventListener(
        "click",
        mostrarObrasFiltradasEnMapa
    );

    document.getElementById("activarTramosObrasDashboard")?.addEventListener(
        "click",
        mostrarTramosFiltradosEnMapa
    );

});

    
    


async function cargarObrasPorAnio(anio) {

    const solicitudId = ++solicitudHistorialActual;

    const lista =
        document.getElementById(
            "listaObrasAnio"
        );


    lista.innerHTML = `
        <p class="obras-cargando">
            Cargando obras de ${anio}...
        </p>
    `;


    try {

        const response =
            await fetch(
                `/api/datos-publicos/obras/anio/${anio}`
            );


        if (!response.ok) {
            throw new Error(
                `HTTP ${response.status}`
            );
        }


        const resultado =
            await response.json();

        if (solicitudId !== solicitudHistorialActual) return;


        obrasHistorial =
            resultado.obras || [];

        serieHistoricaObras[Number(anio)] = obrasHistorial;


        cargarOrganismosHistorial();

        cargarEstadosHistorial();

        renderizarObrasAnio();

        cargarSerieHistoricaParaGraficos();

        cargarComparacionTramos();

    }
    catch (error) {

        console.error(
            "Error cargando historial de obras:",
            error
        );


        lista.innerHTML = `
            <p class="obras-error">
                No se pudieron cargar las obras.
            </p>
        `;

    }
}

function cargarOrganismosHistorial() {

    const select =
        document.getElementById(
            "filtroOrganismoObras"
        );


    if (!select) {
        return;
    }


    const organismos =
        [
            ...new Set(
                obrasHistorial
                    .map(
                        obra =>
                            obra.organismo
                    )
                    .filter(Boolean)
            )
        ]
            .sort();


    select.innerHTML = `
        <option value="todos">
            Todos los organismos
        </option>
    `;


    organismos.forEach(
        organismo => {

            const option =
                document.createElement(
                    "option"
                );

            option.value =
                organismo;

            option.textContent =
                organismo;

            select.appendChild(
                option
            );

        }
    );

    if (!select.querySelector(`option[value="${CSS.escape(filtroOrganismoObras)}"]`)) {
        filtroOrganismoObras = "todos";
    }
    select.value = filtroOrganismoObras;
}

function clasificarEstadoHistorial(obra) {
    const estado = (obra.estado || "").toLowerCase();
    if (estado.includes("final") || estado.includes("termin") || estado.includes("recepción definitiva")) return "finalizada";
    if (estado.includes("ejec") || estado.includes("inicio") || estado.includes("contrato")) return "ejecucion";
    if (estado.includes("adjud")) return "adjudicada";
    if (estado.includes("apertura")) return "apertura";
    return "licitada";
}

function cargarEstadosHistorial() {
    const select = document.getElementById("filtroEstadoObras");
    if (!select) return;

    const estados = [
        ["licitada", "Licitada"],
        ["apertura", "Apertura realizada"],
        ["adjudicada", "Adjudicada"],
        ["ejecucion", "En ejecución"],
        ["finalizada", "Finalizada"]
    ];
    const conteos = Object.fromEntries(estados.map(([valor]) => [valor, 0]));
    obrasHistorial.forEach(obra => conteos[clasificarEstadoHistorial(obra)]++);

    select.innerHTML = `<option value="todos">Todos los estados (${obrasHistorial.length})</option>` +
        estados.map(([valor, etiqueta]) =>
            `<option value="${valor}" ${conteos[valor] === 0 ? "disabled" : ""}>${etiqueta} (${conteos[valor]})</option>`
        ).join("");

    if (!select.querySelector(`option[value="${filtroEstadoObras}"]:not(:disabled)`)) {
        filtroEstadoObras = "todos";
    }
    select.value = filtroEstadoObras;
}

function actualizarResumenObrasAnio() {
    actualizarDashboardObras();
}

function renderizarObrasAnio() {

    const lista =
        document.getElementById(
            "listaObrasAnio"
        );


    const obras =
        obtenerObrasHistorialFiltradas();

    actualizarDashboardObras(obras);


    if (
        obras.length === 0
    ) {

        lista.innerHTML = `
            <div class="sin-obras-anio">
                No encontramos obras
                con estos filtros.
            </div>
        `;

        return;
    }


    lista.innerHTML =
        obras
            .map(
                obra =>
                    crearTarjetaObraHistorial(
                        obra
                    )
            )
            .join("");

    lista.querySelectorAll("[data-obra-accion]").forEach(boton => {
        boton.addEventListener("click", () => {
            const eventoId = Number(boton.dataset.eventoId);
            if (boton.dataset.obraAccion === "mapa") {
                verObraHistorialEnMapa(eventoId);
            }
            else {
                abrirDetalleObraHistorial(eventoId);
            }
        });
    });
}

function obtenerObrasHistorialFiltradas() {

    return obrasHistorial.filter(
        obra => {

            // TEXTO

            const textoObra = `
                ${obra.nombre || ""}
                ${obra.expediente || ""}
                ${obra.ubicacionTexto || ""}
                ${obra.licitacion || ""}
                ${obra.organismo || ""}
            `
                .toLowerCase();


            if (
                filtroTextoObras
                &&
                !textoObra.includes(
                    filtroTextoObras
                )
            ) {
                return false;
            }


            // ORGANISMO

            if (
                filtroOrganismoObras !==
                "todos"
                &&
                obra.organismo !==
                filtroOrganismoObras
            ) {
                return false;
            }


            // ESTADO

            if (
                filtroEstadoObras !==
                "todos"
            ) {

                const clasificacion = clasificarEstadoHistorial(obra);

                if (clasificacion !== filtroEstadoObras) return false;
            }


            return true;
        }
    );
}

function filtrarObrasParaDashboard(obras) {
    return (obras || []).filter(obra => {
        const textoObra = `${obra.nombre || ""} ${obra.expediente || ""} ${obra.ubicacionTexto || ""} ${obra.licitacion || ""} ${obra.organismo || ""}`.toLowerCase();
        if (filtroTextoObras && !textoObra.includes(filtroTextoObras)) return false;
        if (filtroOrganismoObras !== "todos" && obra.organismo !== filtroOrganismoObras) return false;
        return filtroEstadoObras === "todos" || clasificarEstadoHistorial(obra) === filtroEstadoObras;
    });
}

function esTramoObraCompleto(tramo) {
    return Number.isFinite(tramo?.latitudInicio) &&
        Number.isFinite(tramo?.longitudInicio) &&
        Number.isFinite(tramo?.latitudFin) &&
        Number.isFinite(tramo?.longitudFin);
}

function clasificarCoberturaObra(obra) {
    const tienePunto = (obra.ubicaciones || []).some(ubicacion =>
        Number.isFinite(ubicacion.latitud) && Number.isFinite(ubicacion.longitud));
    const tieneTramo = (obra.tramos || []).some(esTramoObraCompleto);
    if (tienePunto || tieneTramo) return "Precisa";
    if ((obra.tipoGeometria || "").trim() || (obra.ubicacionTexto || "").trim()) return "Zona o referencia";
    return "Sin ubicación";
}

function actualizarDashboardObras(obras = obtenerObrasHistorialFiltradas()) {
    const presupuesto = obras.reduce((total, obra) => total + Number(obra.presupuestoOficial || 0), 0);
    const tramos = obras.reduce(
        (total, obra) => total + (obra.tramos || []).filter(esTramoObraCompleto).length,
        0
    );
    const obrasMapeadas = obras.filter(obra => clasificarCoberturaObra(obra) === "Precisa").length;
    const cobertura = obras.length ? Math.round(obrasMapeadas * 100 / obras.length) : 0;

    document.getElementById("cantidadObrasAnio").textContent = obras.length.toLocaleString("es-AR");
    document.getElementById("presupuestoObrasAnio").textContent = formatearDineroHistorial(presupuesto);
    document.getElementById("tramosObrasAnio").textContent = tramos.toLocaleString("es-AR");
    document.getElementById("coberturaMapaObrasAnio").textContent = `${cobertura}%`;

    const anio = document.getElementById("anioObras")?.value || "";
    const resultado = document.getElementById("resultadoFiltrosObras");
    if (resultado) {
        resultado.textContent = `${obras.length} obra${obras.length === 1 ? "" : "s"} en ${anio} con los filtros actuales. El presupuesto suma cada expediente unificado una sola vez.`;
    }

    renderizarGraficosObras(obras);
}

async function cargarSerieHistoricaParaGraficos() {
    if (cargaSerieHistoricaObras) return cargaSerieHistoricaObras;

    cargaSerieHistoricaObras = Promise.all([2024, 2025, 2026].map(async anio => {
        if (serieHistoricaObras[anio]) return;
        try {
            const response = await fetch(`/api/datos-publicos/obras/anio/${anio}`);
            if (!response.ok) return;
            const resultado = await response.json();
            serieHistoricaObras[anio] = resultado.obras || [];
        }
        catch (error) {
            console.warn(`No se pudo cargar la serie de obras ${anio}:`, error);
        }
    })).finally(() => {
        cargaSerieHistoricaObras = null;
        renderizarGraficosObras(obtenerObrasHistorialFiltradas());
    });

    return cargaSerieHistoricaObras;
}

function renderizarGraficosObras(obras) {
    if (typeof Chart === "undefined") return;

    const anios = [2024, 2025, 2026];
    const series = anios.map(anio => filtrarObrasParaDashboard(serieHistoricaObras[anio] || []));
    graficoTendenciaObras = reemplazarGraficoObras(graficoTendenciaObras, "graficoTendenciaObras", {
        type: "bar",
        data: {
            labels: anios,
            datasets: [
                { label: "Obras", data: series.map(items => items.length), backgroundColor: "#2563eb", borderRadius: 6, yAxisID: "y" },
                { label: "Presupuesto", data: series.map(items => items.reduce((total, obra) => total + Number(obra.presupuestoOficial || 0), 0)), type: "line", borderColor: "#db3b87", backgroundColor: "#db3b87", tension: .25, yAxisID: "y1" }
            ]
        },
        options: opcionesGraficoObras({
            scales: {
                y: { beginAtZero: true, ticks: { precision: 0 }, title: { display: true, text: "Obras" } },
                y1: { beginAtZero: true, position: "right", grid: { drawOnChartArea: false }, ticks: { callback: formatearDineroCompactoObras }, title: { display: true, text: "Presupuesto" } }
            }
        })
    });

    const estados = [
        ["licitada", "Licitada"],
        ["apertura", "Apertura realizada"],
        ["adjudicada", "Adjudicada"],
        ["ejecucion", "En ejecución"],
        ["finalizada", "Finalizada"]
    ];
    graficoEstadosObras = reemplazarGraficoObras(graficoEstadosObras, "graficoEstadosObras", {
        type: "doughnut",
        data: {
            labels: estados.map(([, etiqueta]) => etiqueta),
            datasets: [{
                data: estados.map(([estado]) => obras.filter(obra => clasificarEstadoHistorial(obra) === estado).length),
                backgroundColor: ["#64748b", "#2563eb", "#8b5cf6", "#f59e0b", "#14b8a6"]
            }]
        },
        options: opcionesGraficoObras()
    });

    const porOrganismo = Object.entries(obras.reduce((acumulado, obra) => {
        const organismo = obra.organismo || "No informado";
        acumulado[organismo] = (acumulado[organismo] || 0) + Number(obra.presupuestoOficial || 0);
        return acumulado;
    }, {})).sort((a, b) => b[1] - a[1]);
    graficoOrganismosObras = reemplazarGraficoObras(graficoOrganismosObras, "graficoOrganismosObras", {
        type: "bar",
        data: {
            labels: porOrganismo.map(([organismo]) => organismo),
            datasets: [{ label: "Presupuesto oficial", data: porOrganismo.map(([, total]) => total), backgroundColor: "#2563eb", borderRadius: 6 }]
        },
        options: opcionesGraficoObras({ indexAxis: "y", scales: { x: { beginAtZero: true, ticks: { callback: formatearDineroCompactoObras } } } })
    });

    const coberturas = ["Precisa", "Zona o referencia", "Sin ubicación"];
    graficoCoberturaObras = reemplazarGraficoObras(graficoCoberturaObras, "graficoCoberturaObras", {
        type: "doughnut",
        data: {
            labels: coberturas,
            datasets: [{ data: coberturas.map(tipo => obras.filter(obra => clasificarCoberturaObra(obra) === tipo).length), backgroundColor: ["#14b8a6", "#f59e0b", "#cbd5e1"] }]
        },
        options: opcionesGraficoObras()
    });
}

function reemplazarGraficoObras(actual, id, configuracion) {
    actual?.destroy();
    const canvas = document.getElementById(id);
    return canvas ? new Chart(canvas, configuracion) : null;
}

function opcionesGraficoObras(adicionales = {}) {
    return {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { position: "bottom" } },
        ...adicionales
    };
}

function formatearDineroCompactoObras(valor) {
    return new Intl.NumberFormat("es-AR", {
        style: "currency",
        currency: "ARS",
        notation: "compact",
        maximumFractionDigits: 1
    }).format(Number(valor || 0));
}

function crearTarjetaObraHistorial(obra) {

    const tieneUbicacion =
        (obra.ubicaciones || [])
            .some(
                ubicacion =>
                    ubicacion.latitud != null &&
                    ubicacion.longitud != null
            ) || (obra.tramos || []).some(tramo =>
                Number.isFinite(tramo.latitudInicio) &&
                Number.isFinite(tramo.longitudInicio) &&
                Number.isFinite(tramo.latitudFin) &&
                Number.isFinite(tramo.longitudFin));


    const presupuesto =
        obra.presupuestoOficial
            ? formatearDineroHistorial(
                obra.presupuestoOficial
            )
            : "No informado";


    return `
        <article class="obra-historial-card">

            <div class="obra-historial-principal">

                <div class="obra-historial-top">

                    <span class="obra-historial-estado">
                        ${escaparHtml(obra.estado || "Estado no informado")}
                    </span>

                    <span class="obra-historial-organismo">
                        ${escaparHtml(obra.organismo || "")}
                    </span>

                </div>


                <h3>
                    ${escaparHtml(obra.nombre || "Obra pública")}
                </h3>


                <div class="obra-historial-datos">

                    <div>
                        <span>
                            Presupuesto oficial
                        </span>

                        <strong>
                            ${escaparHtml(presupuesto)}
                        </strong>
                    </div>


                    <div>
                        <span>
                            Expediente
                        </span>

                        <strong>
                            ${escaparHtml(obra.expediente || "No informado")}
                        </strong>
                    </div>

                </div>

                ${(obra.eventosRelacionados || []).length > 1 ? `
                    <small class="obra-llamados-unificados">
                        ${(obra.eventosRelacionados || []).length} llamados unificados; el presupuesto se cuenta una sola vez.
                    </small>
                ` : ""}

            </div>


            <div class="obra-historial-acciones">

                <button
                    type="button"
                    data-obra-accion="detalle"
                    data-evento-id="${Number(obra.eventoId)}"
                >
                    Ver detalle
                </button>


                ${tieneUbicacion
            ? `
                            <button
                                type="button"
                                class="btn-ver-mapa"
                                data-obra-accion="mapa"
                                data-evento-id="${Number(obra.eventoId)}"
                            >
                                <i class="fa-solid fa-location-dot"></i>
                                Ver en mapa
                            </button>
                        `
            : ""
        }

            </div>

        </article>
    `;
}

function obtenerObraHistorial(eventoId) {

    return obrasHistorial.find(
        obra =>
            obra.eventoId === eventoId
    );
}

function abrirDetalleObraHistorial(eventoId) {

    const obra =
        obtenerObraHistorial(
            eventoId
        );


    if (!obra) {
        return;
    }


    abrirDetalleObra(
        obra
    );
}

function verObraHistorialEnMapa(eventoId) {

    const obra =
        obtenerObraHistorial(
            eventoId
        );


    if (!obra) {
        return;
    }


    const ubicaciones =
        (obra.ubicaciones || [])
            .filter(
                ubicacion =>
                    ubicacion.latitud != null &&
                    ubicacion.longitud != null
            );

    const puntosTramos = (obra.tramos || [])
        .flatMap(tramo => [
            [tramo.latitudInicio, tramo.longitudInicio],
            [tramo.latitudFin, tramo.longitudFin]
        ])
        .filter(([latitud, longitud]) =>
            Number.isFinite(latitud) && Number.isFinite(longitud));


    if (
        ubicaciones.length === 0 && puntosTramos.length === 0
    ) {
        abrirDetalleObra(
            obra
        );

        return;
    }


    const puntos =
        ubicaciones.map(
            ubicacion => [
                ubicacion.latitud,
                ubicacion.longitud
            ]
        ).concat(puntosTramos);


    document
        .getElementById("map")
        ?.scrollIntoView({
            behavior: "smooth",
            block: "center"
        });


    setTimeout(
        () => {

            if (puntosTramos.length > 0) {
                if (!obrasConTramosDetalle.some(item => item.eventoId === obra.eventoId)) {
                    obrasConTramosDetalle = [...obrasConTramosDetalle, obra];
                    renderizarTramosObrasEnMapa();
                }
                activarCapaTramos();
            }

            if (
                puntos.length === 1
            ) {

                map.flyTo(
                    puntos[0],
                    17,
                    {
                        duration: 1.2
                    }
                );

            }
            else {

                map.flyToBounds(
                    L.latLngBounds(
                        puntos
                    ),
                    {
                        padding:
                            [60, 60],

                        maxZoom: 17,

                        duration: 1.2
                    }
                );

            }

        },
        500
    );
}

function obtenerPuntosObrasMapa(obras) {
    return (obras || []).flatMap(obra => [
        ...(obra.ubicaciones || [])
            .filter(ubicacion => Number.isFinite(ubicacion.latitud) && Number.isFinite(ubicacion.longitud))
            .map(ubicacion => [ubicacion.latitud, ubicacion.longitud]),
        ...(obra.tramos || [])
            .filter(esTramoObraCompleto)
            .flatMap(tramo => [
                [tramo.latitudInicio, tramo.longitudInicio],
                [tramo.latitudFin, tramo.longitudFin]
            ])
    ]);
}

function enfocarObrasDashboardEnMapa(obras) {
    const puntos = obtenerPuntosObrasMapa(obras);
    document.getElementById("map")?.scrollIntoView({ behavior: "smooth", block: "center" });
    if (!puntos.length) return;

    setTimeout(() => {
        map.invalidateSize();
        if (puntos.length === 1) map.flyTo(puntos[0], 16, { duration: 1.1 });
        else map.flyToBounds(L.latLngBounds(puntos), { padding: [50, 50], maxZoom: 16, duration: 1.1 });
    }, 450);
}

function mostrarObrasFiltradasEnMapa() {
    const obras = obtenerObrasHistorialFiltradas();
    const puntos = obtenerPuntosObrasMapa(obras);
    if (!puntos.length) {
        mostrarToast?.("Sin geometría precisa", "Las obras del resultado tienen referencias o zonas, pero no coordenadas para dibujar.", "warning");
        return;
    }

    obrasLayer.clearLayers();
    obras.forEach(agregarObraAlMapa);
    if (!map.hasLayer(obrasLayer)) obrasLayer.addTo(map);
    const toggle = document.getElementById("toggleObras");
    if (toggle) toggle.checked = true;
    enfocarObrasDashboardEnMapa(obras);
}

function mostrarTramosFiltradosEnMapa() {
    const obras = obtenerObrasHistorialFiltradas().filter(obra =>
        (obra.tramos || []).some(esTramoObraCompleto));
    if (!obras.length) {
        mostrarToast?.("Sin tramos georreferenciados", "No hay líneas completas para los filtros seleccionados.", "warning");
        return;
    }

    obrasConTramosDetalle = obras;
    renderizarTramosObrasEnMapa();
    activarCapaTramos();
    enfocarObrasDashboardEnMapa(obras);
}

function formatearDineroHistorial(valor) {

    return new Intl.NumberFormat(
        "es-AR",
        {
            style: "currency",
            currency: "ARS",
            maximumFractionDigits: 0
        }
    ).format(
        Number(valor || 0)
    );
}

async function cargarComparacionTramos() {
    const panel = document.getElementById("comparacionTramosHistoricos");
    if (!panel) return;

    try {
        const response = await fetch("/api/datos-publicos/obras/comparacion-tramos?anioDesde=2024");
        if (!response.ok) return;
        const resultado = await response.json();
        const comparaciones = resultado.comparaciones || [];
        panel.hidden = comparaciones.length === 0;
        if (comparaciones.length === 0) return;

        panel.innerHTML = `
            <strong>Calles intervenidas en distintos años</strong>
            <span>${comparaciones.length} tramo${comparaciones.length === 1 ? "" : "s"} coincidente${comparaciones.length === 1 ? "" : "s"} en la base local.</span>
            <ul>${comparaciones.slice(0, 6).map(item => `
                <li>${escaparHtml(item.calle)} (${escaparHtml(item.desde)} — ${escaparHtml(item.hasta)}): ${item.anios.join(", ")}</li>
            `).join("")}</ul>
        `;
    }
    catch (error) {
        console.warn("No se pudo cargar la comparación histórica", error);
    }
}
