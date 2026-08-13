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

            const apoyos = reclamo.apoyos ?? 0;

            lista.innerHTML += `

                <div class="col-lg-6">

                    <div class="card reclamo-card shadow-sm">

                        <div class="card-body">

                            <div class="d-flex justify-content-between">

                                <h5>

                                    ${escaparHtml(reclamo.titulo || "(Sin título)")}

                                </h5>

                                <span class="badge estado-badge ${obtenerBadgeEstado(reclamo.estado)}">

                                    ${escaparHtml(reclamo.estado)}

                                </span>

                            </div>

                            <div class="reclamo-info">

                                <i class="fa-solid fa-location-dot me-2"></i>

                                ${escaparHtml(reclamo.zona ?? "Sin zona")}

                            </div>

                            <div class="reclamo-info">

                                <i class="fa-solid fa-tag me-2"></i>

                                ${escaparHtml(reclamo.tipo)}

                            </div>

                            <div class="apoyos mb-3">

                                ❤️ ${apoyos} apoyos

                            </div>

                            <div class="text-end">

                                <button

                                    class="btn btn-primary"

                                    data-reclamo-id="${Number(reclamo.id)}">

                                    Gestionar

                                </button>

                            </div>

                        </div>

                    </div>

                </div>

            `;

        });

        lista.querySelectorAll("[data-reclamo-id]").forEach(boton => {
            boton.addEventListener("click", () =>
                verDetalle(Number(boton.dataset.reclamoId)));
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
                <strong>${escaparHtml(detalle.creadoPor.nombre)}</strong><br>
                ${escaparHtml(detalle.creadoPor.email)}
              `
                : "Sin información";


        document.getElementById("detalleAdministrador").innerHTML =

            detalle.asignadoA
                ? `
                <strong>${escaparHtml(detalle.asignadoA.nombre)}</strong><br>
                ${escaparHtml(detalle.asignadoA.email)}
              `
                : "Sin asignar";


        const historial = document.getElementById("detalleHistorial");

        historial.innerHTML = "";


        detalle.historial.forEach(item => {

            historial.innerHTML += `

                <div class="border rounded p-2 mb-2">

                    <strong>

                        ${escaparHtml(item.estadoAnterior)}

                    </strong>

                    →

                    <strong>

                        ${escaparHtml(item.estadoNuevo)}

                    </strong>

                    <br>

                    <small>

                        ${escaparHtml(item.usuario)}

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
            "Estado actualizado",
            "El estado del reclamo se guardó correctamente.",
            "success"
        );

    }

    catch (error) {

        console.error(error);

        mostrarToast(
            "Error",
            error.message,
            "danger"
        );

    }

}

async function actualizarDatosPagina() {

    try {

        await cargarReclamos();

    }
    catch (error) {

        console.error(
            "Error actualizando datos:",
            error
        );

    }

}

actualizarDatosPagina();

setInterval(
    actualizarDatosPagina,
    15000
);

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
