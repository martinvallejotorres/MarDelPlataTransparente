console.log("reclamos.js cargado");
let reclamosMapa = [];
let reclamoUrlAbierto = false;

function abrirReclamoDesdeUrl() {

    if (reclamoUrlAbierto) {
        return;
    }


    const parametros =
        new URLSearchParams(
            window.location.search
        );


    const reclamoId =
        Number(
            parametros.get("reclamo")
        );


    if (!reclamoId) {
        return;
    }


    const reclamo =
        reclamosMapa.find(
            r => r.id === reclamoId
        );


    if (!reclamo) {
        return;
    }


    reclamoUrlAbierto = true;


    irAReclamo(
        reclamo.id
    );

}

function crearMarcadores(reclamos) {

    reclamosMapa = reclamos;

    // Limpiar marcadores anteriores
    markersLayer.clearLayers();

    reclamos.forEach(reclamo => {

        const marker = L.marker(
            [
                reclamo.latitud,
                reclamo.longitud
            ],
            {
                icon: crearIconoReclamo(reclamo.tipo)
            }
        );

        marker.on("click", () => {

            console.log(
                "Click en reclamo:",
                reclamo
            );

            abrirDetalle(reclamo);

        });

        markersLayer.addLayer(marker);

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

            const emoji =
                iconosReclamos[r.tipo] ?? "📌";


            item.className = "urgente-item";


            item.innerHTML = `

                <div class="urgente-posicion">
                    ${index + 1}
                </div>

                <div class="urgente-contenido">

                    <div class="urgente-titulo">
                        ${r.titulo}
                    </div>

                    <div class="urgente-detalle">

                        <span class="urgente-categoria">
                            ${r.tipo}
                        </span>

                        <span class="urgente-zona">
                            ${r.zona ?? "Sin zona"}
                        </span>

                    </div>

                </div>

                <div class="urgente-apoyos">

                    <i class="fa-solid fa-thumbs-up"></i>

                    <span>
                        ${r.apoyos ?? 0}
                    </span>

                </div>

            `;


            item.addEventListener("click", () => {

                irAReclamo(r.id);

            });


            contenedor.appendChild(item);

        });


        cargarCategoriasMapa(data);

    }
    catch (error) {

        console.error(
            "Error Pulso:",
            error
        );

    }

}

