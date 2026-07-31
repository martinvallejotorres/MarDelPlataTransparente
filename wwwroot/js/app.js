// Crear mapa centrado en Mar del Plata

const map = L.map('map').setView(
    [-38.0055, -57.5426],
    13
);


// Capa del mapa

L.tileLayer(
    'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    {
        attribution: 'OpenStreetMap'
    }
).addTo(map);



// Capas globales

let heatLayer;

let markersLayer = L.layerGroup().addTo(map);


