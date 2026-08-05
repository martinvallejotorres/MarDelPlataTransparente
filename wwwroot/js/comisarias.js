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
                !comisaria.latitud ||
                !comisaria.longitud
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
                            ${comisaria.nombre}
                        </strong>

                        <span>
                            ${comisaria.direccion}
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

    document.getElementById(
        "datosCiudadCategoria"
    ).textContent = "Seguridad";


    document.getElementById(
        "datosCiudadTitulo"
    ).textContent =
        comisaria.nombre;


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


    const datosSeguridad =
        await cargarDatosSeguridad();


    if (datosSeguridad) {

        document.getElementById(
            "seguridadCamaras"
        ).textContent =
            datosSeguridad.camaras
                .toLocaleString("es-AR");


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
            datosSeguridad.licitacion;


        document.getElementById(
            "seguridadDescripcion"
        ).textContent =
            datosSeguridad.descripcion;

        document.getElementById(
            "seguridadAnio"
        ).textContent =
            datosSeguridad.anio;


        document.getElementById(
            "seguridadFuente"
        ).textContent =
            datosSeguridad.fuente;


        document.getElementById(
            "seguridadActualizacion"
        ).textContent =
            "Actualizado: " +
            formatearFecha(
                datosSeguridad.fechaActualizacion
            );
    }


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
                    <span>Seguridad</span>
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