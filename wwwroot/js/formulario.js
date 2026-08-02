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
// Obtener reclamos desde la API

function cargarReclamos() {


    console.log("Ejecutando cargarReclamos()");


    fetch('/api/reclamos')


        .then(response => {


            console.log("Respuesta GET:", response.status);


            if (!response.ok) {

                throw new Error("Error obteniendo reclamos");

            }


            return response.json();


        })


        .then(reclamos => {


            console.log("Reclamos recibidos:", reclamos);



            let puntos = [];



            reclamos.forEach(reclamo => {


                puntos.push([

                    reclamo.latitud,

                    reclamo.longitud,

                    1

                ]);


            });



            console.log("Puntos heatmap:", puntos);



            // Crear marcadores

            crearMarcadores(reclamos);




            // Actualizar heatmap


            if (heatLayer) {

                map.removeLayer(heatLayer);

            }



            heatLayer = L.heatLayer(

                puntos,

                {

                    radius: 30,

                    blur: 25,

                    maxZoom: 15

                }

            ).addTo(map);



        })


        .catch(error => {


            console.error(

                "Error cargando reclamos:",

                error

            );


        });



}


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






