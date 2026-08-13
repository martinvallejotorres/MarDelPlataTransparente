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

    const imagen =
        document.getElementById("detalleImagen");

    const contenedorImagen =
        document.querySelector(
            ".detalle-imagen-contenedor"
        );

    const fotoUrl = urlLocalSegura(reclamo.fotoUrl);

    if (fotoUrl) {

        imagen.src =
            fotoUrl;

        contenedorImagen.style.display =
            "block";

    }
    else {

        imagen.removeAttribute("src");

        contenedorImagen.style.display =
            "none";

    }

    const detalleUbicacion = document.getElementById("detalleUbicacion");
    detalleUbicacion.replaceChildren();
    const iconoUbicacion = document.createElement("i");
    iconoUbicacion.className = "fa-solid fa-location-dot me-2";
    detalleUbicacion.append(
        iconoUbicacion,
        document.createTextNode(reclamo.direccion ?? "")
    );

    document.getElementById("detalleDescripcion").textContent =
        reclamo.descripcion;

    document.getElementById("cantidadApoyos").textContent =
        reclamo.apoyos ?? 0;


    // ==========================
    // Categoría del reclamo
    // ==========================

    const badge =
        document.getElementById("detallePrioridad");

    badge.textContent =
        reclamo.tipo ?? "Otros";


    // Sacamos posibles clases viejas de prioridad
    badge.classList.remove(
        "priority-high",
        "priority-medium",
        "priority-low"
    );


    // ==========================
    // Timeline
    // ==========================

    generarTimeline(
        reclamo.estado ?? "Recibido"
    );


    // Mostrar panel
    detalle.show();

}


async function compartirReclamo() {

    if (!reclamoActual) {
        return;
    }

    const url = new URL(window.location.origin);

    url.searchParams.set(
        "reclamo",
        reclamoActual.id
    );


    const datosCompartir = {

        title:
            reclamoActual.titulo,

        text:
            `Mirá este reclamo en Mar del Plata Transparente: ${reclamoActual.titulo}`,

        url:
            url.toString()

    };


    // Celular o navegador compatible
    if (navigator.share) {

        try {

            await navigator.share(
                datosCompartir
            );

        }
        catch (error) {

            // Si simplemente cerró la ventana de compartir
            if (error.name !== "AbortError") {

                console.error(
                    "Error compartiendo:",
                    error
                );

            }

        }

        return;
    }


    // PC / navegador sin Web Share API
    try {

        await navigator.clipboard.writeText(
            url.toString()
        );


        mostrarToast(
            "Enlace copiado",
            "El enlace del reclamo fue copiado al portapapeles.",
            "success"
        );

    }
    catch (error) {

        console.error(
            "No se pudo copiar el enlace:",
            error
        );

    }

}

const btnCompartir =
    document.getElementById(
        "btnCompartir"
    );

if (btnCompartir) {

    btnCompartir.addEventListener(
        "click",
        compartirReclamo
    );

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

    if (!obtenerUsuario()) {

        sessionStorage.setItem(
            "reclamoPendienteApoyo",
            reclamoActual.id
        );

        mostrarToast(
            "Iniciá sesión",
            "Necesitás una cuenta para apoyar este reclamo.",
            "warning"
        );

        const modalLogin =
            bootstrap.Modal.getOrCreateInstance(
                document.getElementById("modalLogin")
            );

        modalLogin.show();

        return;
    }

    try {

        const data = await apiFetch(
            `/reclamos/${reclamoActual.id}/apoyar`,
            {
                method: "POST"
            }
        );

        reclamoActual.apoyos =
            data.apoyos;

        document.getElementById(
            "cantidadApoyos"
        ).textContent =
            data.apoyos;

        mostrarToast(
            "Gracias",
            "Apoyaste este reclamo.",
            "success"
        );

        cargarPulsoCiudad();

    }
    catch (error) {

        mostrarToast(
            "Ya apoyaste este reclamo.",
            "No puedes volver a apoyarlo.",
            "warning"
        );

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


