const panel = document.getElementById("detallePanel");

let detalle;
let reclamoActual = null;

document.addEventListener("DOMContentLoaded", () => {

    detalle = new bootstrap.Offcanvas(
        document.getElementById("detalleOffcanvas")
    );

    document.getElementById("btnApoyar")
        .addEventListener(
            "click",
            apoyarReclamo
        );

});


function abrirDetalle(reclamo) {

    reclamoActual = reclamo;

    document.getElementById("detalleTitulo").textContent =
        reclamo.titulo;

    document.getElementById("detalleImagen").src =
        reclamo.fotoUrl ?? "https://placehold.co/700x400";

    document.getElementById("detalleUbicacion").innerHTML =
        `<i class="fa-solid fa-location-dot me-2"></i>${reclamo.direccion}`;

    document.getElementById("detalleDescripcion").textContent =
        reclamo.descripcion;

    document.getElementById("cantidadApoyos").textContent =
        reclamo.apoyos ?? 0;

    const prioridad = obtenerPrioridad(reclamo.apoyos ?? 0);

    const badge = document.getElementById("detallePrioridad");

    badge.textContent = prioridad.texto;

    badge.classList.remove(
        "priority-high",
        "priority-medium",
        "priority-low"
    );

    badge.classList.add(prioridad.clase);

    generarTimeline(
        reclamo.estado ?? "Recibido"
    );

    detalle.show();

}

function obtenerPrioridad(apoyos) {

    if (apoyos >= 100) {

        return {
            texto: "Alta Prioridad",
            clase: "priority-high"
        };

    }

    if (apoyos >= 50) {

        return {
            texto: "Media Prioridad",
            clase: "priority-medium"
        };

    }

    return {
        texto: "Baja Prioridad",
        clase: "priority-low"
    };

}


function cerrarDetalle() {
    panel.classList.remove("abierto");
}

const botonCerrar = document.getElementById("cerrarPanel");

if (botonCerrar) {

    botonCerrar.addEventListener(
        "click",
        cerrarDetalle
    );

}

async function apoyarReclamo() {

    if (!reclamoActual) return;

    try {

        const response = await fetch(

            `/api/reclamos/${reclamoActual.id}/apoyar`,

            {
                method: "POST"
            }

        );

        if (!response.ok) {

            throw new Error();

        }

        const data = await response.json();

        reclamoActual.apoyos = data.apoyos;

        document.getElementById(
            "cantidadApoyos"
        ).textContent = data.apoyos;

        cargarPulsoCiudad();

    }
    catch (error) {

        console.error(error);

        alert("No se pudo apoyar el reclamo");

    }

}

function generarTimeline(estado) {

    const estados = [
        "Recibido",
        "En revisión",
        "Programado",
        "Resuelto"
    ];


    let html = "";


    let encontrado = false;


    estados.forEach(e => {


        let activo = !encontrado;


        html += `

        <div class="timeline-item">

            <div class="
                timeline-dot 
                ${activo ? "active" : ""}
            "></div>


            <div>

                ${e}

            </div>

        </div>

        `;


        if (e === estado) {
            encontrado = true;
        }


    });



    document.getElementById(
        "timelineEstado"
    ).innerHTML = html;


}


