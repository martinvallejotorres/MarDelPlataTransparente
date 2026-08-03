// ==========================================
// MAPA - MAR DEL PLATA TRANSPARENTE
// ==========================================


// ==========================================
// Crear mapa centrado en Mar del Plata
// ==========================================

const map = L.map('map', {

    // Si querés desactivar el zoom con la rueda:
    // scrollWheelZoom: false,

    zoomControl: false

}).setView(
    [-38.0055, -57.5426],
    13
);


// ==========================================
// Zoom abajo a la derecha
// ==========================================

L.control.zoom({
    position: "bottomright"
}).addTo(map);


// ==========================================
// CAPAS DEL MAPA
// ==========================================


// Mapa de calles

const capaCalles = L.tileLayer(

    'https://tile.openstreetmap.org/{z}/{x}/{y}.png',

    {
        attribution:
            '&copy; OpenStreetMap contributors',

        maxZoom: 19
    }

);


// Vista satelital

const capaSatelite = L.tileLayer(

    'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',

    {
        attribution:
            'Tiles © Esri — Sources: Esri and contributors',

        // Esri tiene buena resolución hasta este nivel
        // en nuestra zona.
        maxNativeZoom: 17,

        // Leaflet permite seguir acercando.
        maxZoom: 19
    }

);


// Satélite como mapa inicial

capaSatelite.addTo(map);


// ==========================================
// Selector Satélite / Mapa
// ==========================================

L.control.layers(

    {
        "Satélite": capaSatelite,
        "Mapa": capaCalles
    },

    null,

    {
        position: "topright"
    }

).addTo(map);


// ==========================================
// CAPAS GLOBALES
// ==========================================


// Heatmap

let heatLayer;


// ==========================================
// MARKER CLUSTER
// ==========================================

let markersLayer = L.markerClusterGroup({

    // No mostrar el polígono azul cuando
    // pasamos el mouse por un cluster.
    showCoverageOnHover: false,


    // Si varios reclamos están prácticamente
    // en el mismo punto, los separa visualmente.
    spiderfyOnMaxZoom: true,


    // Al tocar una burbuja nos acerca
    // automáticamente al área.
    zoomToBoundsOnClick: true,


    // Desde zoom 17 dejamos de agrupar
    // y mostramos los reclamos individuales.
    disableClusteringAtZoom: 17,


    // ======================================
    // Diseño personalizado de las burbujas
    // ======================================

    iconCreateFunction: function (cluster) {

        const cantidad =
            cluster.getChildCount();


        let clase =
            "cluster-bajo";


        // 10 o más reclamos
        if (cantidad >= 10) {

            clase =
                "cluster-alto";

        }

        // Entre 5 y 9 reclamos
        else if (cantidad >= 5) {

            clase =
                "cluster-medio";

        }


        return L.divIcon({

            html: `
                <div class="cluster-bubble ${clase}">
                    ${cantidad}
                </div>
            `,

            className:
                "cluster-container",

            iconSize:
                [48, 48],

            iconAnchor:
                [24, 24]

        });

    }

});


// Agregar clusters al mapa

map.addLayer(markersLayer);

map.on("zoomend", () => {

    const zoomActual = map.getZoom();

    if (!heatLayer) {
        return;
    }

    if (zoomActual >= 16) {

        if (map.hasLayer(heatLayer)) {
            map.removeLayer(heatLayer);
        }

    }
    else {

        if (!map.hasLayer(heatLayer)) {
            heatLayer.addTo(map);
        }

    }

});

// ==========================================
// BOTÓN NUEVO RECLAMO
// Mostrar solamente cuando el mapa
// está visible en pantalla
// ==========================================

const btnNuevoReclamo =
    document.getElementById(
        "btnNuevoReclamo"
    );


const mapaElemento =
    document.getElementById(
        "map"
    );


if (
    btnNuevoReclamo &&
    mapaElemento
) {

    const observerMapa =
        new IntersectionObserver(

            entries => {

                const mapaVisible =
                    entries[0].isIntersecting;


                btnNuevoReclamo.classList.toggle(

                    "oculto",

                    !mapaVisible

                );

            },

            {
                threshold: 0.15
            }

        );


    observerMapa.observe(
        mapaElemento
    );

}