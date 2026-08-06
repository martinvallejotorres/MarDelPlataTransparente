console.log("obras.js cargado");


// ==========================================
// CAPA DE OBRAS
// ==========================================

const obrasLayer = L.layerGroup();

let obrasCargadas = false;

let obrasDetalle = [];

let filtroObrasActual ="todas";

// ==========================================
// COLORES POR ESTADO
// ==========================================

function obtenerColorObra(estado) {

    const valor =
        (estado || "")
            .toLowerCase();

    if (
        valor.includes("final")
        ||
        valor.includes("termin")
    ) {
        return "#16a34a";
    }

    if (
        valor.includes("ejec")
    ) {
        return "#f59e0b";
    }

    return "#2563eb";
}


// ==========================================
// CARGAR OBRAS
// ==========================================

async function cargarObras() {

    if (obrasCargadas) {
        return;
    }

    try {

        const response =
            await fetch(
                "/api/datos-publicos/obras?anio=2026&mes=6"
            );

        if (!response.ok) {
            throw new Error(
                `Error HTTP ${response.status}`
            );
        }

        const obrasBasicas =
            await response.json();


        obrasDetalle = [];


        for (const obraBasica of obrasBasicas) {

            try {

                const detalleResponse =
                    await fetch(
                        `/api/datos-publicos/obras/${obraBasica.eventoId}`
                    );

                if (!detalleResponse.ok) {
                    continue;
                }


                const detalle =
                    await detalleResponse.json();


                obrasDetalle.push(
                    detalle
                );


                agregarObraAlMapa(
                    detalle
                );

            }
            catch (error) {

                console.error(
                    "Error obteniendo detalle de obra:",
                    obraBasica.eventoId,
                    error
                );
            }
        }


        obrasCargadas = true;

        actualizarPanelObras();

        console.log(
            "Obras cargadas:",
            obrasDetalle
        );

    }
    catch (error) {

        console.error(
            "Error cargando obras:",
            error
        );
    }
}

function abrirPanelObras() {

    const overlay =
        document.getElementById(
            "overlayPanelObras"
        );

    if (!overlay) {
        return;
    }

    actualizarPanelObras();

    overlay.classList.add(
        "abierto"
    );
}

function cerrarPanelObras() {

    const overlay =
        document.getElementById(
            "overlayPanelObras"
        );

    const toggleObras =
        document.getElementById(
            "toggleObras"
        );


    overlay?.classList.remove(
        "abierto"
    );


    if (toggleObras) {
        toggleObras.checked = false;
    }


    if (
        map.hasLayer(
            obrasLayer
        )
    ) {
        map.removeLayer(
            obrasLayer
        );
    }
}

function actualizarPanelObras() {

    actualizarKpisObras();

    renderizarListaObras();
}

function actualizarKpisObras() {

    const total =
        obrasDetalle.length;


    const ejecucion =
        obrasDetalle.filter(
            obra =>
                clasificarEstadoObra(
                    obra
                ) === "ejecucion"
        ).length;


    const proximas =
        obrasDetalle.filter(
            obra =>
                clasificarEstadoObra(
                    obra
                ) === "proximas"
        ).length;


    const finalizadas =
        obrasDetalle.filter(
            obra =>
                clasificarEstadoObra(
                    obra
                ) === "finalizadas"
        ).length;


    const presupuesto =
        obrasDetalle.reduce(
            (total, obra) =>
                total +
                Number(
                    obra.presupuestoOficial
                    || 0
                ),
            0
        );


    document.getElementById(
        "obrasTotal"
    ).textContent =
        total;


    document.getElementById(
        "obrasEjecucion"
    ).textContent =
        ejecucion;


    document.getElementById(
        "obrasProximas"
    ).textContent =
        proximas;


    document.getElementById(
        "obrasFinalizadas"
    ).textContent =
        finalizadas;


    document.getElementById(
        "obrasPresupuestoTotal"
    ).textContent =
        formatearDineroObra(
            presupuesto
        );
}

function clasificarEstadoObra(obra) {

    const estado =
        (obra.estado || "")
            .toLowerCase();


    if (
        estado.includes(
            "final"
        )
        ||
        estado.includes(
            "termin"
        )
    ) {
        return "finalizadas";
    }


    if (
        estado.includes(
            "ejec"
        )
    ) {
        return "ejecucion";
    }


    return "proximas";
}

function renderizarListaObras() {

    const contenedor =
        document.getElementById(
            "listaObras"
        );


    if (!contenedor) {
        return;
    }


    let obras =
        [...obrasDetalle];


    if (
        filtroObrasActual !==
        "todas"
    ) {

        obras =
            obras.filter(
                obra =>
                    clasificarEstadoObra(
                        obra
                    ) ===
                    filtroObrasActual
            );
    }


    if (
        obras.length === 0
    ) {

        contenedor.innerHTML = `
            <div
                style="
                    padding:20px;
                    text-align:center;
                    color:#94a3b8;
                    font-size:11px;
                "
            >
                No hay obras para mostrar.
            </div>
        `;

        return;
    }


    contenedor.innerHTML =
        obras.map(
            crearTarjetaObra
        ).join("");


    document
        .querySelectorAll(
            ".obra-card"
        )
        .forEach(
            tarjeta => {

                tarjeta.addEventListener(
                    "click",
                    function () {

                        const eventoId =
                            Number(
                                this.dataset.eventoId
                            );


                        const obra =
                            obrasDetalle.find(
                                x =>
                                    x.eventoId ===
                                    eventoId
                            );


                        if (!obra) {
                            return;
                        }


                        enfocarObra(
                            obra
                        );

                    }
                );

            }
        );
}

function crearTarjetaObra(obra) {

    const presupuesto =
        obra.presupuestoOficial
            ? formatearDineroObra(
                obra.presupuestoOficial
            )
            : "No informado";


    const estado =
        obtenerTextoEstadoObra(
            obra
        );


    return `

        <article
            class="obra-card"
            data-evento-id="${obra.eventoId}"
        >

            <div class="obra-card-top">

                <h5>
                    ${obra.nombre}
                </h5>

                <span class="obra-card-estado">
                    ${estado}
                </span>

            </div>


            <div class="obra-card-ubicacion">

                <i class="fa-solid fa-location-dot"></i>

                <span>
                    ${obra.ubicacionTexto
        ||
        "Ubicación no especificada"
        }
                </span>

            </div>


            <div class="obra-card-footer">

                <div class="obra-card-dato">

                    <span>
                        Presupuesto oficial
                    </span>

                    <strong>
                        ${presupuesto}
                    </strong>

                </div>


                <span class="obra-card-ver">
                    Ver detalle →
                </span>

            </div>

        </article>
    `;
}

function obtenerTextoEstadoObra(obra) {

    const clasificacion =
        clasificarEstadoObra(
            obra
        );


    switch (
    clasificacion
    ) {

        case "ejecucion":
            return "En ejecución";

        case "finalizadas":
            return "Finalizada";

        default:
            return "Próxima";
    }
}

function enfocarObra(obra) {

    /*
        Si en el futuro tenemos geometría
        exacta, primero enfocamos el mapa.

        Para obras generales abrimos
        directamente su ficha.
    */

    abrirDetalleObra(
        obra
    );
}

// ==========================================
// AGREGAR OBRA SEGÚN GEOMETRÍA
// ==========================================

function agregarObraAlMapa(obra) {

    const tipo =
        (obra.tipoGeometria || "")
            .toLowerCase();

    const ubicacion =
        (obra.ubicacionTexto || "")
            .toLowerCase();


    // ======================================
    // ZONAS DEMASIADO GENERALES
    // ======================================

    const esZonaGeneral =
        tipo === "zona" &&
        (
            ubicacion.includes("ejido urbano")
            ||
            ubicacion.includes("partido de general pueyrredon")
            ||
            ubicacion.includes("ciudad de mar del plata")
        );


    if (esZonaGeneral) {

        console.log(
            "Obra general, no se dibuja en mapa:",
            obra.nombre
        );

        return;
    }


    // ======================================
    // GEOMETRÍAS PRECISAS
    // ======================================

    switch (tipo) {

        case "zona":
            agregarObraZona(
                obra
            );
            break;

        case "tramo":
            agregarObraTramo(
                obra
            );
            break;

        case "punto":
            agregarObraPunto(
                obra
            );
            break;

        default:

            console.warn(
                "Obra sin geometría precisa:",
                obra.nombre
            );

            break;
    }
}


// ==========================================
// ZONA
// ==========================================

function agregarObraZona(obra) {

    if (
        !obra.coordenadas
        ||
        obra.coordenadas.length < 3
    ) {
        return;
    }


    const color =
        obtenerColorObra(
            obra.estado
        );


    const zona =
        L.polygon(
            obra.coordenadas,
            {
                color: color,
                weight: 2,
                opacity: .8,
                fillColor: color,
                fillOpacity: .12
            }
        );


    configurarInteraccionObra(
        zona,
        obra
    );


    obrasLayer.addLayer(
        zona
    );
}

// ==========================================
// TRAMO
// ==========================================

function agregarObraTramo(obra) {

    /*
        Cuando tengamos coordenadas reales
        del tramo, deberían venir del backend.

        Por ahora no dibujamos una línea falsa.
    */

    if (
        !obra.coordenadas
        ||
        obra.coordenadas.length < 2
    ) {
        return;
    }


    const color =
        obtenerColorObra(
            obra.estado
        );


    const linea =
        L.polyline(
            obra.coordenadas,
            {
                color: color,

                weight: 6,

                opacity: .85
            }
        );


    configurarInteraccionObra(
        linea,
        obra
    );


    obrasLayer.addLayer(
        linea
    );
}


// ==========================================
// PUNTO
// ==========================================

function agregarObraPunto(obra) {

    if (
        !obra.coordenadas
        ||
        obra.coordenadas.length === 0
    ) {
        return;
    }


    const coordenada =
        obra.coordenadas[0];


    const color =
        obtenerColorObra(
            obra.estado
        );


    const icono =
        L.divIcon({
            className:
                "obra-marker-container",

            html: `
                <div
                    class="obra-marker"
                    style="background:${color}"
                >
                    <i class="fa-solid fa-person-digging"></i>
                </div>
            `,

            iconSize:
                [42, 42],

            iconAnchor:
                [21, 21]
        });


    const marker =
        L.marker(
            coordenada,
            {
                icon: icono
            }
        );


    configurarInteraccionObra(
        marker,
        obra
    );


    obrasLayer.addLayer(
        marker
    );
}


// ==========================================
// TOOLTIP + CLICK
// ==========================================

function configurarInteraccionObra(capa, obra) {

    const presupuesto =
        obra.presupuestoOficial
            ? formatearDineroObra(
                obra.presupuestoOficial
            )
            : "Sin presupuesto informado";


    capa.bindTooltip(
        `
            <div class="obra-tooltip">

                <strong>
                    ${obra.nombre}
                </strong>

                <span>
                    ${obra.ubicacionTexto || ""}
                </span>

                <small>
                    ${presupuesto}
                </small>

            </div>
        `,
        {
            direction: "top",

            sticky: true
        }
    );


    capa.on(
        "click",
        function () {

            abrirDetalleObra(
                obra
            );

        }
    );
}


// ==========================================
// OFFCANVAS
// ==========================================

function abrirDetalleObra(obra) {

    // ============================
    // ETIQUETAS PARA OBRAS
    // ============================

    const etiquetaDireccion =
        document.getElementById(
            "datosCiudadEtiquetaDireccion"
        );

    if (etiquetaDireccion) {
        etiquetaDireccion.textContent =
            "Ámbito de ejecución";
    }


    const etiquetaTelefono =
        document.getElementById(
            "datosCiudadEtiquetaTelefono"
        );

    if (etiquetaTelefono) {
        etiquetaTelefono.textContent =
            "Licitación";
    }


    const iconoDireccion =
        document.getElementById(
            "datosCiudadIconoDireccion"
        );

    if (iconoDireccion) {
        iconoDireccion.className =
            "fa-solid fa-map-location-dot";
    }


    const iconoTelefono =
        document.getElementById(
            "datosCiudadIconoTelefono"
        );

    if (iconoTelefono) {
        iconoTelefono.className =
            "fa-solid fa-file-contract";
    }


    // ============================
    // DATOS DE LA OBRA
    // ============================

    document.getElementById(
        "datosCiudadCategoria"
    ).textContent =
        "OBRA PÚBLICA";


    document.getElementById(
        "datosCiudadTitulo"
    ).textContent =
        obra.nombre;


    document.getElementById(
        "datosCiudadDireccion"
    ).textContent =
        obra.ubicacionTexto ||
        "Sin ubicación específica informada";


    document.getElementById(
        "datosCiudadTelefono"
    ).textContent =
        obra.licitacion ||
        "Sin licitación informada";


    document.getElementById(
        "datosCiudadOrganismo"
    ).textContent =
        obra.organismo ||
        "Municipalidad de General Pueyrredon";

    const seccionSeguridad =
        document.querySelector(
            ".datos-ciudad-seccion"
        );

    if (seccionSeguridad) {
        seccionSeguridad.style.display =
            "none";
    }

    mostrarDatosObra(obra);


    const offcanvasElement =
        document.getElementById(
            "datosCiudadOffcanvas"
        );


    const offcanvas =
        bootstrap.Offcanvas.getOrCreateInstance(
            offcanvasElement
        );


    offcanvas.show();
}


// ==========================================
// BLOQUE DINÁMICO DE OBRA
// ==========================================

function mostrarDatosObra(obra) {

    let contenedor =
        document.getElementById(
            "detalleObraDatos"
        );


    if (!contenedor) {

        contenedor =
            document.createElement(
                "div"
            );

        contenedor.id =
            "detalleObraDatos";

        contenedor.className =
            "datos-ciudad-seccion detalle-obra-datos";


        const fuente =
            document.querySelector(
                ".datos-ciudad-fuente"
            );


        if (fuente) {

            fuente.parentNode.insertBefore(
                contenedor,
                fuente
            );

        }
    }


    const presupuesto =
        obra.presupuestoOficial
            ? formatearDineroObra(
                obra.presupuestoOficial
            )
            : "-";


    const superficie =
        obra.superficieM2
            ? obra.superficieM2
                .toLocaleString(
                    "es-AR"
                ) + " m²"
            : "-";


    contenedor.style.display =
        "block";


    contenedor.innerHTML = `

        <h5>
            Información de la obra
        </h5>


        <div class="obra-detalle-grid">

            <div class="obra-detalle-item">

                <span>
                    Presupuesto oficial
                </span>

                <strong>
                    ${presupuesto}
                </strong>

            </div>


            <div class="obra-detalle-item">

                <span>
                    Superficie prevista
                </span>

                <strong>
                    ${superficie}
                </strong>

            </div>


            <div class="obra-detalle-item">

                <span>
                    Frentes de trabajo
                </span>

                <strong>
                    ${obra.frentesTrabajo ?? "-"}
                </strong>

            </div>


            <div class="obra-detalle-item">

                <span>
                    Expediente
                </span>

                <strong>
                    ${obra.expediente || "-"}
                </strong>

            </div>


            <div class="obra-detalle-item">

                <span>
                    Apertura
                </span>

                <strong>
                    ${obra.fechaApertura
            ? new Date(
                obra.fechaApertura
            )
                .toLocaleDateString(
                    "es-AR"
                )
            : "-"
        }
                </strong>

            </div>


            <div class="obra-detalle-item">

                <span>
                    Alcance
                </span>

                <strong>
                    ${obra.tipoGeometria || "-"}
                </strong>

            </div>

        </div>


        <div class="obra-aclaracion">

            <i class="fa-solid fa-circle-info"></i>

            <span>
                Las ubicaciones específicas de los trabajos
                son determinadas por la Inspección de Obras.
                El área mostrada representa el ámbito general
                informado en la documentación oficial.
            </span>

        </div>
    `;


    const fuenteTexto =
        document.getElementById(
            "seguridadFuente"
        );


    if (fuenteTexto) {

        fuenteTexto.textContent =
            "EMVIAL / Municipalidad de General Pueyrredon";

    }


    const actualizacion =
        document.getElementById(
            "seguridadActualizacion"
        );


    if (actualizacion) {

        actualizacion.textContent =
            obra.licitacion || "";

    }
}


// ==========================================
// FORMATO MONETARIO
// ==========================================

function formatearDineroObra(valor) {

    return Number(valor)
        .toLocaleString(
            "es-AR",
            {
                style: "currency",

                currency: "ARS",

                maximumFractionDigits: 2
            }
        );
}

document.addEventListener(
    "change",
    async function (event) {

        if (
            event.target.id !==
            "toggleObras"
        ) {
            return;
        }


        if (event.target.checked) {

            await cargarObras();


            if (
                !map.hasLayer(
                    obrasLayer
                )
            ) {

                obrasLayer.addTo(
                    map
                );

            }
            abrirPanelObras();
        }
        else {

            if (
                map.hasLayer(
                    obrasLayer
                )
            ) {

                map.removeLayer(
                    obrasLayer
                );

            }
            cerrarPanelObras();
        }
    }
);

document.addEventListener(
    "click",
    function (event) {
       

        const botonFiltro =
            event.target.closest(
                ".obra-filtro"
            );


        if (botonFiltro) {

            document
                .querySelectorAll(
                    ".obra-filtro"
                )
                .forEach(
                    boton =>
                        boton.classList.remove(
                            "activo"
                        )
                );


            botonFiltro
                .classList.add(
                    "activo"
                );


            filtroObrasActual =
                botonFiltro
                    .dataset
                    .filtro;


            renderizarListaObras();

            return;
        }


        if (
            event.target.closest(
                "#cerrarPanelObras"
            )
        ) {

            cerrarPanelObras();

        }

    }
);

document.getElementById("overlayPanelObras")?.addEventListener(
        "click",
        function (event) {

            if (
                event.target === this
            ) {
                cerrarPanelObras();
            }

        }
    );