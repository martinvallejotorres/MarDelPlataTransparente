let obrasHistorial = [];
let filtroTextoObras = "";
let filtroOrganismoObras = "todos";
let filtroEstadoObras = "todos";

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

});

    
    


async function cargarObrasPorAnio(anio) {

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


        obrasHistorial =
            resultado.obras || [];


        cargarOrganismosHistorial();

        actualizarResumenObrasAnio();

        renderizarObrasAnio();

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
}

function actualizarResumenObrasAnio() {

    document.getElementById(
        "cantidadObrasAnio"
    ).textContent =
        obrasHistorial.length;


    const presupuesto =
        obrasHistorial.reduce(
            (total, obra) =>
                total +
                Number(
                    obra.presupuestoOficial || 0
                ),
            0
        );


    document.getElementById(
        "presupuestoObrasAnio"
    ).textContent =
        formatearDineroHistorial(
            presupuesto
        );
}

function renderizarObrasAnio() {

    const lista =
        document.getElementById(
            "listaObrasAnio"
        );


    const obras =
        obtenerObrasHistorialFiltradas();


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

                const clasificacion =
                    clasificarEstadoObra(
                        obra
                    );


                if (
                    filtroEstadoObras ===
                    "proceso"
                    &&
                    clasificacion !==
                    "proximas"
                ) {
                    return false;
                }


                if (
                    filtroEstadoObras ===
                    "ejecucion"
                    &&
                    clasificacion !==
                    "ejecucion"
                ) {
                    return false;
                }


                if (
                    filtroEstadoObras ===
                    "finalizada"
                    &&
                    clasificacion !==
                    "finalizadas"
                ) {
                    return false;
                }
            }


            return true;
        }
    );
}

function crearTarjetaObraHistorial(obra) {

    const tieneUbicacion =
        (obra.ubicaciones || [])
            .some(
                ubicacion =>
                    ubicacion.latitud != null &&
                    ubicacion.longitud != null
            );


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
                        ${obra.estado
        || "Estado no informado"
        }
                    </span>

                    <span class="obra-historial-organismo">
                        ${obra.organismo
        || ""
        }
                    </span>

                </div>


                <h3>
                    ${obra.nombre
        || "Obra pública"
        }
                </h3>


                <div class="obra-historial-datos">

                    <div>
                        <span>
                            Presupuesto oficial
                        </span>

                        <strong>
                            ${presupuesto}
                        </strong>
                    </div>


                    <div>
                        <span>
                            Expediente
                        </span>

                        <strong>
                            ${obra.expediente
        || "No informado"
        }
                        </strong>
                    </div>

                </div>

            </div>


            <div class="obra-historial-acciones">

                <button
                    type="button"
                    onclick="abrirDetalleObraHistorial(${obra.eventoId})"
                >
                    Ver detalle
                </button>


                ${tieneUbicacion
            ? `
                            <button
                                type="button"
                                class="btn-ver-mapa"
                                onclick="verObraHistorialEnMapa(${obra.eventoId})"
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


    if (
        ubicaciones.length === 0
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
        );


    document
        .getElementById("map")
        ?.scrollIntoView({
            behavior: "smooth",
            block: "center"
        });


    setTimeout(
        () => {

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