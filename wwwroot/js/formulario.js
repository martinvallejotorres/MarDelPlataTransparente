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




function enviarReclamo() {

    if (!tipoSeleccionado) {

        alert("Seleccioná un tipo de reclamo");

        return;

    }


    const reclamo = {

        titulo:
            document.getElementById("titulo").value,

        tipo: tipoSeleccionado,


        descripcion:
            document.getElementById("descripcion").value,


        direccion:
            document.getElementById("direccion").value

    };



    console.log("Enviando:", reclamo);



    fetch('/api/reclamos',
        {

            method: "POST",

            headers: {
                "Content-Type": "application/json"
            },

            body: JSON.stringify(reclamo)

        })


        .then(async response => {



            console.log(

                "Respuesta POST:",

                response.status

            );



            if (!response.ok) {


                throw new Error(

                    "No se pudo crear el reclamo"

                );


            }

            return await response.json();

        })



        .then(data => {


            console.log(

                "Reclamo creado:",

                data

            );


            alert(

                "Reclamo creado correctamente"

            );

            cargarReclamos();

        })



        .catch(error => {

            console.error(

                "Error creando reclamo:",

                error

            );


            alert(

                "Error creando reclamo"

            );


        });

}


// Cargar al iniciar

cargarReclamos();