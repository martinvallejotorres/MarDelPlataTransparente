console.log("comisarias.js cargado");


// ==========================================
// CAPA DE COMISARÍAS
// ==========================================

const comisariasLayer = L.layerGroup();

let comisariasCargadas = false;


// ==========================================
// ICONO
// ==========================================

function crearIconoComisaria() {

    return L.divIcon({
        className: "comisaria-icon-container",

        html: `
            <div class="comisaria-marker">
                <i class="fa-solid fa-shield-halved"></i>
            </div>
        `,

        iconSize: [42, 42],

        iconAnchor: [21, 21]
    });
}


// ==========================================
// CARGAR COMISARÍAS
// ==========================================

async function cargarComisarias() {

    // Si ya las descargamos una vez,
    // no volvemos a consultar la API.
    if (comisariasCargadas) {
        return;
    }


    try {

        const response = await fetch(
            "/api/datos-publicos/comisarias"
        );


        if (!response.ok) {

            throw new Error(
                `Error HTTP ${response.status}`
            );
        }


        const comisarias =
            await response.json();


        console.log(
            "Comisarías obtenidas:",
            comisarias
        );


        comisariasLayer.clearLayers();


        comisarias.forEach(comisaria => {

            if (
                !Number.isFinite(comisaria.latitud) ||
                !Number.isFinite(comisaria.longitud) ||
                comisaria.latitud < -38.20 || comisaria.latitud > -37.70 ||
                comisaria.longitud < -57.85 || comisaria.longitud > -57.30
            ) {
                return;
            }


            const marker =
                L.marker(
                    [
                        comisaria.latitud,
                        comisaria.longitud
                    ],
                    {
                        icon: crearIconoComisaria()
                    }
                );


            // ==================================
            // HOVER
            // ==================================

            marker.bindTooltip(
                `
                    <div class="comisaria-tooltip">

                        <strong>
                            ${escaparHtml(comisaria.nombre)}
                        </strong>

                        <span>
                            ${escaparHtml(comisaria.direccion)}
                        </span>

                    </div>
                `,
                {
                    direction: "top",
                    offset: [0, -18]
                }
            );


            // ==================================
            // CLICK
            // ==================================

            marker.on("click", function () {

                abrirDetalleComisaria(
                    comisaria
                );

            });


            comisariasLayer.addLayer(marker);

        });


        comisariasCargadas = true;


        console.log(
            `${comisarias.length} dependencias cargadas`
        );

    }
    catch (error) {

        console.error(
            "Error cargando comisarías:",
            error
        );

        mostrarToast(
            "Comisarías no disponibles",
            "La fuente oficial no respondió. Probá nuevamente en unos minutos.",
            "warning"
        );
    }
}

let seguridadDatos = null;

async function cargarDatosSeguridad() {

    try {

        if (seguridadDatos) {
            return seguridadDatos;
        }

        const response = await fetch(
            "/api/datos-publicos/seguridad"
        );

        if (!response.ok) {
            throw new Error(
                `Error HTTP ${response.status}`
            );
        }

        seguridadDatos =
            await response.json();

        return seguridadDatos;

    }
    catch (error) {

        console.error(
            "Error cargando datos de seguridad:",
            error
        );

        return null;
    }
}

async function abrirDetalleComisaria(comisaria) {

    // ==========================================
    // MOSTRAR SEGURIDAD / OCULTAR OBRAS
    // ==========================================

    const seccionSeguridad =
        document.querySelector(
            ".datos-ciudad-seccion"
        );

    if (seccionSeguridad) {
        seccionSeguridad.style.display =
            "block";
    }


    const detalleObra =
        document.getElementById(
            "detalleObraDatos"
        );

    if (detalleObra) {
        detalleObra.style.display =
            "none";
    }


    // ==========================================
    // CABECERA
    // ==========================================

    document.getElementById(
        "datosCiudadCategoria"
    ).textContent =
        "SEGURIDAD";


    document.getElementById(
        "datosCiudadTitulo"
    ).textContent =
        comisaria.nombre;


    // ==========================================
    // ETIQUETAS
    // ==========================================

    document.getElementById(
        "datosCiudadEtiquetaDireccion"
    ).textContent =
        "Dirección";


    document.getElementById(
        "datosCiudadEtiquetaTelefono"
    ).textContent =
        "Teléfono";


    // ==========================================
    // ICONOS
    // ==========================================

    document.getElementById(
        "datosCiudadIconoDireccion"
    ).className =
        "fa-solid fa-location-dot";


    document.getElementById(
        "datosCiudadIconoTelefono"
    ).className =
        "fa-solid fa-phone";


    // ==========================================
    // DATOS DE LA COMISARÍA
    // ==========================================

    document.getElementById(
        "datosCiudadDireccion"
    ).textContent =
        comisaria.direccion ||
        "Sin dirección informada";


    document.getElementById(
        "datosCiudadTelefono"
    ).textContent =
        comisaria.telefono ||
        "Sin teléfono informado";


    document.getElementById(
        "datosCiudadOrganismo"
    ).textContent =
        "Policía de la Provincia de Buenos Aires";


    // ==========================================
    // DATOS GENERALES DE SEGURIDAD
    // ==========================================

    const datosSeguridad =
        await cargarDatosSeguridad();


    if (datosSeguridad) {

        document.getElementById(
            "seguridadCamaras"
        ).textContent =
            datosSeguridad.camaras
                .toLocaleString(
                    "es-AR"
                );


        document.getElementById(
            "seguridadPresupuesto"
        ).textContent =
            formatearDinero(
                datosSeguridad
                    .presupuestoMantenimientoCamaras
            );


        document.getElementById(
            "seguridadLicitacion"
        ).textContent =
            datosSeguridad
                .licitacion;


        document.getElementById(
            "seguridadDescripcion"
        ).textContent =
            datosSeguridad
                .descripcion;


        document.getElementById(
            "seguridadAnio"
        ).textContent =
            datosSeguridad
                .anio;


        document.getElementById(
            "seguridadFuente"
        ).textContent =
            datosSeguridad
                .fuente;


        document.getElementById(
            "seguridadActualizacion"
        ).textContent =
            "Actualizado: " +
            formatearFecha(
                datosSeguridad
                    .fechaActualizacion
            );
    }


    // ==========================================
    // ABRIR OFFCANVAS
    // ==========================================

    const offcanvasElement =
        document.getElementById(
            "datosCiudadOffcanvas"
        );


    const offcanvas =
        bootstrap.Offcanvas
            .getOrCreateInstance(
                offcanvasElement
            );


    offcanvas.show();
}

function formatearDinero(valor) {

    if (!valor) {
        return "-";
    }

    if (valor >= 1000000000) {

        return (
            "$" +
            (
                valor /
                1000000000
            )
                .toLocaleString(
                    "es-AR",
                    {
                        maximumFractionDigits: 2
                    }
                )
            +
            " mil millones"
        );
    }


    if (valor >= 1000000) {

        return (
            "$" +
            (
                valor /
                1000000
            )
                .toLocaleString(
                    "es-AR",
                    {
                        maximumFractionDigits: 2
                    }
                )
            +
            " millones"
        );
    }


    return valor.toLocaleString(
        "es-AR",
        {
            style: "currency",
            currency: "ARS",
            maximumFractionDigits: 0
        }
    );
}

function formatearFecha(fecha) {

    if (!fecha) {
        return "-";
    }

    return new Date(fecha)
        .toLocaleDateString(
            "es-AR",
            {
                day: "2-digit",
                month: "2-digit",
                year: "numeric"
            }
        );
}

// ==========================================
// CONTROL DE CAPAS DE INFORMACIÓN
// ==========================================

const ControlDatosCiudad =
    L.Control.extend({

        options: {
            position: "topright"
        },

        onAdd: function () {

            const contenedor =
                L.DomUtil.create(
                    "div",
                    "control-datos-wrapper"
                );

            contenedor.innerHTML = `
                <button
                    type="button"
                    class="control-datos-boton"
                    id="btnCapasCiudad"
                    title="Seguridad"
                >
                    <i class="fa-solid fa-shield-halved"></i>
                    <span>Capas</span>
                </button>

                <div
                    class="control-datos-panel"
                    id="panelCapasCiudad"
                >

                    <div class="control-datos-header">
                        <i class="fa-solid fa-layer-group"></i>

                        <span>
                            Datos de la ciudad
                        </span>
                    </div>


                    <label class="control-datos-opcion">

                        <input
                            type="checkbox"
                            id="toggleComisarias"
                        >

                        <span class="control-datos-icono seguridad">
                            <i class="fa-solid fa-shield-halved"></i>
                        </span>

                        <span>
                            Comisarías
                        </span>

                    </label>

                    <div class="control-datos-separador">
                        OBRAS
                    </div>

                    <label class="control-datos-opcion">

                        <input
                            type="checkbox"
                            id="toggleObras"
                        >

                        <span class="control-datos-icono obras">
                            <i class="fa-solid fa-person-digging"></i>
                        </span>

                        <span>
                            Obras públicas
                        </span>

                    </label>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="toggleTramosObras">
                        <span class="control-datos-icono tramos">
                            <i class="fa-solid fa-road"></i>
                        </span>
                        <span>Tramos de obras</span>
                    </label>

                    <div class="control-datos-separador">
                        ADMINISTRACIÓN PÚBLICA
                    </div>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="toggleDelegacionesMunicipales">
                        <span class="control-datos-icono administracion">
                            <i class="fa-solid fa-map-location-dot"></i>
                        </span>
                        <span>Delegaciones municipales</span>
                    </label>

                    <div class="control-datos-separador">
                        MOVILIDAD
                    </div>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="toggleRecorridosColectivos">
                        <span class="control-datos-icono movilidad">
                            <i class="fa-solid fa-route"></i>
                        </span>
                        <span>Recorridos de colectivos</span>
                    </label>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="toggleParadasColectivos">
                        <span class="control-datos-icono movilidad">
                            <i class="fa-solid fa-bus-simple"></i>
                        </span>
                        <span>Paradas de colectivos</span>
                    </label>

                    <div class="control-datos-separador">
                        MEDIO AMBIENTE
                    </div>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="toggleArroyosAmbiente">
                        <span class="control-datos-icono ambiente">
                            <i class="fa-solid fa-water"></i>
                        </span>
                        <span>Arroyos</span>
                    </label>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="togglePuntosAguaAmbiente">
                        <span class="control-datos-icono ambiente">
                            <i class="fa-solid fa-droplet"></i>
                        </span>
                        <span>Puntos de muestreo de agua</span>
                    </label>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="toggleEstacionesAmbiente">
                        <span class="control-datos-icono ambiente">
                            <i class="fa-solid fa-wind"></i>
                        </span>
                        <span>Estaciones de aire y olores</span>
                    </label>

                    <div class="control-datos-separador">
                        SALUD Y SERVICIOS SOCIALES
                    </div>

                    <label class="control-datos-opcion">
                        <input type="checkbox" id="toggleCentrosSalud">
                        <span class="control-datos-icono salud">
                            <i class="fa-solid fa-house-medical"></i>
                        </span>
                        <span>Centros municipales de salud</span>
                    </label>

                </div>
            `;

            L.DomEvent.disableClickPropagation(
                contenedor
            );

            L.DomEvent.disableScrollPropagation(
                contenedor
            );

            return contenedor;
        }
    });


map.addControl(
    new ControlDatosCiudad()
);

const btnCapasCiudad =
    document.getElementById(
        "btnCapasCiudad"
    );

const panelCapasCiudad =
    document.getElementById(
        "panelCapasCiudad"
    );


if (
    btnCapasCiudad &&
    panelCapasCiudad
) {

    btnCapasCiudad.addEventListener(
        "click",
        function () {

            panelCapasCiudad
                .classList
                .toggle("abierto");

        }
    );


    document.addEventListener(
        "click",
        function (event) {

            const wrapper =
                document.querySelector(
                    ".control-datos-wrapper"
                );

            if (
                wrapper &&
                !wrapper.contains(
                    event.target
                )
            ) {

                panelCapasCiudad
                    .classList
                    .remove("abierto");
            }

        }
    );
}

// ==========================================
// ACTIVAR / DESACTIVAR COMISARÍAS
// ==========================================

document.addEventListener(
    "change",
    async function (event) {

        if (
            event.target.id !==
            "toggleComisarias"
        ) {
            return;
        }


        const activo =
            event.target.checked;


        if (activo) {

            await cargarComisarias();

            if (
                !map.hasLayer(
                    comisariasLayer
                )
            ) {

                comisariasLayer.addTo(
                    map
                );
            }

        }
        else {

            if (
                map.hasLayer(
                    comisariasLayer
                )
            ) {

                map.removeLayer(
                    comisariasLayer
                );
            }
        }
    }
);
