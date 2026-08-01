let reclamoActual = null;

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

        document.getElementById("cantidad").textContent =
            datos.masUrgentes.length;

    }

    catch (error) {

        console.error(error);

    }

}


async function cargarReclamos() {

    try {

        const reclamos = await apiFetch("/admin/reclamos");

        const tabla = document.getElementById("tablaReclamos");

        tabla.innerHTML = "";

        reclamos.forEach(reclamo => {

            const apoyos = reclamo.apoyosUsuarios
                ? reclamo.apoyosUsuarios.length
                : 0;

            tabla.innerHTML += `
                <tr>

                    <td>${reclamo.id}</td>

                    <td>${reclamo.titulo}</td>

                    <td>
                        <span class="badge bg-secondary">
                            ${reclamo.estado}
                        </span>
                    </td>

                    <td>${reclamo.zona ?? "-"}</td>

                    <td>${apoyos}</td>

                    <td>

                        <button
                            class="btn btn-sm btn-primary"
                            onclick="verDetalle(${reclamo.id})">

                            Gestionar

                        </button>

                    </td>

                </tr>
            `;

        });

    }

    catch (error) {

        console.error(error);

    }

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

        alert("Estado actualizado correctamente.");

    }

    catch (error) {

        console.error(error);

        alert(error.message);

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

});