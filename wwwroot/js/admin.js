let reclamoActual = null;
let listaReclamos = [];

document
    .getElementById("btnVolver")
    .onclick = () => {

        window.location = "/";

    };


async function cargarEstadisticas() {

    try {

        const datos = await apiFetch("/reclamos/estadisticas");

        document.getElementById("activos").textContent =
            datos.reclamosActivos;

        document.getElementById("votos").textContent =
            datos.votosTotales;

        document.getElementById("revision").textContent =
            datos.enRevision;

        document.getElementById("solucionados").textContent =
            datos.solucionados;

    }

    catch (error) {

        console.error(error);

    }

}

function obtenerBadgeEstado(estado) {

    switch (estado) {

        case "Recibido":
            return "bg-secondary";

        case "En revisión":
            return "bg-info";

        case "En proceso":
            return "bg-warning text-dark";

        case "Solucionado":
            return "bg-success";

        case "Rechazado":
            return "bg-danger";

        default:
            return "bg-secondary";
    }

}

async function cargarReclamos() {

    try {

        listaReclamos = await apiFetch("/admin/reclamos");

        const lista = document.getElementById("listaReclamos");

        lista.innerHTML = "";

        listaReclamos.forEach(reclamo => {

            const apoyos = reclamo.apoyosUsuarios
                ? reclamo.apoyosUsuarios.length
                : 0;

            lista.innerHTML += `

                <div class="col-lg-6">

                    <div class="card reclamo-card shadow-sm">

                        <div class="card-body">

                            <div class="d-flex justify-content-between">

                                <h5>

                                    ${reclamo.titulo || "(Sin título)"}

                                </h5>

                                <span class="badge estado-badge ${obtenerBadgeEstado(reclamo.estado)}">

                                    ${reclamo.estado}

                                </span>

                            </div>

                            <div class="reclamo-info">

                                <i class="fa-solid fa-location-dot me-2"></i>

                                ${reclamo.zona ?? "Sin zona"}

                            </div>

                            <div class="reclamo-info">

                                <i class="fa-solid fa-tag me-2"></i>

                                ${reclamo.tipo}

                            </div>

                            <div class="apoyos mb-3">

                                ❤️ ${apoyos} apoyos

                            </div>

                            <div class="text-end">

                                <button

                                    class="btn btn-primary"

                                    onclick="verDetalle(${reclamo.id})">

                                    Gestionar

                                </button>

                            </div>

                        </div>

                    </div>

                </div>

            `;

        });

    }

    catch (error) {

        console.error(error);

    }

}

function filtrarReclamos() {

    const texto = document
        .getElementById("buscarReclamo")
        .value
        .toLowerCase();

    document
        .querySelectorAll("#listaReclamos .col-lg-6")
        .forEach(card => {

            card.style.display =
                card.textContent
                    .toLowerCase()
                    .includes(texto)
                    ? ""
                    : "none";

        });

}

async function verDetalle(id) {

    try {

        const detalle = await apiFetch(`/admin/reclamos/${id}`);
        reclamoActual = id;

        document.getElementById("detalleTitulo").textContent =
            detalle.titulo;

        document.getElementById("detalleDescripcion").textContent =
            detalle.descripcion;

        document.getElementById("detalleDireccion").textContent =
            detalle.direccion;

        document.getElementById("detalleZona").textContent =
            detalle.zona;

        document.getElementById("nuevoEstado").value =
            detalle.estado;

        document.getElementById("detalleApoyos").textContent =
            detalle.apoyos;

        document.getElementById("detalleUsuario").innerHTML =

            detalle.creadoPor
                ? `
                <strong>${detalle.creadoPor.nombre}</strong><br>
                ${detalle.creadoPor.email}
              `
                : "Sin información";


        document.getElementById("detalleAdministrador").innerHTML =

            detalle.asignadoA
                ? `
                <strong>${detalle.asignadoA.nombre}</strong><br>
                ${detalle.asignadoA.email}
              `
                : "Sin asignar";


        const historial = document.getElementById("detalleHistorial");

        historial.innerHTML = "";


        detalle.historial.forEach(item => {

            historial.innerHTML += `

                <div class="border rounded p-2 mb-2">

                    <strong>

                        ${item.estadoAnterior}

                    </strong>

                    →

                    <strong>

                        ${item.estadoNuevo}

                    </strong>

                    <br>

                    <small>

                        ${item.usuario}

                    </small>

                    <br>

                    <small class="text-muted">

                        ${new Date(item.fecha).toLocaleString()}

                    </small>

                </div>

            `;

        });


        new bootstrap.Modal(
            document.getElementById("modalDetalle")
        ).show();

    }

    catch (error) {

        console.error(error);

    }

}

async function guardarEstado() {

    try {

        const estado = document
            .getElementById("nuevoEstado")
            .value;

        await apiFetch(

            `/admin/reclamos/${reclamoActual}/estado`,

            {

                method: "PUT",

                body: JSON.stringify({

                    estado

                })

            }

        );

        bootstrap.Modal
            .getInstance(
                document.getElementById("modalDetalle")
            )
            .hide();

        await cargarReclamos();

        await cargarEstadisticas();

        mostrarToast(
            "Estado actualizado correctamente"
        );

    }

    catch (error) {

        console.error(error);

        mostrarToast(
            "Error al actualizar el reclamo"
        );

    }

}




document.addEventListener("DOMContentLoaded", () => {

    cargarEstadisticas();

    cargarReclamos();

    document
        .getElementById("btnGuardarEstado")
        .addEventListener(
            "click",
            guardarEstado
        );

    document
        .getElementById("buscarReclamo")
        .addEventListener("input", filtrarReclamos);

});