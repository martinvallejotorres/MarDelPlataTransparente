console.log("reclamos.js cargado");
let reclamosMapa = [];

function crearMarcadores(reclamos) {

    reclamosMapa = reclamos;


    // Limpiar marcadores anteriores
    markersLayer.clearLayers();


    reclamos.forEach(reclamo => {

        const marker = L.marker([

            reclamo.latitud,

            reclamo.longitud

        ],
            {
                icon: crearIconoReclamo(reclamo.tipo)
            });


        marker.addTo(markersLayer);



        marker.on("click", () => {


            console.log("Click en reclamo:", reclamo);


            abrirDetalle(reclamo);


        });



    });

}


// Iconos de reclamos

const iconosReclamos = {

    "Baches": "🚧",
    "Basura": "🗑️",
    "Agua": "💧",
    "Alumbrado": "💡",
    "Espacios Verdes": "🌳",
    "Tránsito": "🚦",
    "Árboles caídos": "🌲",
    "Otros": "📌"

};

// Crear icono emoji para Leaflet

function crearIconoReclamo(tipo) {


    const emoji = iconosReclamos[tipo] || "📌";


    return L.divIcon({

        className: "emoji-marker",

        html: `
            <div class="marker-emoji">
                ${emoji}
            </div>
        `,

        iconSize: [
            35,
            35
        ],

        iconAnchor: [
            17,
            17
        ]

    });

}

async function cargarPulsoCiudad() {


    try {


        const response = await fetch('/api/reclamos/estadisticas');


        if (!response.ok) {

            throw new Error(
                "Error cargando estadísticas"
            );

        }

        const data = await response.json();


        document.getElementById(
            "totalReclamos"
        ).textContent = data.reclamosActivos;



        document.getElementById(
            "totalVotos"
        ).textContent = data.votosTotales;



        const contenedor =
            document.getElementById(
                "topReclamos"
            );



        contenedor.innerHTML = "";


        data.masUrgentes.forEach((r, index) => {

            const item = document.createElement("div");

            const emoji = iconosReclamos[r.tipo] ?? "📌";

            item.className = "urgente-item";

            item.innerHTML = `

        <div class="urgente-header">

            <span class="puesto">
                ${index + 1}
            </span>

            <span class="titulo">

                ${emoji} ${r.titulo}

            </span>

        </div>

        <div class="urgente-info">

            <span>

                📍 ${r.zona ?? "Sin zona"}

            </span>

            <span>

                👍 ${r.apoyos}

            </span>

        </div>

    `;

            contenedor.appendChild(item);

        });



    }
    catch (error) {


        console.error(
            "Error Pulso:",
            error
        );


    }


}

cargarPulsoCiudad();

setInterval(
    cargarPulsoCiudad,
    30000
);