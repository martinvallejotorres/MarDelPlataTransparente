console.log("formulario.js cargado");

let tipoSeleccionado = "null";

document.addEventListener("click", function (e) {

    const card = e.target.closest(".tipo-card");

    if (!card) return;


    document
        .querySelectorAll(".tipo-card")
        .forEach(c => c.classList.remove("active"));


    card.classList.add("active");


    tipoSeleccionado = card.dataset.tipo;


    console.log("Seleccionado:", tipoSeleccionado);

});



// Agrupar por cercanía geográfica (distancia máxima en grados)
function agruparReclamos(reclamos, distanciaMaxima = 0.01) {

    const grupos = [];

    reclamos.forEach(reclamo => {

        let grupoEncontrado = null;

        for (const grupo of grupos) {

            const distancia = Math.sqrt(
                Math.pow(
                    reclamo.latitud - grupo.latitud,
                    2
                )
                +
                Math.pow(
                    reclamo.longitud - grupo.longitud,
                    2
                )
            );

            if (distancia <= distanciaMaxima) {
                grupoEncontrado = grupo;
                break;
            }

        }

        if (grupoEncontrado) {

            grupoEncontrado.reclamos.push(reclamo);

            const cantidad =
                grupoEncontrado.reclamos.length;

            grupoEncontrado.latitud =
                grupoEncontrado.reclamos.reduce(
                    (suma, r) =>
                        suma + r.latitud,
                    0
                ) / cantidad;

            grupoEncontrado.longitud =
                grupoEncontrado.reclamos.reduce(
                    (suma, r) =>
                        suma + r.longitud,
                    0
                ) / cantidad;

        }
        else {

            grupos.push({
                latitud: reclamo.latitud,
                longitud: reclamo.longitud,
                reclamos: [reclamo]
            });

        }

    });

    return grupos;
}

// Obtener color según cantidad de reclamos y apoyos
function obtenerColorGrupo(grupo) {

    const cantidad =
        grupo.reclamos.length;

    const votos =
        grupo.reclamos.reduce(
            (total, reclamo) =>
                total + (reclamo.apoyos ?? 0),
            0
        );

    const intensidad =
        (cantidad * 2)
        +
        (votos / 10);

    if (intensidad >= 19) {
        return "#e74c3c";
    }

    if (intensidad >= 9) {
        return "#f1c40f";
    }

    return "#2ecc71";
}

// Crear ondas de calor para reclamos agrupados
function crearOndasReclamos(reclamos) {

    if (heatLayer) {
        map.removeLayer(heatLayer);
    }

    heatLayer = L.layerGroup();

    const grupos =
        agruparReclamos(reclamos);

    grupos.forEach(grupo => {

        const color =
            obtenerColorGrupo(grupo);

        const ondas = [
            {
                radio: 36,
                fillOpacity: 0.07,
                opacity: 0.15
            },
            {
                radio: 28,
                fillOpacity: 0.11,
                opacity: 0.23
            },
            {
                radio: 20,
                fillOpacity: 0.17,
                opacity: 0.35
            }
        ];

        ondas.forEach(onda => {

            const circulo =
                L.circleMarker(
                    [
                        grupo.latitud,
                        grupo.longitud
                    ],
                    {
                        radius: onda.radio,
                        color: color,
                        fillColor: color,
                        fillOpacity:
                            onda.fillOpacity,
                        opacity:
                            onda.opacity,
                        weight: 2,
                        interactive: false
                    }
                );

            heatLayer.addLayer(circulo);

        });

    });

    if (map.getZoom() < 16) {
        heatLayer.addTo(map);
    }
}

// Obtener reclamos desde la API
async function cargarReclamos() {

    try {

        const reclamos = await apiFetch("/reclamos");


        // ==========================
        // Reclamos visibles en mapa
        // ==========================

        const reclamosVisibles = reclamos.filter(
            reclamo => reclamo.estado !== "Rechazado"
        );


        // ==========================
        // Marcadores
        // ==========================

        crearMarcadores(reclamosVisibles);
        // Si se abrió la página desde un enlace compartido
        abrirReclamoDesdeUrl();

        // ==========================
        // Ondas / intensidad
        // ==========================

        crearOndasReclamos(
            reclamosVisibles
        );


        // ==========================
        // Actualizar detalle abierto
        // ==========================

        if (
            typeof reclamoActual !== "undefined" &&
            reclamoActual
        ) {

            const actualizado = reclamos.find(
                r => r.id === reclamoActual.id
            );

            if (actualizado) {

                reclamoActual = actualizado;

                const contador =
                    document.getElementById(
                        "cantidadApoyos"
                    );

                if (contador) {

                    contador.textContent =
                        actualizado.apoyos ?? 0;

                }

            }

        }


        return reclamos;

    }
    catch (error) {

        console.error(
            "Error cargando reclamos:",
            error
        );

    }

}

// ========================== 
async function enviarReclamo() {

    const titulo = document
        .getElementById("titulo")
        .value
        .trim();

    const descripcion = document
        .getElementById("descripcion")
        .value
        .trim();

    const direccion = document
        .getElementById("direccion")
        .value
        .trim();

    const foto = document
        .getElementById("foto")
        .files[0];


    // ==========================
    // Validaciones
    // ==========================

    if (!tipoSeleccionado) {

        mostrarToast(
            "Atención",
            "Seleccioná un tipo de reclamo.",
            "warning"
        );

        return;
    }


    if (titulo.length < 5 || titulo.length > 100) {

        mostrarToast(
            "Título inválido",
            "El título debe tener entre 5 y 100 caracteres.",
            "warning"
        );

        return;
    }


    if (descripcion.length < 10 || descripcion.length > 1000) {

        mostrarToast(
            "Descripción inválida",
            "La descripción debe tener entre 10 y 1000 caracteres.",
            "warning"
        );

        return;
    }


    if (direccion.length < 5 || direccion.length > 200) {

        mostrarToast(
            "Dirección inválida",
            "Ingresá una dirección válida.",
            "warning"
        );

        return;
    }


    // ==========================
    // Validar foto
    // ==========================

    if (foto) {

        const tamañoMaximo =
            5 * 1024 * 1024;

        const tiposPermitidos = [
            "image/jpeg",
            "image/png",
            "image/webp"
        ];


        if (foto.size > tamañoMaximo) {

            mostrarToast(
                "Imagen demasiado grande",
                "La foto no puede superar los 5 MB.",
                "warning"
            );

            return;
        }


        if (!tiposPermitidos.includes(foto.type)) {

            mostrarToast(
                "Formato no permitido",
                "Solo podés subir imágenes JPG, PNG o WEBP.",
                "warning"
            );

            return;
        }
    }


    // ==========================
    // Crear FormData
    // ==========================

    const formData = new FormData();

    formData.append("titulo", titulo);
    formData.append("tipo", tipoSeleccionado);
    formData.append("descripcion", descripcion);
    formData.append("direccion", direccion);

    if (foto) {

        formData.append("foto", foto);

    }


    // ==========================
    // Enviar reclamo
    // ==========================

    try {

        const token =
            localStorage.getItem("token");


        if (!token) {

            mostrarToast(
                "Iniciá sesión",
                "Tenés que iniciar sesión para crear un reclamo.",
                "warning"
            );

            return;
        }


        const response = await fetch(
            "/api/reclamos",
            {
                method: "POST",

                headers: {
                    Authorization: `Bearer ${token}`
                },

                body: formData
            }
        );


        if (!response.ok) {

            const error =
                await response.text();

            throw new Error(error);
        }


        const reclamo =
            await response.json();


        console.log(
            "Reclamo creado:",
            reclamo
        );


        mostrarToast(
            "Reclamo creado",
            "Tu reclamo fue publicado correctamente.",
            "success"
        );


        bootstrap.Modal
            .getInstance(
                document.getElementById(
                    "modalReclamo"
                )
            )
            .hide();


        // Limpiar formulario

        document.getElementById("titulo").value = "";
        document.getElementById("descripcion").value = "";
        document.getElementById("direccion").value = "";
        document.getElementById("foto").value = "";


        cargarReclamos();

    }
    catch (error) {

        console.error(
            "Error creando reclamo:",
            error
        );


        mostrarToast(
            "No se pudo crear",
            error.message,
            "error"
        );

    }

}


// Cargar al iniciar

cargarReclamos();






